using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Reflection;
using Luckbound.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace Luckbound.Tests.Editor
{
    public sealed class F02SpriteFamilyManifestImporterTests
    {
        private const string ManifestPath = "docs/agent_staging/f02-family-pipeline/manifest.example.json";
        private const string OutputFolder = "Assets/Luckbound/Tests/EditMode/F02SpriteFamilyPipelineFixture";
        private const string TexturePath = OutputFolder + "/BellatorTechnicalFixture.png";
        private const string ClipPath = OutputFolder + "/TechnicalHold.anim";
        private const string AtlasPath = OutputFolder + "/bellator-technical-fixture.spriteatlas";
        private const string SpriteName = "BellatorTechnicalFixture_0";
        private const string FixtureResourceId = "f02-bellator-technical-image";
        private const string SourcePath = "docs/art/F01_bellator_32x48.png";
        private const string ForeignSourcePath = "docs/art/F01_master_reference.png";
        private const string PromptRecordPath = "docs/art/records/ART-F01-BELLATOR-32X48.v1.json";
        private const string MultiManifestPath = "docs/agent_staging/f02-multi-family-acceptance/manifest.multi-image.example.json";
        private const string MultiOutputFolder = "Assets/Luckbound/Tests/EditMode/F02SpriteFamilyPipelineMultiFixture";

        // Test-only reader used to create a corrupt scratch publication; production marker format is unchanged.
        [Serializable]
        private sealed class OwnershipMarkerFixture
        {
            public int schemaVersion;
            public string familyId;
            public string outputFolder;
            public string folderGuid;
            public string markerGuid;
            public string manifestFingerprintAlgorithm;
            public int outputContractVersion;
            public string manifestSha256;
            public string folderMetaSha256;
            public string markerMetaSha256;
            public OwnershipAssetFixture[] assets;
        }

        [Serializable]
        private sealed class OwnershipAssetFixture
        {
            public string fileName;
            public string guid;
            public string contentSha256;
            public string metaSha256;
        }

#if UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
            EntryPoint = "CreateSymbolicLinkW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateSymbolicLinkWindows(string symbolicLinkPath, string targetPath, uint flags);
#else
        [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true,
            EntryPoint = "symlink", ExactSpelling = true)]
        private static extern int CreateSymbolicLinkUnix(string targetPath, string symbolicLinkPath);
#endif

        [Test]
        public void SourcePayloadUsesOneValidatedSnapshotWhenPathChangesBeforeOutputWrite()
        {
            string token = Guid.NewGuid().ToString("N");
            string sourcePath = ProjectFullPath("Temp/F02SnapshotSource-" + token + ".png");
            string outputPath = ProjectFullPath("Temp/F02SnapshotOutput-" + token + ".png");
            byte[] original = File.ReadAllBytes(ProjectFullPath(SourcePath));
            byte[] replacement = original.Concat(new byte[] { 0 }).ToArray();
            string expectedSha256 = HashBytes(original);

            try
            {
                Assert.That(Directory.Exists(Path.GetDirectoryName(sourcePath)), Is.True,
                    "Unity's project Temp directory must exist before this isolated file test runs.");
                Assert.That(File.Exists(sourcePath), Is.False);
                Assert.That(File.Exists(outputPath), Is.False);
                File.WriteAllBytes(sourcePath, original);

                Type importerType = typeof(F02SpriteFamilyManifestImporter);
                Type imageType = importerType.GetNestedType("ImageSource", BindingFlags.NonPublic);
                Assert.That(imageType, Is.Not.Null);
                object image = Activator.CreateInstance(imageType, true);
                BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                imageType.GetField("sourcePath", members).SetValue(image, "Temp/F02SnapshotSource-" + token + ".png");
                imageType.GetField("sha256", members).SetValue(image, expectedSha256);
                imageType.GetField("width", members).SetValue(image, 32);
                imageType.GetField("height", members).SetValue(image, 48);
                imageType.GetField("bitDepth", members).SetValue(image, 8);
                imageType.GetField("colorType", members).SetValue(image, 6);

                MethodInfo capture = importerType.GetMethod("ReadAndValidatePng", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(capture, Is.Not.Null);
                byte[] captured = (byte[])capture.Invoke(null, new[] { sourcePath, image });
                CollectionAssert.AreEqual(original, captured);

                // Simulate replacement after validation but before the destination is written.
                File.WriteAllBytes(sourcePath, replacement);
                MethodInfo copy = importerType.GetMethod("CopySourceIfChanged", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(copy, Is.Not.Null);
                copy.Invoke(null, new object[] { captured, expectedSha256, outputPath, false });

                CollectionAssert.AreEqual(original, File.ReadAllBytes(outputPath));
                Assert.That(HashFile(outputPath), Is.EqualTo(expectedSha256));
            }
            finally
            {
                if (File.Exists(sourcePath)) File.Delete(sourcePath);
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Test]
        public void SeparatePngFamily_SecondRunPreservesSpriteClipAtlasAndFiles()
        {
            AssertOutputFolderAbsent();
            Dictionary<string, string> outsideBefore = SnapshotOutsideFamily();

            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                FamilyIdentity first = CaptureIdentity();
                Dictionary<string, string> firstFiles = SnapshotFamilyFiles();

                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                FamilyIdentity second = CaptureIdentity();
                Dictionary<string, string> secondFiles = SnapshotFamilyFiles();

                Assert.That(second.TextureGuid, Is.EqualTo(first.TextureGuid));
                Assert.That(second.SpriteGuid, Is.EqualTo(first.SpriteGuid));
                Assert.That(second.SpriteLocalId, Is.EqualTo(first.SpriteLocalId));
                Assert.That(second.ClipGuid, Is.EqualTo(first.ClipGuid));
                Assert.That(second.ClipLocalId, Is.EqualTo(first.ClipLocalId));
                Assert.That(second.AtlasGuid, Is.EqualTo(first.AtlasGuid));
                Assert.That(second.AtlasLocalId, Is.EqualTo(first.AtlasLocalId));
                CollectionAssert.AreEqual(first.ClipSpriteGuids, second.ClipSpriteGuids);
                CollectionAssert.AreEqual(first.ClipSpriteLocalIds, second.ClipSpriteLocalIds);
                CollectionAssert.AreEqual(first.AtlasPackableGuids, second.AtlasPackableGuids);
                CollectionAssert.AreEqual(first.AtlasPackableLocalIds, second.AtlasPackableLocalIds);
                AssertFileSnapshotsEqual(firstFiles, secondFiles);

                Assert.That(first.FilterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(first.MipmapsEnabled, Is.False);
                Assert.That(first.PixelsPerUnit, Is.EqualTo(16f));
                Assert.That(first.TextureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(first.FallbackPhysicsShape, Is.False);
                Assert.That(first.PhysicsShapeCount, Is.Zero);
                Assert.That(first.SpriteRect, Is.EqualTo(new Rect(0, 0, 32, 48)));
                Assert.That(first.SpritePivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(first.ClipKeyCount, Is.EqualTo(1));
                Assert.That(first.AtlasPackableGuids.Length, Is.EqualTo(1));
                Assert.That(first.AtlasIncludeInBuild, Is.False);
                Assert.That(first.AtlasFilterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(first.AtlasMipmapsEnabled, Is.False);
                Assert.That(first.AtlasPadding, Is.EqualTo(2));
                Assert.That(first.AtlasAllowsRotation, Is.False);
                Assert.That(first.AtlasTightPacking, Is.False);

                AssertFileSnapshotsEqual(outsideBefore, SnapshotOutsideFamily());
            }
            finally
            {
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ReorderedManifestObjectProperties_AreTheSamePublishedManifestAndDoNotWrite()
        {
            AssertOutputFolderAbsent();
            string reorderedManifestPath = null;
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                Dictionary<string, string> publishedAssets = SnapshotAssetsTree();

                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string reordered = MoveRootStringPropertyToEnd(json, "familyId");
                Assert.That(reordered, Is.Not.EqualTo(json), "The fixture must change root JSON property order.");
                reorderedManifestPath = WriteTemporaryManifest(reordered);

                F02SpriteFamilyManifestImporter.ImportFamily(reorderedManifestPath);

                AssertFileSnapshotsEqual(publishedAssets, SnapshotAssetsTree());
            }
            finally
            {
                if (reorderedManifestPath != null) DeleteProjectFile(reorderedManifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PublishedFamily_RejectsChangedPngAndUpdatedHashWithoutAssetsMutation()
        {
            AssertOutputFolderAbsent();
            string changedSource = null;
            string changedManifestPath = null;
            try
            {
                changedSource = CopyModifiedSourceToTemp();
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                json = ReplaceJsonStringProperty(json, "sourcePath", changedSource);
                json = ReplaceJsonStringProperty(json, "sha256", HashFile(ProjectFullPath(changedSource)));
                changedManifestPath = WriteTemporaryManifest(json);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(changedManifestPath), "immutable");
            }
            finally
            {
                if (changedManifestPath != null) DeleteProjectFile(changedManifestPath);
                if (changedSource != null) DeleteProjectFile(changedSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PublishedFamily_RejectsChangedAtlasSettingsWithoutAssetsMutation()
        {
            AssertOutputFolderAbsent();
            string changedManifestPath = null;
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string changedAtlasManifest = json.Replace("\"padding\": 2", "\"padding\": 3");
                Assert.That(changedAtlasManifest, Is.Not.EqualTo(json), "The fixture must change atlas padding.");
                changedManifestPath = WriteTemporaryManifest(changedAtlasManifest);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(changedManifestPath), "immutable");
            }
            finally
            {
                if (changedManifestPath != null) DeleteProjectFile(changedManifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ChangedContentCanUseANewVersionedFamilyIdAndOutputFolder()
        {
            const string versionedFamilyId = "bellator-technical-fixture-v2";
            string versionedOutputFolder = OutputFolder + "-v2";
            AssertOutputFolderAbsent();
            AssertOutputFolderAbsent(versionedOutputFolder);
            string changedSource = CopyModifiedSourceToTemp();
            string versionedManifestPath = null;
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                json = ReplaceJsonStringProperty(json, "familyId", versionedFamilyId);
                json = ReplaceJsonStringProperty(json, "resourceId", FixtureResourceId + "-v2");
                json = ReplaceJsonStringProperty(json, "outputFolder", versionedOutputFolder);
                json = ReplaceJsonStringProperty(json, "sourcePath", changedSource);
                json = ReplaceJsonStringProperty(json, "sha256", HashFile(ProjectFullPath(changedSource)));
                versionedManifestPath = WriteTemporaryManifest(json);

                F02SpriteFamilyManifestImporter.ImportFamily(versionedManifestPath);

                Assert.That(File.Exists(ProjectFullPath(versionedOutputFolder + "/" + versionedFamilyId + ".spriteatlas")), Is.True);
                Assert.That(File.Exists(ProjectFullPath(versionedOutputFolder + "/F02FamilyOwnership.json")), Is.True);
            }
            finally
            {
                if (versionedManifestPath != null) DeleteProjectFile(versionedManifestPath);
                DeleteProjectFile(changedSource);
                CleanupOutputFolder(versionedOutputFolder);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void MultiImageFamily_CrossImageClipsSlicesAtlasAndFilesAreStableAcrossSecondRun()
        {
            string outputFolder = MultiOutputFolder + "-" + Guid.NewGuid().ToString("N");
            AssertOutputFolderAbsent(outputFolder);
            Dictionary<string, string> outsideBefore = SnapshotOutsideFamily(outputFolder);
            string[] fixturePaths =
            {
                "docs/agent_staging/f02-multi-family-acceptance/fixtures/BellatorFramesA.png",
                "docs/agent_staging/f02-multi-family-acceptance/fixtures/BellatorFramesB.png",
                "docs/art/records/F02-MULTI-IMAGE-TEST-FIXTURE.v2.json",
                MultiManifestPath
            };
            Dictionary<string, string> fixtureBytesBefore = fixturePaths.ToDictionary(
                path => path, path => HashFile(ProjectFullPath(path)), StringComparer.Ordinal);
            string manifestPath = null;

            try
            {
                manifestPath = WriteTemporaryManifest(ReplaceJsonStringProperty(
                    File.ReadAllText(ProjectFullPath(MultiManifestPath)), "outputFolder", outputFolder));
                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);
                MultiFamilyIdentity first = CaptureMultiFamilyIdentity(outputFolder);
                Dictionary<string, string> firstFiles = SnapshotFamilyFiles(outputFolder);

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);
                MultiFamilyIdentity second = CaptureMultiFamilyIdentity(outputFolder);
                Dictionary<string, string> secondFiles = SnapshotFamilyFiles(outputFolder);

                CollectionAssert.AreEqual(first.ObjectIdentities, second.ObjectIdentities);
                CollectionAssert.AreEqual(first.Geometry, second.Geometry);
                CollectionAssert.AreEqual(first.ClipCurves, second.ClipCurves);
                CollectionAssert.AreEqual(first.AtlasPackables, second.AtlasPackables);
                AssertFileSnapshotsEqual(firstFiles, secondFiles);
                AssertFileSnapshotsEqual(outsideBefore, SnapshotOutsideFamily(outputFolder));
                foreach (KeyValuePair<string, string> fixture in fixtureBytesBefore)
                    Assert.That(HashFile(ProjectFullPath(fixture.Key)), Is.EqualTo(fixture.Value), fixture.Key);
            }
            finally
            {
                CleanupOutputFolder(outputFolder);
                if (manifestPath != null) DeleteProjectFile(manifestPath);
            }
        }

        [Test]
        public void MultiFrameAnimatorEvaluation_SamplesFrameIntervalsAndLoopBoundary()
        {
            const string outputFolder = MultiOutputFolder + "-animator-evaluation";
            const string controllerFolder = "Assets/Luckbound/Tests/EditMode";
            string controllerPath = controllerFolder + "/F02RuntimeProbe-" + Guid.NewGuid().ToString("N") + ".controller";
            string manifestPath = null;
            GameObject animatedObject = null;
            AssertOutputFolderAbsent(outputFolder);
            try
            {
                string manifestJson = ReplaceJsonStringProperty(
                    File.ReadAllText(ProjectFullPath(MultiManifestPath)), "outputFolder", outputFolder);
                string repeatedLoopFrames = "\"frames\": [\"BellatorIdle_A\", \"BellatorIdle_B\", \"BellatorIdle_A\"]";
                string distinctFrames = "\"frames\": [\"BellatorIdle_A\", \"BellatorAttack_A\", \"BellatorIdle_B\"]";
                string repeatedAttackFrames = "\"frames\": [\"BellatorAttack_B\", \"BellatorAttack_A\", \"BellatorAttack_B\"]";
                Assert.That(manifestJson.Contains(repeatedLoopFrames), Is.True, "The loop fixture sequence must be present.");
                Assert.That(manifestJson.Contains(repeatedAttackFrames), Is.True, "The non-loop fixture sequence must be present.");
                manifestJson = manifestJson.Replace(repeatedLoopFrames, distinctFrames)
                    .Replace(repeatedAttackFrames, distinctFrames);
                manifestPath = WriteTemporaryManifest(manifestJson);
                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (string path in Directory.GetFiles(ProjectFullPath(outputFolder), "*.png", SearchOption.TopDirectoryOnly))
                    foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(
                        outputFolder + "/" + Path.GetFileName(path)).OfType<Sprite>())
                        sprites.Add(sprite.name, sprite);
                string[] frames = { "BellatorIdle_A", "BellatorAttack_A", "BellatorIdle_B" };
                Assert.That(sprites[frames[0]], Is.Not.EqualTo(sprites[frames[2]]),
                    "The runtime fixture must distinguish the first and final sprites.");

                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Assert.That(controller, Is.Not.Null);
                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                stateMachine.defaultState = stateMachine.AddState("F02ImportedClip");
                AnimatorState state = controller.layers[0].stateMachine.defaultState;
                Assert.That(state, Is.Not.Null);
                animatedObject = new GameObject("F02 isolated clip evaluator") { hideFlags = HideFlags.HideAndDontSave };
                Animator animator = animatedObject.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                SpriteRenderer renderer = animatedObject.AddComponent<SpriteRenderer>();
                animator.runtimeAnimatorController = controller;

                AssertClipAnimatorPlayback(outputFolder + "/BellatorIdle.anim", true,
                    frames, sprites, controller, state, animator, renderer);
                AssertClipAnimatorPlayback(outputFolder + "/BellatorAttack.anim", false,
                    frames, sprites, controller, state, animator, renderer);
            }
            finally
            {
                if (animatedObject != null) UnityEngine.Object.DestroyImmediate(animatedObject);
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                CleanupOutputFolder(outputFolder);
                AssetDatabase.DeleteAsset(controllerPath);
            }
        }

        [Test]
        public void ExistingClipWithExtraSpriteBinding_IsRejectedWithoutAssetsMutation()
        {
            AssertExistingClipRejectedWithoutAssetsMutation(clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                    "UnexpectedChild", typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = AssetDatabase.LoadAllAssetsAtPath(TexturePath)
                        .OfType<Sprite>().Single(item => item.name == SpriteName) }
                });
            }, "only its declared SpriteRenderer sprite binding");
        }

        [Test]
        public void ExistingClipWithExtraFloatCurve_IsRejectedWithoutAssetsMutation()
        {
            AssertExistingClipRejectedWithoutAssetsMutation(clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                    "", typeof(SpriteRenderer), "m_Color.r");
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }, "undeclared float-curve bindings");
        }

        [Test]
        public void ExistingClipWithAnimationEvent_IsRejectedWithoutAssetsMutation()
        {
            AssertExistingClipRejectedWithoutAssetsMutation(clip =>
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = 0f, functionName = "UndeclaredEvent" }
                }), "undeclared animation events");
        }

        [Test]
        public void ExistingClipWithCorruptStartTime_IsRejectedWithoutRepair()
        {
            float corruptedStartTime = 0f;
            AssertExistingClipTimeCorruptionRejectedWithoutRepair(clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                corruptedStartTime = -0.005f / clip.frameRate;
                settings.startTime = corruptedStartTime;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }, clip => AssertPersistedTimeIsOutsidePreviousApproximation(
                AnimationUtility.GetAnimationClipSettings(clip).startTime, corruptedStartTime, 0f),
                "duration differs from its manifest");
        }

        [Test]
        public void ExistingClipWithCorruptStopTime_IsRejectedWithoutRepair()
        {
            float corruptedStopTime = float.NaN;
            AssertExistingClipTimeCorruptionRejectedWithoutRepair(clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                corruptedStopTime = settings.stopTime + 0.005f / clip.frameRate;
                settings.stopTime = corruptedStopTime;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }, clip => AssertPersistedTimeIsOutsidePreviousApproximation(
                AnimationUtility.GetAnimationClipSettings(clip).stopTime, corruptedStopTime, 1f / clip.frameRate),
                "duration differs from its manifest");
        }

        [Test]
        public void ExistingClipWithCorruptFrameKeyTime_IsRejectedWithoutRepair()
        {
            float corruptedFrameTime = float.NaN;
            AssertExistingMultiClipTimeCorruptionRejectedWithoutRepair(clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                corruptedFrameTime = keys[1].time + 0.005f / clip.frameRate;
                keys[1].time = corruptedFrameTime;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }, clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AssertPersistedTimeIsOutsidePreviousApproximation(keys[1].time, corruptedFrameTime, 1f / clip.frameRate);
            }, "frame reference differs from its manifest");
        }

        [Test]
        public void ExistingClipWithCorruptLastFrameKeyTime_IsRejectedWithoutRepair()
        {
            float corruptedLastFrameTime = float.NaN;
            AssertExistingMultiClipTimeCorruptionRejectedWithoutRepair(clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                int lastFrameIndex = keys.Length - 1;
                corruptedLastFrameTime = keys[lastFrameIndex].time + 0.005f / clip.frameRate;
                keys[lastFrameIndex].time = corruptedLastFrameTime;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }, clip =>
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AssertPersistedTimeIsOutsidePreviousApproximation(keys[keys.Length - 1].time,
                    corruptedLastFrameTime, 2f / clip.frameRate);
            }, "frame reference differs from its manifest");
        }

        private static void AssertExistingClipRejectedWithoutAssetsMutation(
            Action<AnimationClip> corruptClip, string expectedMessage)
        {
            AssertOutputFolderAbsent();
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                Assert.That(clip, Is.Not.Null);
                corruptClip(clip);
                AssetDatabase.SaveAssetIfDirty(clip);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), expectedMessage);
            }
            finally
            {
                CleanupOutputFolder();
            }
        }

        private static void AssertExistingClipTimeCorruptionRejectedWithoutRepair(
            Action<AnimationClip> corruptClip, Action<AnimationClip> assertCorruptionPersisted,
            string expectedMessage)
        {
            AssertOutputFolderAbsent();
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                string clipFullPath = ProjectFullPath(ClipPath);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                Assert.That(clip, Is.Not.Null);
                string originalHash = HashFile(clipFullPath);

                corruptClip(clip);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);

                AnimationClip reloadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                Assert.That(reloadedClip, Is.Not.Null);
                assertCorruptionPersisted(reloadedClip);
                string corruptedHash = HashFile(clipFullPath);
                Assert.That(corruptedHash, Is.Not.EqualTo(originalHash),
                    "The time corruption must be present in the serialized clip before validation.");

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), expectedMessage);
                Assert.That(HashFile(clipFullPath), Is.EqualTo(corruptedHash),
                    "Rejecting a corrupt existing clip must leave its serialized bytes unchanged.");
            }
            finally
            {
                CleanupOutputFolder();
            }
        }

        private static void AssertExistingMultiClipTimeCorruptionRejectedWithoutRepair(
            Action<AnimationClip> corruptClip, Action<AnimationClip> assertCorruptionPersisted,
            string expectedMessage)
        {
            const string outputFolder = MultiOutputFolder;
            const string clipPath = MultiOutputFolder + "/BellatorAttack.anim";
            AssertOutputFolderAbsent(outputFolder);
            string manifestPath = null;
            try
            {
                string manifestJson = ReplaceJsonStringProperty(
                    File.ReadAllText(ProjectFullPath(MultiManifestPath)), "outputFolder", outputFolder);
                manifestPath = WriteTemporaryManifest(manifestJson);
                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);
                string clipFullPath = ProjectFullPath(clipPath);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.That(clip, Is.Not.Null);
                string originalHash = HashFile(clipFullPath);

                corruptClip(clip);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);

                AnimationClip reloadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.That(reloadedClip, Is.Not.Null);
                assertCorruptionPersisted(reloadedClip);
                string corruptedHash = HashFile(clipFullPath);
                Assert.That(corruptedHash, Is.Not.EqualTo(originalHash),
                    "The time corruption must be present in the serialized clip before validation.");

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), expectedMessage);
                Assert.That(HashFile(clipFullPath), Is.EqualTo(corruptedHash),
                    "Rejecting a corrupt existing clip must leave its serialized bytes unchanged.");
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                CleanupOutputFolder(outputFolder);
            }
        }

        [Test]
        public void ImageTraceability_InheritsFamilyDefaultsAndAcceptsUniqueImageResourceIdOverride()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string inheritedImage = BuildImageJson(tempSource, "BellatorTechnicalFixtureSecond.png",
                    "BellatorTechnicalFixtureSecond_0", 32);
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", inheritedImage));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureSecond.png")), Is.True,
                    "The second image omits metadata overrides and must inherit the family defaults.");
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
                Assert.That(atlas, Is.Not.Null);
                Assert.That(SpriteAtlasExtensions.GetPackables(atlas).Length, Is.EqualTo(2),
                    "The first image's resourceId override must differ from the second image's inherited family ID.");
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_ExplicitNullOverridesInheritFamilyDefaults()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureNullMetadata.png",
                    "BellatorTechnicalFixtureNullMetadata_0", 32);
                image = InsertJsonStringProperty(image, "resourceId", "f02-bellator-null-metadata-image");
                foreach (string field in new[]
                {
                    "status", "provenance", "referenceId", "referencePath",
                    "referenceSha256", "promptReference", "promptReferenceSha256"
                })
                    image = InsertJsonNullProperty(image, field);

                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureNullMetadata.png")), Is.True);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
                Assert.That(atlas, Is.Not.Null);
                Assert.That(SpriteAtlasExtensions.GetPackables(atlas).Length, Is.EqualTo(2));
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_NullResourceIdInheritsFamilyDefaultWithUniqueEffectiveIds()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string json = InsertImageJsonStringProperty(
                    File.ReadAllText(ProjectFullPath(ManifestPath)), "resourceId", "f02-existing-image-override");
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureNullResourceId.png",
                    "BellatorTechnicalFixtureNullResourceId_0", 32);
                image = InsertJsonNullProperty(image, "resourceId");
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureNullResourceId.png")), Is.True);
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [TestCase("resourceId", "17", "Number")]
        [TestCase("status", "true", "True")]
        [TestCase("provenance", "[]", "Array")]
        [TestCase("referenceId", "{}", "Object")]
        public void ImageTraceability_NonStringOverrideTypesAreRejectedWithoutAssetMutation(
            string field, string rawValue, string expectedType)
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureWrongType.png",
                    "BellatorTechnicalFixtureWrongType_0", 32);
                image = InsertJsonRawProperty(image, "\"" + field + "\"", rawValue);
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath),
                    "must be a JSON string or null: images[1]." + field + " (type=" + expectedType + ")");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_StringNullRemainsTextAndUsesExistingStatusValidation()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureStringNull.png",
                    "BellatorTechnicalFixtureStringNull_0", 32);
                image = InsertJsonStringProperty(image, "status", "null");
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "traceability metadata");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_WhitespaceOverrideIsRetainedAndRejectedWithoutAssetMutation()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureWhitespaceMetadata.png",
                    "BellatorTechnicalFixtureWhitespaceMetadata_0", 32);
                image = InsertJsonStringProperty(image, "provenance", " \t ");
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "traceability metadata");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_EscapedPropertyNameAndJsonLookingStringAreHandledAsTokens()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureEscapedMetadata.png",
                    "BellatorTechnicalFixtureEscapedMetadata_0", 32);
                image = InsertJsonRawProperty(image, "\"resource\\u0049d\"", "\"f02-bellator-escaped-metadata-image\"");
                image = InsertJsonStringProperty(image, "provenance", "Test fixture text: \"resourceId\": null");
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureEscapedMetadata.png")), Is.True);
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_DuplicateMetadataPropertyIsRejectedBeforeAssetMutation()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureDuplicateMetadata.png",
                    "BellatorTechnicalFixtureDuplicateMetadata_0", 32);
                image = InsertJsonRawProperty(image, "\"resourceId\"", "\"f02-bellator-first-image-id\"");
                image = InsertJsonRawProperty(image, "\"resourceId\"", "\"f02-bellator-second-image-id\"");
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "DuplicateMember");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [TestCase("null")]
        [TestCase("17")]
        [TestCase("[]")]
        public void ImageArrayNonObjectEntriesAreRejectedWithoutAssetMutation(string rawValue)
        {
            AssertOutputFolderAbsent();
            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", rawValue));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "images");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void FamilyRequiredMetadataMustBeJsonStrings()
        {
            AssertOutputFolderAbsent();
            string manifestPath = null;
            try
            {
                string json = ReplaceJsonPropertyWithRawValue(
                    File.ReadAllText(ProjectFullPath(ManifestPath)), "resourceId", "null");
                manifestPath = WriteTemporaryManifest(json);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath),
                    "family metadata field must be a JSON string: resourceId");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void FamilyManifestRootMustBeJsonObject()
        {
            AssertOutputFolderAbsent();
            string manifestPath = WriteTemporaryManifest("[]");
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "root must be an object");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void FamilyImagesPropertyMustBeJsonArray()
        {
            AssertOutputFolderAbsent();
            string manifestPath = null;
            try
            {
                string json = ReplaceJsonArrayPropertyWithRawValue(
                    File.ReadAllText(ProjectFullPath(ManifestPath)), "images", "null");
                manifestPath = WriteTemporaryManifest(json);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "images must be an array");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [TestCase("resourceId", "traceability metadata")]
        [TestCase("status", "traceability metadata")]
        [TestCase("provenance", "traceability metadata")]
        [TestCase("referenceId", "traceability metadata")]
        [TestCase("referencePath", "safe project-relative paths")]
        [TestCase("referenceSha256", "traceability metadata")]
        [TestCase("promptReference", "traceability metadata")]
        [TestCase("promptReferenceSha256", "traceability metadata")]
        public void ImageTraceability_EmptyOverrideDoesNotInheritFamilyDefault(string field, string expectedError)
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureEmptyMetadata.png",
                    "BellatorTechnicalFixtureEmptyMetadata_0", 32);
                image = InsertJsonStringProperty(image, field, string.Empty);
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), expectedError);
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageTraceability_AcceptsValidPerImageMetadataOverrides()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string tempReference = "Temp/F02ImageReference-" + Guid.NewGuid().ToString("N") + ".png";
            string tempPromptRecord = CopyPromptRecordForTest();
            string manifestPath = null;
            try
            {
                File.Copy(ProjectFullPath(ForeignSourcePath), ProjectFullPath(tempReference));
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureOverrides.png",
                    "BellatorTechnicalFixtureOverrides_0", 32);
                image = InsertJsonStringProperty(image, "resourceId", "f02-bellator-image-override");
                image = InsertJsonStringProperty(image, "status", "Final");
                image = InsertJsonStringProperty(image, "provenance", "Test-only image-level override for the F02 importer.");
                image = InsertJsonStringProperty(image, "referenceId", "ART-F01-MASTER-001");
                image = InsertJsonStringProperty(image, "referencePath", tempReference);
                image = InsertJsonStringProperty(image, "referenceSha256", HashFile(ProjectFullPath(tempReference)));
                image = InsertJsonStringProperty(image, "promptReference", tempPromptRecord);
                image = InsertJsonStringProperty(image, "promptReferenceSha256", HashFile(ProjectFullPath(tempPromptRecord)));

                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureOverrides.png")), Is.True);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
                Assert.That(atlas, Is.Not.Null);
                Assert.That(SpriteAtlasExtensions.GetPackables(atlas).Length, Is.EqualTo(2));
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                DeleteProjectFile(tempReference);
                DeleteProjectFile(tempPromptRecord);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void DuplicateEffectiveImageResourceId_IsRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string duplicateImage = InsertJsonStringProperty(
                    BuildImageJson(tempSource, "DuplicateResource.png", "DuplicateResource_0", 32),
                    "resourceId", FixtureResourceId);
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", duplicateImage));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "duplicate resource ID");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageResourceIdUniqueness_IsOrdinalAndCaseSensitive()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string image = BuildImageJson(tempSource, "BellatorTechnicalFixtureCaseDistinct.png",
                    "BellatorTechnicalFixtureCaseDistinct_0", 32);
                image = InsertJsonStringProperty(image, "resourceId", FixtureResourceId.ToUpperInvariant());
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));

                F02SpriteFamilyManifestImporter.ImportFamily(manifestPath);

                Assert.That(File.Exists(ProjectFullPath(OutputFolder + "/BellatorTechnicalFixtureCaseDistinct.png")), Is.True);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
                Assert.That(atlas, Is.Not.Null);
                Assert.That(SpriteAtlasExtensions.GetPackables(atlas).Length, Is.EqualTo(2),
                    "Resource IDs that differ only by case are distinct under the ordinal contract.");
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PromptReference_MissingVersionedRecordIsRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
            string missingRecord = "docs/art/records/F02MissingCreationRecord-" +
                Guid.NewGuid().ToString("N") + ".v1.json";
            Assert.That(File.Exists(ProjectFullPath(missingRecord)), Is.False,
                "The missing-record fixture must not reuse an existing project file.");
            string json = InsertImageJsonStringProperty(
                File.ReadAllText(ProjectFullPath(ManifestPath)),
                "promptReference", missingRecord);
            string manifestPath = WriteTemporaryManifest(json);
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "existing versioned creation record");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PromptReference_IncompleteRecordIsRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
            string recordId = "F02IncompleteCreationRecord-" + Guid.NewGuid().ToString("N");
            string promptRecord = WriteTemporaryTextFile(
                "docs/art/records/" + recordId + ".v1.json",
                "{\"schemaVersion\":1,\"recordId\":\"" + recordId + "\",\"recordVersion\":1," +
                "\"creationMethod\":\"generated\",\"generator\":\"test-generator\",\"literalPrompt\":\"\"," +
                "\"processing\":{\"tool\":\"test-tool\",\"version\":\"1\",\"procedure\":\"test\",\"parameters\":\"test\"}}");
            string json = InsertImageJsonStringProperty(File.ReadAllText(ProjectFullPath(ManifestPath)),
                "promptReference", promptRecord);
            json = InsertImageJsonStringProperty(json, "promptReferenceSha256", HashFile(ProjectFullPath(promptRecord)));
            string manifestPath = WriteTemporaryManifest(json);
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "versioned record with literal prompt");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(promptRecord);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PromptReference_RecordVersionMustMatchVersionedFileName()
        {
            AssertOutputFolderAbsent();
            string promptRecord = CopyPromptRecordForTest("docs/art/records/F02VersionMismatch-" +
                Guid.NewGuid().ToString("N") + ".v2.json");
            string json = InsertImageJsonStringProperty(File.ReadAllText(ProjectFullPath(ManifestPath)),
                "promptReference", promptRecord);
            json = InsertImageJsonStringProperty(json, "promptReferenceSha256", HashFile(ProjectFullPath(promptRecord)));
            string manifestPath = WriteTemporaryManifest(json);
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "versioned record with literal prompt");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(promptRecord);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PromptReference_RecordOutsideContractDirectoryIsRejected()
        {
            AssertOutputFolderAbsent();
            string promptRecord = CopyPromptRecordForTest("Temp/F02OutOfDirectory-" +
                Guid.NewGuid().ToString("N") + ".v1.json");
            string json = InsertImageJsonStringProperty(File.ReadAllText(ProjectFullPath(ManifestPath)),
                "promptReference", promptRecord);
            json = InsertImageJsonStringProperty(json, "promptReferenceSha256", HashFile(ProjectFullPath(promptRecord)));
            string manifestPath = WriteTemporaryManifest(json);
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "must live directly under docs/art/records");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(promptRecord);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PromptReference_ChangingPublishedVersionWithoutChangingManifestHashIsRejected()
        {
            AssertOutputFolderAbsent();
            string promptRecord = CopyPromptRecordForTest();
            string expectedHash = HashFile(ProjectFullPath(promptRecord));
            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            json = ReplaceJsonStringProperty(json, "promptReference", promptRecord);
            json = ReplaceJsonStringProperty(json, "promptReferenceSha256", expectedHash);
            string manifestPath = WriteTemporaryManifest(json);
            try
            {
                string originalRecord = File.ReadAllText(ProjectFullPath(promptRecord));
                File.WriteAllText(ProjectFullPath(promptRecord),
                    ReplaceJsonStringProperty(originalRecord, "literalPrompt", "tampered after publication"), new UTF8Encoding(false));
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "hash differs from its manifest declaration");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(promptRecord);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void SourceAndReferenceWithIdenticalContent_AreRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string sourceHash = HashFile(ProjectFullPath(tempSource));
            string image = BuildImageJson(tempSource, "BellatorIdenticalReference.png",
                "BellatorIdenticalReference_0", 32);
            // Models hard-link and short-name aliases without relying on host-specific link APIs.
            image = InsertJsonStringProperty(image, "referencePath", tempSource);
            image = InsertJsonStringProperty(image, "referenceSha256", sourceHash);
            string manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(
                File.ReadAllText(ProjectFullPath(ManifestPath)), "images", image));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "identical content hash");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void EffectiveImageReferenceSymlinkOutsideProject_IsRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
            string linkPath = "Temp/F02Reference-" + Guid.NewGuid().ToString("N") + ".png";
            string linkFullPath = ProjectFullPath(linkPath);
            string externalTarget = Path.Combine(Path.GetTempPath(), "F02ExternalReference-" + Guid.NewGuid().ToString("N") + ".png");
            string manifestPath = null;
            bool linkCreated = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(linkFullPath));
                File.Copy(ProjectFullPath(SourcePath), externalTarget);
                string targetHash = HashFile(externalTarget);
                CreateFileSymbolicLink(linkFullPath, externalTarget);
                linkCreated = true;

                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                manifestPath = WriteTemporaryManifest(
                    InsertImageJsonStringProperty(json, "referencePath", linkPath));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "filesystem link");
                Assert.That(HashFile(externalTarget), Is.EqualTo(targetHash),
                    "Rejecting the reference link must leave its external target untouched.");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                if (linkCreated && File.Exists(linkFullPath)) File.Delete(linkFullPath);
                if (File.Exists(externalTarget)) File.Delete(externalTarget);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageSourceUsedAsReferenceThroughDifferentPath_IsRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
#if UNITY_EDITOR_WIN
            string aliasPath = SourcePath.ToUpperInvariant();
            string expectedRejection = "cannot also be its effective reference";
#else
            string aliasPath = "Temp/F02SourceReferenceAlias-" + Guid.NewGuid().ToString("N") + ".png";
            string expectedRejection = "filesystem link";
#endif
            string aliasFullPath = ProjectFullPath(aliasPath);
            string sourceFullPath = ProjectFullPath(SourcePath);
            string sourceHash = HashFile(sourceFullPath);
            string manifestPath = null;
#if !UNITY_EDITOR_WIN
            bool linkCreated = false;
#endif
            try
            {
#if !UNITY_EDITOR_WIN
                Directory.CreateDirectory(Path.GetDirectoryName(aliasFullPath));
                CreateFileSymbolicLink(aliasFullPath, sourceFullPath);
                linkCreated = true;
#endif
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string imageWithAlias = InsertImageJsonStringProperty(json, "referencePath", aliasPath);
                imageWithAlias = InsertImageJsonStringProperty(imageWithAlias, "referenceSha256", sourceHash);
                manifestPath = WriteTemporaryManifest(imageWithAlias);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), expectedRejection);
                Assert.That(HashFile(sourceFullPath), Is.EqualTo(sourceHash),
                    "The source used through an alias must remain untouched.");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
#if !UNITY_EDITOR_WIN
                if (linkCreated && File.Exists(aliasFullPath)) File.Delete(aliasFullPath);
#endif
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ImageSourceUsedAsExactReference_IsRejectedBeforeChangingAssets()
        {
            AssertOutputFolderAbsent();
            string sourceFullPath = ProjectFullPath(SourcePath);
            string sourceHash = HashFile(sourceFullPath);
            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            string withReferencePath = InsertImageJsonStringProperty(json, "referencePath", SourcePath);
            string manifestPath = WriteTemporaryManifest(
                InsertImageJsonStringProperty(withReferencePath, "referenceSha256", sourceHash));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "cannot also be its effective reference");
                Assert.That(HashFile(sourceFullPath), Is.EqualTo(sourceHash));
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }
        [Test]
        public void ImportingFamily_DoesNotPersistAnUnrelatedDirtyAsset()
        {
            AssertOutputFolderAbsent();
            string unrelatedAtlasPath = "Assets/Luckbound/Tests/EditMode/F02UnrelatedDirty-" +
                Guid.NewGuid().ToString("N") + ".spriteatlas";
            AssertOutputFolderAbsent(unrelatedAtlasPath);
            string unrelatedFullPath = ProjectFullPath(unrelatedAtlasPath);

            SpriteAtlas unrelatedAtlas = new SpriteAtlas();
            bool ownsUnrelatedAtlas = false;
            try
            {
                AssetDatabase.CreateAsset(unrelatedAtlas, unrelatedAtlasPath);
                ownsUnrelatedAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(unrelatedAtlasPath) == unrelatedAtlas;
                Assert.That(ownsUnrelatedAtlas, Is.True,
                    "The test must verify the exact atlas asset it created before mutating it.");
                EditorUtility.SetDirty(unrelatedAtlas);
                AssetDatabase.SaveAssetIfDirty(unrelatedAtlas);
                string savedHash = HashFile(unrelatedFullPath);

                SpriteAtlasPackingSettings settings = SpriteAtlasExtensions.GetPackingSettings(unrelatedAtlas);
                int dirtyPadding = settings.padding == 13 ? 12 : 13;
                settings.padding = dirtyPadding;
                SpriteAtlasExtensions.SetPackingSettings(unrelatedAtlas, settings);
                EditorUtility.SetDirty(unrelatedAtlas);
                Assert.That(SpriteAtlasExtensions.GetPackingSettings(unrelatedAtlas).padding, Is.EqualTo(dirtyPadding));

                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);

                Assert.That(HashFile(unrelatedFullPath), Is.EqualTo(savedHash),
                    "Importing one family must not persist a different dirty asset.");
                Assert.That(SpriteAtlasExtensions.GetPackingSettings(unrelatedAtlas).padding, Is.EqualTo(dirtyPadding),
                    "The unrelated asset should remain dirty in memory after the family import.");

                AssetDatabase.SaveAssetIfDirty(unrelatedAtlas);
                Assert.That(HashFile(unrelatedFullPath), Is.Not.EqualTo(savedHash),
                    "The fixture must prove its dirty setting is serializable to disk.");
            }
            finally
            {
                if (ownsUnrelatedAtlas && AssetDatabase.LoadAssetAtPath<SpriteAtlas>(unrelatedAtlasPath) == unrelatedAtlas)
                    AssetDatabase.DeleteAsset(unrelatedAtlasPath);
                CleanupOutputFolder();
            }
        }

        [TestCase("BellatorTechnicalFixture.png")]
        [TestCase("BellatorTechnicalFixture.png.meta")]
        [TestCase("TechnicalHold.anim")]
        [TestCase("TechnicalHold.anim.meta")]
        [TestCase("bellator-technical-fixture.spriteatlas")]
        [TestCase("bellator-technical-fixture.spriteatlas.meta")]
        [TestCase("F02FamilyOwnership.json")]
        [TestCase("F02FamilyOwnership.json.meta")]
        [TestCase("../F02SpriteFamilyPipelineFixture.meta")]
        public void DestinationFileReparsePoint_IsRejectedBeforeChangingItsExternalTarget(string destinationName)
        {
            AssertOutputFolderAbsent();
            string outputFullPath = ProjectFullPath(OutputFolder);
            string destinationFullPath = Path.GetFullPath(Path.Combine(outputFullPath, destinationName));
            string externalTarget = Path.Combine(Path.GetTempPath(), "F02ExternalTarget-" + Guid.NewGuid().ToString("N") + ".bin");
            bool linkCreated = false;
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                Assert.That(File.Exists(destinationFullPath), Is.True,
                    "The fixture must exist before it is replaced with a link.");
                File.WriteAllText(externalTarget, "F02 external sentinel", Encoding.UTF8);
                string targetHash = HashFile(externalTarget);

                File.Delete(destinationFullPath);
                CreateFileSymbolicLink(destinationFullPath, externalTarget);
                linkCreated = true;

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), "filesystem link");
                Assert.That(HashFile(externalTarget), Is.EqualTo(targetHash),
                    "Preflight must not modify the target outside the project.");
            }
            finally
            {
                if (linkCreated && File.Exists(destinationFullPath)) File.Delete(destinationFullPath);
                if (File.Exists(externalTarget)) File.Delete(externalTarget);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void UndeclaredPng_IsReportedAndLeftUntouched()
        {
            string outputFullPath = ProjectFullPath(OutputFolder);
            AssertOutputFolderAbsent();
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                string extraPath = Path.Combine(outputFullPath, "NotInManifest.png");
                string sourcePath = ProjectFullPath(ForeignSourcePath);
                File.Copy(sourcePath, extraPath);
                AssetDatabase.ImportAsset(OutputFolder + "/NotInManifest.png", ImportAssetOptions.ForceSynchronousImport);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), "Undeclared output artifact");
                Assert.That(File.Exists(ProjectFullPath(TexturePath)), Is.True);
                Assert.That(File.Exists(ProjectFullPath(ClipPath)), Is.True);
                Assert.That(File.Exists(ProjectFullPath(AtlasPath)), Is.True);
            }
            finally
            {
                CleanupOutputFolder();
            }
        }

        [Test]
        public void PublishedFamily_RejectsMissingAtlasWhenItsMarkerEntryIsAlsoOmitted()
        {
            AssertOutputFolderAbsent();
            string atlasFullPath = ProjectFullPath(AtlasPath);
            string atlasMetaPath = atlasFullPath + ".meta";
            string markerFullPath = ProjectFullPath(OutputFolder + "/F02FamilyOwnership.json");
            bool autoRefreshDisallowed = false;
            try
            {
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshDisallowed = true;
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                Assert.That(File.Exists(atlasFullPath), Is.True);
                Assert.That(File.Exists(atlasMetaPath), Is.True);

                File.Delete(atlasFullPath);
                File.Delete(atlasMetaPath);
                RemoveOwnershipAssetRecordForTest(Path.GetFileName(AtlasPath));
                string markerAfterCorruption = HashFile(markerFullPath);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath),
                    "exactly match the manifest output inventory");

                Assert.That(File.Exists(atlasFullPath), Is.False);
                Assert.That(File.Exists(atlasMetaPath), Is.False);
                Assert.That(HashFile(markerFullPath), Is.EqualTo(markerAfterCorruption));
            }
            finally
            {
                try
                {
                    CleanupOutputFolder();
                }
                finally
                {
                    if (autoRefreshDisallowed) AssetDatabase.AllowAutoRefresh();
                }
            }
        }
        [Test]
        public void UnmarkedFolderWithDeclaredPng_IsRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets/Luckbound/Tests/EditMode",
                    "F02SpriteFamilyPipelineFixture"), Is.Not.Empty);
                File.Copy(ProjectFullPath(ForeignSourcePath), ProjectFullPath(TexturePath));
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
                string beforeHash = HashFile(ProjectFullPath(TexturePath));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), "no ownership marker");
                Assert.That(HashFile(ProjectFullPath(TexturePath)), Is.EqualTo(beforeHash));
            }
            finally
            {
                CleanupOutputFolder();
            }
        }

        [Test]
        public void DeclaredPngAddedOutsideOwnershipMarker_IsRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            string destination = ProjectFullPath(OutputFolder + "/Intruder.png");
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                File.Copy(ProjectFullPath(ForeignSourcePath), destination);
                AssetDatabase.ImportAsset(OutputFolder + "/Intruder.png", ImportAssetOptions.ForceSynchronousImport);

                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string image = BuildImageJson(tempSource, "Intruder.png", "Intruder_0", 32);
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", image));
                string beforeHash = HashFile(destination);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "without recorded family ownership");
                Assert.That(HashFile(destination), Is.EqualTo(beforeHash));
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ExistingAtlasWithForeignPackable_IsRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string foreignAssetPath = "Assets/Luckbound/Tests/EditMode/F02ForeignPackable-" +
                Guid.NewGuid().ToString("N") + ".png";
            string foreignFullPath = ProjectFullPath(foreignAssetPath);
            AssertOutputFolderAbsent(foreignAssetPath);
            bool ownsForeignAsset = false;
            try
            {
                F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath);
                File.Copy(ProjectFullPath(ForeignSourcePath), foreignFullPath);
                ownsForeignAsset = File.Exists(foreignFullPath);
                AssetDatabase.ImportAsset(foreignAssetPath, ImportAssetOptions.ForceSynchronousImport);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
                Texture2D foreign = AssetDatabase.LoadAssetAtPath<Texture2D>(foreignAssetPath);
                Assert.That(atlas, Is.Not.Null);
                Assert.That(foreign, Is.Not.Null);
                SpriteAtlasExtensions.Add(atlas, new UnityEngine.Object[] { foreign });
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssetIfDirty(atlas);

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), "foreign or case-aliased packable");
                Assert.That(SpriteAtlasExtensions.GetPackables(atlas).Any(item => item == foreign), Is.True);
            }
            finally
            {
                if (ownsForeignAsset && File.Exists(foreignFullPath)) AssetDatabase.DeleteAsset(foreignAssetPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void InvalidLaterPng_IsRejectedBeforeAnyFamilyOutputIsCreated()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string invalidImage = BuildImageJson(tempSource, "LateInvalid.png", "LateInvalid_0", 31);
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", invalidImage));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "dimensions or encoding");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ClipNamesThatDifferOnlyByCase_AreRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            string duplicateClip = BuildClipJson("technicalhold");
            string manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "clips", duplicateClip));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "case-insensitive names");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void DestinationPathsThatDifferOnlyByCase_AreRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            string duplicatePathImage = BuildImageJson(tempSource, "bellatortechnicalfixture.PNG",
                "CaseAlias_0", 32);
            string manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", duplicatePathImage));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "collide on Windows");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [Test]
        public void ExistingOutputFolderThatDiffersOnlyByCase_IsRejectedWithoutChangingAssets()
        {
            const string aliasFolder = "Assets/Luckbound/Tests/EditMode/f02spritefamilypipelinefixture";
            AssertOutputFolderAbsent();
            AssertOutputFolderAbsent(aliasFolder);
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets/Luckbound/Tests/EditMode",
                    "f02spritefamilypipelinefixture"), Is.Not.Empty);
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(ManifestPath), "differs only by case");
            }
            finally
            {
                CleanupOutputFolder(aliasFolder);
            }
        }

        [Test]
        public void InvalidOutputFolderSegment_IsRejectedBeforeCreatingAnyAssetParent()
        {
            const string invalidOutputFolder = "Assets/Luckbound/Tests/EditMode/F02RepairParent/NUL/Family";
            const string parentThatMustRemainAbsent = "Assets/Luckbound/Tests/EditMode/F02RepairParent";
            string parentFullPath = ProjectFullPath(parentThatMustRemainAbsent);
            Assert.That(Directory.Exists(parentFullPath), Is.False);
            Assert.That(File.Exists(parentFullPath), Is.False);
            Assert.That(File.Exists(parentFullPath + ".meta"), Is.False);

            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            string manifestPath = WriteTemporaryManifest(
                ReplaceJsonStringProperty(json, "outputFolder", invalidOutputFolder));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "invalid Windows path segment");
                Assert.That(Directory.Exists(parentFullPath), Is.False);
                Assert.That(File.Exists(parentFullPath), Is.False);
                Assert.That(File.Exists(parentFullPath + ".meta"), Is.False);
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                if (Directory.Exists(parentFullPath)) Directory.Delete(parentFullPath, true);
                if (File.Exists(parentFullPath + ".meta")) File.Delete(parentFullPath + ".meta");
            }
        }

        [TestCase("x", TestName = "SliceXBoundsThatWouldOverflowInt_AreRejectedWithoutChangingAssets")]
        [TestCase("y", TestName = "SliceYBoundsThatWouldOverflowInt_AreRejectedWithoutChangingAssets")]
        public void SliceBoundsThatWouldOverflowInt_AreRejectedWithoutChangingAssets(string axis)
        {
            AssertOutputFolderAbsent();
            string tempSource = CopySourceToTemp();
            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string overflowImage = BuildOverflowingSliceImageJson(tempSource, axis);
                manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", overflowImage));

                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "in-bounds rectangles");
                AssertOutputFolderAbsent();
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                DeleteProjectFile(tempSource);
                CleanupOutputFolder();
            }
        }

        [TestCase("Bad?", TestName = "QuestionMark_IsRejectedByWindowsNameContract")]
        [TestCase("Bad*", TestName = "Asterisk_IsRejectedByWindowsNameContract")]
        [TestCase("Bad|", TestName = "Pipe_IsRejectedByWindowsNameContract")]
        [TestCase("Bad:", TestName = "Colon_IsRejectedByWindowsNameContract")]
        [TestCase("Bad<", TestName = "LessThan_IsRejectedByWindowsNameContract")]
        [TestCase("Bad>", TestName = "GreaterThan_IsRejectedByWindowsNameContract")]
        [TestCase("Bad\"Name", TestName = "Quote_IsRejectedByWindowsNameContract")]
        [TestCase("Bad\u0001", TestName = "ControlCharacter_IsRejectedByWindowsNameContract")]
        [TestCase("Bad ", TestName = "TrailingSpace_IsRejectedByWindowsNameContract")]
        [TestCase("Bad.", TestName = "TrailingDot_IsRejectedByWindowsNameContract")]
        [TestCase("CON", TestName = "ReservedDosNameWithoutExtension_IsRejected")]
        [TestCase("CON.txt", TestName = "ReservedDosNameWithExtension_IsRejected")]
        public void InvalidWindowsOutputSegment_IsRejectedWithoutAssetsMutation(string invalidSegment)
        {
            const string outputParent = "Assets/Luckbound/Tests/EditMode/F02Repair3WindowsNames";
            string parentFullPath = ProjectFullPath(outputParent);
            Assert.That(Directory.Exists(parentFullPath), Is.False);
            Assert.That(File.Exists(parentFullPath), Is.False);
            Assert.That(File.Exists(parentFullPath + ".meta"), Is.False);

            string manifestPath = null;
            try
            {
                string json = File.ReadAllText(ProjectFullPath(ManifestPath));
                string invalidOutputFolder = outputParent + "/" + invalidSegment + "/Family";
                manifestPath = WriteTemporaryManifest(
                    ReplaceJsonStringProperty(json, "outputFolder", invalidOutputFolder));
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "invalid Windows path segment");
                Assert.That(Directory.Exists(parentFullPath), Is.False);
                Assert.That(File.Exists(parentFullPath), Is.False);
                Assert.That(File.Exists(parentFullPath + ".meta"), Is.False);
            }
            finally
            {
                if (manifestPath != null) DeleteProjectFile(manifestPath);
                if (Directory.Exists(parentFullPath)) Directory.Delete(parentFullPath, true);
                if (File.Exists(parentFullPath + ".meta")) File.Delete(parentFullPath + ".meta");
            }
        }

        [Test]
        public void SourceThatAliasesAnotherImageDestination_IsRejectedWithoutChangingAssets()
        {
            AssertOutputFolderAbsent();
            string json = File.ReadAllText(ProjectFullPath(ManifestPath));
            string aliasingImage = BuildImageJsonWithHash(TexturePath,
                HashFile(ProjectFullPath(SourcePath)), "Later.png", "Later_0", 32);
            string manifestPath = WriteTemporaryManifest(InsertIntoJsonArray(json, "images", aliasingImage));
            try
            {
                AssertRejectedWithoutAssetsMutation(() =>
                    F02SpriteFamilyManifestImporter.ImportFamily(manifestPath), "source aliases a planned family output");
                AssertOutputFolderAbsent();
            }
            finally
            {
                DeleteProjectFile(manifestPath);
                CleanupOutputFolder();
            }
        }

        private static void RemoveOwnershipAssetRecordForTest(string fileName)
        {
            string markerPath = ProjectFullPath(OutputFolder + "/F02FamilyOwnership.json");
            OwnershipMarkerFixture marker = JsonUtility.FromJson<OwnershipMarkerFixture>(
                File.ReadAllText(markerPath, Encoding.UTF8));
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.assets, Is.Not.Null);
            int countBefore = marker.assets.Length;
            marker.assets = marker.assets.Where(asset =>
                !string.Equals(asset.fileName, fileName, StringComparison.Ordinal)).ToArray();
            Assert.That(marker.assets.Length, Is.EqualTo(countBefore - 1),
                "The corruption fixture must remove exactly one declared ownership entry.");
            File.WriteAllText(markerPath, JsonUtility.ToJson(marker, true) + "\n", new UTF8Encoding(false));
        }
        private static void AssertRejectedWithoutAssetsMutation(System.Action action, string expectedMessage)
        {
            Dictionary<string, string> before = SnapshotAssetsTree();
            Exception error = Assert.Catch<Exception>(() => action());
            Assert.That(error, Is.Not.Null, "The import was expected to reject this fixture.");
            StringAssert.Contains(expectedMessage, error.Message);
            AssertFileSnapshotsEqual(before, SnapshotAssetsTree());
        }

        private static Dictionary<string, string> SnapshotAssetsTree()
        {
            string root = Path.GetFullPath(Application.dataPath);
            var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                result.Add("D/" + relative, string.Empty);
            }
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                result.Add("F/" + relative, HashFile(path));
            }
            return result;
        }

        private static void AssertOutputFolderAbsent()
        {
            AssertOutputFolderAbsent(OutputFolder);
        }

        private static void AssertOutputFolderAbsent(string outputFolder)
        {
            string outputFullPath = ProjectFullPath(outputFolder);
            Assert.That(Directory.Exists(outputFullPath), Is.False,
                "The F02 importer tests require their dedicated scratch output folder to be absent.");
            Assert.That(File.Exists(outputFullPath + ".meta"), Is.False,
                "The F02 importer tests require scratch folder metadata to be absent.");
            Assert.That(File.Exists(outputFullPath), Is.False,
                "The F02 importer tests require the scratch output path not to be occupied by a user file.");
        }

        private static void CleanupOutputFolder()
        {
            CleanupOutputFolder(OutputFolder);
        }

        private static void CleanupOutputFolder(string outputFolder)
        {
            string outputFullPath = ProjectFullPath(outputFolder);
            if (!Directory.Exists(outputFullPath)) return;
            AssetDatabase.DeleteAsset(outputFolder);
            if (Directory.Exists(outputFullPath)) Directory.Delete(outputFullPath, true);
            if (File.Exists(outputFullPath + ".meta")) File.Delete(outputFullPath + ".meta");
        }

        private static string CopySourceToTemp()
        {
            string relative = "Temp/F02Source-" + Guid.NewGuid().ToString("N") + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFullPath(relative)));
            File.Copy(ProjectFullPath(SourcePath), ProjectFullPath(relative));
            return relative;
        }

        private static string CopyModifiedSourceToTemp()
        {
            string relative = "Temp/F02SourceChanged-" + Guid.NewGuid().ToString("N") + ".png";
            string fullPath = ProjectFullPath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(ProjectFullPath(SourcePath)), false), Is.True);
                    Color32[] pixels = texture.GetPixels32();
                    pixels[0].r = (byte)(pixels[0].r ^ 0xff);
                    texture.SetPixels32(pixels);
                    texture.Apply(false, false);
                    File.WriteAllBytes(fullPath, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                Assert.That(HashFile(fullPath), Is.Not.EqualTo(HashFile(ProjectFullPath(SourcePath))),
                    "The changed PNG fixture must have different payload bytes.");
                return relative;
            }
            catch
            {
                if (File.Exists(fullPath)) DeleteProjectFile(relative);
                throw;
            }
        }

        private static string CopyPromptRecordForTest()
        {
            string recordId = "F02PromptRecord-" + Guid.NewGuid().ToString("N");
            return CopyPromptRecordForTest("docs/art/records/" + recordId + ".v1.json");
        }

        private static string CopyPromptRecordForTest(string relative)
        {
            string fileName = Path.GetFileName(relative);
            int versionMarker = fileName.LastIndexOf(".v", StringComparison.Ordinal);
            Assert.That(versionMarker, Is.GreaterThan(0), "The test record path needs an ID and versioned filename.");
            string recordId = fileName.Substring(0, versionMarker);
            string recordContents = File.ReadAllText(ProjectFullPath(PromptRecordPath));
            recordContents = ReplaceJsonStringProperty(recordContents, "recordId", recordId);
            string fullPath = ProjectFullPath(relative);
            Assert.That(File.Exists(fullPath), Is.False,
                "The test helper only writes its uniquely owned record path and must not overwrite existing data.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, recordContents, new UTF8Encoding(false));
            return relative;
        }

        private static string WriteTemporaryTextFile(string relative, string contents)
        {
            string fullPath = ProjectFullPath(relative);
            Assert.That(File.Exists(fullPath), Is.False,
                "The test helper must never overwrite an existing project file: " + relative);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, contents, new UTF8Encoding(false));
            return relative;
        }

        private static string WriteTemporaryManifest(string json)
        {
            string relative = "Temp/F02Manifest-" + Guid.NewGuid().ToString("N") + ".json";
            string fullPath = ProjectFullPath(relative);
            Assert.That(File.Exists(fullPath), Is.False,
                "The test helper must never overwrite an existing project file: " + relative);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, json);
            return relative;
        }

        private static void DeleteProjectFile(string relative)
        {
            string fullPath = ProjectFullPath(relative);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }

        private static void CreateFileSymbolicLink(string linkPath, string targetPath)
        {
#if UNITY_EDITOR_WIN
            const uint allowUnprivilegedCreate = 0x2;
            if (!CreateSymbolicLinkWindows(linkPath, targetPath, allowUnprivilegedCreate))
                Assert.Inconclusive("The host could not create the file symlink required by this regression; Win32 error " +
                    Marshal.GetLastWin32Error() + ".");
#else
            if (CreateSymbolicLinkUnix(targetPath, linkPath) != 0)
                Assert.Inconclusive("The host could not create the file symlink required by this regression; errno " +
                    Marshal.GetLastWin32Error() + ".");
#endif
        }

        private static string BuildOverflowingSliceImageJson(string sourcePath, string axis)
        {
            bool overflowX = axis == "x";
            return JsonUtility.ToJson(new TestImageSpec
            {
                sourcePath = sourcePath,
                assetFileName = "OverflowingSlice.png",
                sha256 = HashFile(ProjectFullPath(sourcePath)),
                width = 32,
                height = 48,
                bitDepth = 8,
                colorType = 6,
                slices = new[]
                {
                    new TestSliceSpec
                    {
                        name = "OverflowingSlice_0", x = overflowX ? int.MaxValue : 0,
                        y = overflowX ? 0 : int.MaxValue, width = overflowX ? 1 : 32,
                        height = overflowX ? 48 : 1,
                        pivotX = 0.5f, pivotY = 0f
                    }
                }
            }, true);
        }

        private static string BuildImageJson(string sourcePath, string assetFileName, string spriteName, int declaredWidth)
        {
            string hash = HashFile(ProjectFullPath(sourcePath));
            return BuildImageJsonWithHash(sourcePath, hash, assetFileName, spriteName, declaredWidth);
        }

        private static string BuildImageJsonWithHash(string sourcePath, string hash, string assetFileName,
            string spriteName, int declaredWidth)
        {
            var image = new TestImageSpec
            {
                sourcePath = sourcePath,
                assetFileName = assetFileName,
                sha256 = hash,
                width = declaredWidth,
                height = 48,
                bitDepth = 8,
                colorType = 6,
                slices = new[]
                {
                    new TestSliceSpec
                    {
                        name = spriteName, x = 0, y = 0, width = declaredWidth, height = 48,
                        pivotX = 0.5f, pivotY = 0f
                    }
                }
            };
            return JsonUtility.ToJson(image, true);
        }

        private static string ReplaceJsonStringProperty(string json, string propertyName, string value)
        {
            string key = "\"" + propertyName + "\"";
            int keyIndex = json.IndexOf(key, System.StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), "JSON property missing: " + propertyName);
            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            int start = json.IndexOf('"', colonIndex + 1);
            int end = json.IndexOf('"', start + 1);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return json.Substring(0, start + 1) + EscapeJsonString(value) + json.Substring(end);
        }

        private static string MoveRootStringPropertyToEnd(string json, string propertyName)
        {
            string key = "\"" + propertyName + "\"";
            int keyStart = json.IndexOf(key, StringComparison.Ordinal);
            Assert.That(keyStart, Is.GreaterThanOrEqualTo(0), "JSON property missing: " + propertyName);
            int colon = json.IndexOf(':', keyStart + key.Length);
            int valueStart = json.IndexOf('"', colon + 1);
            Assert.That(valueStart, Is.GreaterThan(colon));

            int valueEnd = valueStart + 1;
            while (valueEnd < json.Length)
            {
                if (json[valueEnd] == '"' && json[valueEnd - 1] != '\\') break;
                valueEnd++;
            }
            Assert.That(valueEnd, Is.LessThan(json.Length), "The selected root property must have a string value.");
            int comma = json.IndexOf(',', valueEnd + 1);
            Assert.That(comma, Is.GreaterThan(valueEnd), "The selected property must not already be last.");

            string property = json.Substring(keyStart, valueEnd - keyStart + 1);
            string withoutProperty = json.Remove(keyStart, comma - keyStart + 1);
            int rootEnd = withoutProperty.LastIndexOf('}');
            Assert.That(rootEnd, Is.GreaterThan(0), "Manifest root object is missing its closing brace.");
            string prefix = withoutProperty.Substring(0, rootEnd).TrimEnd();
            return prefix + ",\n  " + property + "\n" + withoutProperty.Substring(rootEnd);
        }

        private static string InsertImageJsonStringProperty(string json, string propertyName, string value)
        {
            int imagesStart = json.IndexOf("\"images\"", StringComparison.Ordinal);
            Assert.That(imagesStart, Is.GreaterThanOrEqualTo(0), "The JSON images array is missing.");
            int imageStart = json.IndexOf('{', imagesStart);
            Assert.That(imageStart, Is.GreaterThanOrEqualTo(0), "The JSON images array has no image object.");
            string property = "\n      \"" + propertyName + "\": \"" + EscapeJsonString(value) + "\",";
            return json.Insert(imageStart + 1, property);
        }
        private static string InsertJsonStringProperty(string json, string propertyName, string value)
        {
            int objectStart = json.IndexOf('{');
            Assert.That(objectStart, Is.GreaterThanOrEqualTo(0), "JSON object is missing.");
            string property = "\n  \"" + propertyName + "\": \"" + EscapeJsonString(value) + "\",";
            return json.Insert(objectStart + 1, property);
        }

        private static string InsertJsonRawProperty(string json, string rawPropertyName, string rawValue)
        {
            int objectStart = json.IndexOf('{');
            Assert.That(objectStart, Is.GreaterThanOrEqualTo(0), "JSON object is missing.");
            return json.Insert(objectStart + 1, "\n  " + rawPropertyName + ": " + rawValue + ",");
        }

        private static string ReplaceJsonPropertyWithRawValue(string json, string propertyName, string rawValue)
        {
            string key = "\"" + propertyName + "\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), "JSON property missing: " + propertyName);
            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            Assert.That(colonIndex, Is.GreaterThan(keyIndex));
            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;
            Assert.That(valueStart, Is.LessThan(json.Length));
            Assert.That(json[valueStart], Is.EqualTo('"'), "The selected fixture property must be a JSON string.");
            int valueEnd = valueStart + 1;
            while (valueEnd < json.Length)
            {
                if (json[valueEnd] == '"' && json[valueEnd - 1] != '\\') break;
                valueEnd++;
            }
            Assert.That(valueEnd, Is.LessThan(json.Length), "The selected JSON string must be closed.");
            return json.Substring(0, valueStart) + rawValue + json.Substring(valueEnd + 1);
        }

        private static string ReplaceJsonArrayPropertyWithRawValue(string json, string propertyName, string rawValue)
        {
            string key = "\"" + propertyName + "\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), "JSON property missing: " + propertyName);
            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            Assert.That(colonIndex, Is.GreaterThan(keyIndex));
            int valueStart = json.IndexOf('[', colonIndex + 1);
            Assert.That(valueStart, Is.GreaterThan(colonIndex), "The selected fixture property must be an array.");

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int valueEnd = -1;
            for (int index = valueStart; index < json.Length; index++)
            {
                char current = json[index];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (current == '\\') escaped = true;
                    else if (current == '"') inString = false;
                    continue;
                }

                if (current == '"') inString = true;
                else if (current == '[' || current == '{') depth++;
                else if (current == ']' || current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        valueEnd = index;
                        break;
                    }
                }
            }

            Assert.That(valueEnd, Is.GreaterThan(valueStart), "The selected JSON array must be closed.");
            return json.Substring(0, valueStart) + rawValue + json.Substring(valueEnd + 1);
        }

        private static string InsertJsonNullProperty(string json, string propertyName)
        {
            int objectStart = json.IndexOf('{');
            Assert.That(objectStart, Is.GreaterThanOrEqualTo(0), "JSON object is missing.");
            string property = "\n  \"" + propertyName + "\": null,";
            return json.Insert(objectStart + 1, property);
        }

        private static string EscapeJsonString(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (char.IsControl(character))
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            return builder.ToString();
        }

        private static string BuildClipJson(string name)
        {
            return JsonUtility.ToJson(new TestClipSpec
            {
                name = name,
                framesPerSecond = 12,
                loop = false,
                frames = new[] { SpriteName }
            }, true);
        }

        private static string InsertIntoJsonArray(string json, string propertyName, string itemJson)
        {
            string key = "\"" + propertyName + "\"";
            int keyIndex = json.IndexOf(key, System.StringComparison.Ordinal);
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0), "JSON array property missing: " + propertyName);
            int start = json.IndexOf('[', keyIndex);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            int end = -1;
            for (int i = start; i < json.Length; i++)
            {
                char character = json[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"') inString = true;
                else if (character == '[') depth++;
                else if (character == ']' && --depth == 0) { end = i; break; }
            }
            Assert.That(end, Is.GreaterThan(start), "JSON array did not terminate: " + propertyName);
            string current = json.Substring(start + 1, end - start - 1).Trim();
            string insertion = (current.Length == 0 ? string.Empty : ",") + "\n" + itemJson + "\n";
            return json.Insert(end, insertion);
        }

        [Serializable]
        private sealed class TestImageSpec
        {
            public string sourcePath;
            public string assetFileName;
            public string sha256;
            public int width;
            public int height;
            public int bitDepth;
            public int colorType;
            public TestSliceSpec[] slices;
        }

        [Serializable]
        private sealed class TestSliceSpec
        {
            public string name;
            public int x;
            public int y;
            public int width;
            public int height;
            public float pivotX;
            public float pivotY;
        }

        [Serializable]
        private sealed class TestClipSpec
        {
            public string name;
            public int framesPerSecond;
            public bool loop;
            public string[] frames;
        }

        private static MultiFamilyIdentity CaptureMultiFamilyIdentity(string outputFolder)
        {
            string texturePathA = outputFolder + "/BellatorFramesA.png";
            string texturePathB = outputFolder + "/BellatorFramesB.png";
            string atlasPath = outputFolder + "/f02-bellator-cross-image-fixture.spriteatlas";
            string[] texturePaths = { texturePathA, texturePathB };
            Texture2D[] textures = texturePaths.Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path)).ToArray();
            Assert.That(textures.All(texture => texture != null), Is.True);

            var identities = new List<string>();
            foreach (Texture2D texture in textures)
                identities.Add("texture:" + AssetDatabase.GetAssetPath(texture) + ":" + StableIdentity(texture));

            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var geometry = new List<string>();
            foreach (string texturePath in texturePaths)
            {
                Sprite[] importedSprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
                    .OfType<Sprite>()
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(importedSprites.Length, Is.EqualTo(2), texturePath);

                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, texturePath);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16f));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

                var importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);
                Assert.That(importerSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(importerSettings.spriteGenerateFallbackPhysicsShape, Is.False);

                var factories = new SpriteDataProviderFactories();
                factories.Init();
                ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                Assert.That(provider, Is.Not.Null);
                provider.InitSpriteEditorDataProvider();
                SpriteRect[] rects = provider.GetSpriteRects();
                Assert.That(rects.Length, Is.EqualTo(2));

                foreach (Sprite sprite in importedSprites)
                {
                    Assert.That(sprites.ContainsKey(sprite.name), Is.False, "Slice names must be unique across the family.");
                    sprites.Add(sprite.name, sprite);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f), sprite.name);
                    Assert.That(sprite.GetPhysicsShapeCount(), Is.Zero, sprite.name);
                    identities.Add("sprite:" + sprite.name + ":" + StableIdentity(sprite));
                    SpriteRect rect = rects.Single(item => item.name == sprite.name);
                    Rect expectedRect = sprite.name.Contains("Idle")
                        ? new Rect(0, 0, 32, 48)
                        : new Rect(0, 48, 32, 48);
                    float expectedPivotY = sprite.name.Contains("Idle") ? 0f : 0.125f;
                    Assert.That(rect.rect, Is.EqualTo(expectedRect), sprite.name);
                    Assert.That(rect.alignment, Is.EqualTo(SpriteAlignment.Custom), sprite.name);
                    Assert.That(rect.pivot.x, Is.EqualTo(0.5f).Within(0.0001f), sprite.name);
                    Assert.That(rect.pivot.y, Is.EqualTo(expectedPivotY).Within(0.0001f), sprite.name);
                    Assert.That(rect.border, Is.EqualTo(Vector4.zero), sprite.name);
                    geometry.Add(sprite.name + ":" + rect.spriteID + ":" + rect.rect + ":" + rect.pivot + ":" + importer.spritePixelsPerUnit +
                        ":" + importer.filterMode + ":" + importer.mipmapEnabled + ":" + sprite.GetPhysicsShapeCount());
                }
            }

            Assert.That(sprites.Keys, Is.EquivalentTo(new[]
            {
                "BellatorIdle_A", "BellatorIdle_B", "BellatorAttack_A", "BellatorAttack_B"
            }));

            string[] idleFrames = { "BellatorIdle_A", "BellatorIdle_B", "BellatorIdle_A" };
            string[] attackFrames = { "BellatorAttack_B", "BellatorAttack_A", "BellatorAttack_B" };
            var clipCurves = new List<string>();
            CaptureMultiClip(outputFolder + "/BellatorIdle.anim", idleFrames, true, texturePaths, sprites,
                identities, clipCurves);
            CaptureMultiClip(outputFolder + "/BellatorAttack.anim", attackFrames, false, texturePaths, sprites,
                identities, clipCurves);

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            Assert.That(atlas, Is.Not.Null);
            identities.Add("atlas:" + atlasPath + ":" + StableIdentity(atlas));
            UnityEngine.Object[] packables = SpriteAtlasExtensions.GetPackables(atlas);
            Assert.That(packables.Length, Is.EqualTo(2));
            string[] packableIdentities = packables.Select(StableIdentity).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEquivalent(textures.Select(StableIdentity), packableIdentities);
            var packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            var atlasTextureSettings = SpriteAtlasExtensions.GetTextureSettings(atlas);
            Assert.That(SpriteAtlasExtensions.IsIncludeInBuild(atlas), Is.False);
            Assert.That(atlasTextureSettings.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(atlasTextureSettings.generateMipMaps, Is.False);
            Assert.That(packing.padding, Is.EqualTo(2));
            Assert.That(packing.enableRotation, Is.False);
            Assert.That(packing.enableTightPacking, Is.False);

            string outputFullPath = ProjectFullPath(outputFolder);
            Assert.That(Directory.GetFiles(outputFullPath, "*.png", SearchOption.TopDirectoryOnly).Length, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(outputFullPath, "*.anim", SearchOption.TopDirectoryOnly).Length, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(outputFullPath, "*.spriteatlas", SearchOption.TopDirectoryOnly).Length, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(outputFullPath, "F02FamilyOwnership.json")), Is.True);

            return new MultiFamilyIdentity
            {
                ObjectIdentities = identities.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Geometry = geometry.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ClipCurves = clipCurves.ToArray(),
                AtlasPackables = packableIdentities
            };
        }

        private static void CaptureMultiClip(string clipPath, string[] frames, bool loop, string[] texturePaths,
            IReadOnlyDictionary<string, Sprite> sprites, ICollection<string> identities, ICollection<string> clipCurves)
        {
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip, Is.Not.Null, clipPath);
            RecordClipObservation(clip, "reloaded-clip");
            Assert.That(clip.frameRate, Is.EqualTo(12f));
            Assert.That(clip.length, Is.EqualTo((float)frames.Length / 12f).Within(0.0001f), clipPath);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            AssertFiniteAndApproximately(clipSettings.startTime, 0f, clipPath + " start time");
            AssertFiniteAndApproximately(clipSettings.stopTime, (float)frames.Length / clip.frameRate,
                clipPath + " duration boundary");
            Assert.That(clipSettings.loopTime, Is.EqualTo(loop));
            identities.Add("clip:" + clipPath + ":" + StableIdentity(clip));

            EditorCurveBinding expectedBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(objectBindings.Length, Is.EqualTo(1), clipPath);
            Assert.That(objectBindings[0].path, Is.EqualTo(expectedBinding.path), clipPath);
            Assert.That(objectBindings[0].type, Is.EqualTo(expectedBinding.type), clipPath);
            Assert.That(objectBindings[0].propertyName, Is.EqualTo(expectedBinding.propertyName), clipPath);
            Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, clipPath);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty, clipPath);

            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, expectedBinding);
            Assert.That(keys, Is.Not.Null);
            Assert.That(keys.Length, Is.EqualTo(frames.Length), clipPath);
            var referencedTexturePaths = new HashSet<string>(StringComparer.Ordinal);
            var curve = new List<string>
            {
                clipPath,
                clip.frameRate.ToString(CultureInfo.InvariantCulture),
                clip.length.ToString(CultureInfo.InvariantCulture),
                loop.ToString()
            };
            for (int index = 0; index < frames.Length; index++)
            {
                Assert.That(sprites.ContainsKey(frames[index]), Is.True, frames[index]);
                Assert.That(keys[index].time, Is.EqualTo((float)index / 12f).Within(0.0001f), clipPath);
                Assert.That(keys[index].value, Is.EqualTo(sprites[frames[index]]), clipPath);
                string framePath = AssetDatabase.GetAssetPath(keys[index].value);
                Assert.That(texturePaths.Contains(framePath), Is.True, framePath);
                referencedTexturePaths.Add(framePath);
                curve.Add(index + ":" + StableIdentity(keys[index].value));
            }

            Assert.That(keys[keys.Length - 1].time, Is.EqualTo((float)(frames.Length - 1) / 12f).Within(0.0001f), clipPath);
            Assert.That(referencedTexturePaths.Count, Is.EqualTo(2), clipPath + " must reference sprites from both source images.");
            clipCurves.Add(string.Join("|", curve));
        }

        private static void AssertClipAnimatorPlayback(
            string clipPath, bool loop, string[] frames, IReadOnlyDictionary<string, Sprite> sprites,
            AnimatorController controller, AnimatorState state, Animator animator, SpriteRenderer renderer)
        {
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip, Is.Not.Null, clipPath);
            RecordClipObservation(clip, "reloaded-clip");
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(loop), clipPath);
            state.motion = clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            animator.runtimeAnimatorController = null;
            animator.runtimeAnimatorController = controller;
            animator.Rebind();

            float frameDuration = 1f / clip.frameRate;
            float expectedDuration = (float)frames.Length / clip.frameRate;
            AssertFiniteAndApproximately(clip.length, expectedDuration,
                clipPath + " AnimationClip.length before Animator clock samples");
            float durationBoundary = clip.length;
            AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[0]], 0f, clipPath);
            for (int index = 0; index < frames.Length; index++)
                AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[index]],
                    (index + 0.5f) * frameDuration, clipPath);
            for (int boundaryIndex = 1; boundaryIndex < frames.Length; boundaryIndex++)
            {
                float boundary = boundaryIndex * frameDuration;
                AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[boundaryIndex - 1]],
                    boundary - 0.25f * frameDuration, clipPath);
                AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[boundaryIndex]],
                    boundary, clipPath);
                AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[boundaryIndex]],
                    boundary + 0.25f * frameDuration, clipPath);
            }

            Sprite expectedAtDurationBoundary = loop ? sprites[frames[0]] : sprites[frames[frames.Length - 1]];
            AssertAnimatorSpriteAtTime(animator, state, renderer, sprites[frames[frames.Length - 1]],
                durationBoundary - 0.25f * frameDuration, clipPath);
            AssertAnimatorSpriteAtTime(animator, state, renderer, expectedAtDurationBoundary,
                durationBoundary, clipPath);
            AssertAnimatorSpriteAtTime(animator, state, renderer, expectedAtDurationBoundary,
                durationBoundary + 0.25f * frameDuration, clipPath);
        }

        private static void AssertAnimatorSpriteAtTime(
            Animator animator, AnimatorState state, SpriteRenderer renderer, Sprite expected,
            float elapsedSeconds, string clipPath)
        {
            animator.Rebind();
            animator.Play(Animator.StringToHash("Base Layer." + state.name), 0, 0f);
            animator.Update(0f);
            animator.Update(elapsedSeconds);
            AnimatorStateInfo observedState = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log("F02_ANIMATOR_OBSERVATION " + JsonUtility.ToJson(new AnimatorObservation
            {
                unityVersion = Application.unityVersion,
                commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
                clipPath = clipPath,
                elapsedSeconds = elapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
                normalizedTime = observedState.normalizedTime.ToString("R", CultureInfo.InvariantCulture),
                stateLength = observedState.length.ToString("R", CultureInfo.InvariantCulture),
                actualSprite = renderer.sprite == null ? "<null>" : renderer.sprite.name,
                expectedSprite = expected == null ? "<null>" : expected.name
            }));
            Assert.That(renderer.sprite, Is.EqualTo(expected),
                clipPath + " Animator sprite at elapsed seconds " + elapsedSeconds.ToString("R", CultureInfo.InvariantCulture));
        }

        [Serializable]
        private sealed class ClipObservation
        {
            public string phase, unityVersion, commit, clipPath, length, framesPerSecond, startTime, stopTime;
            public bool loopTime;
            public string[] keyTimes, spriteNames;
        }

        [Serializable]
        private sealed class AnimatorObservation
        {
            public string unityVersion, commit, clipPath, elapsedSeconds, normalizedTime, stateLength;
            public string actualSprite, expectedSprite;
        }

        private static void RecordClipObservation(AnimationClip clip, string phase)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding) ??
                Array.Empty<ObjectReferenceKeyframe>();
            Debug.Log("F02_CLIP_OBSERVATION " + JsonUtility.ToJson(new ClipObservation
            {
                phase = phase,
                unityVersion = Application.unityVersion,
                commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
                clipPath = AssetDatabase.GetAssetPath(clip),
                length = clip.length.ToString("R", CultureInfo.InvariantCulture),
                framesPerSecond = clip.frameRate.ToString("R", CultureInfo.InvariantCulture),
                startTime = settings.startTime.ToString("R", CultureInfo.InvariantCulture),
                stopTime = settings.stopTime.ToString("R", CultureInfo.InvariantCulture),
                loopTime = settings.loopTime,
                keyTimes = keys.Select(key => key.time.ToString("R", CultureInfo.InvariantCulture)).ToArray(),
                spriteNames = keys.Select(key => key.value == null ? "<null>" : key.value.name).ToArray()
            }));
        }

        private static string StableIdentity(UnityEngine.Object asset)
        {
            Assert.That(asset, Is.Not.Null);
            string guid;
            long localId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId), Is.True,
                "Every imported family object must have a stable GUID and local file ID.");
            Assert.That(string.IsNullOrEmpty(guid), Is.False);
            Assert.That(localId, Is.Not.EqualTo(0));
            return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        }

        private static void AssertFiniteAndApproximately(float actual, float expected, string message)
        {
            Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False, message);
            Assert.That(Mathf.Approximately(actual, expected), Is.True, message);
        }

        private static void AssertPersistedTimeIsOutsidePreviousApproximation(
            float actual, float corruptedValue, float expectedValue)
        {
            Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False,
                "The persisted corruption must remain a finite time value.");
            Assert.That(actual, Is.EqualTo(corruptedValue),
                "The time corruption must persist unchanged after AssetDatabase reload.");
            Assert.That(Mathf.Approximately(actual, expectedValue), Is.False,
                "A persisted 0.005-frame offset must be rejected by the previous Mathf.Approximately contract.");
        }

        private static FamilyIdentity CaptureIdentity()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().Single(item => item.name == SpriteName);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            Assert.That(texture, Is.Not.Null);
            Assert.That(clip, Is.Not.Null);
            RecordClipObservation(clip, "single-reload");
            Assert.That(atlas, Is.Not.Null);

            string textureGuid;
            long textureLocalId;
            string spriteGuid;
            long spriteLocalId;
            string clipGuid;
            long clipLocalId;
            string atlasGuid;
            long atlasLocalId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out textureGuid, out textureLocalId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out spriteGuid, out spriteLocalId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out clipGuid, out clipLocalId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(atlas, out atlasGuid, out atlasLocalId), Is.True);

            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            var importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            Assert.That(provider, Is.Not.Null);
            provider.InitSpriteEditorDataProvider();
            SpriteRect spriteRect = provider.GetSpriteRects().Single();
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(
                clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            Assert.That(keys, Is.Not.Null);
            Assert.That(keys.Length, Is.EqualTo(1));
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            AssertFiniteAndApproximately(clipSettings.startTime, 0f, "A one-frame clip must retain its start time after reload.");
            AssertFiniteAndApproximately(clipSettings.stopTime, 1f / clip.frameRate,
                "A one-frame clip must retain an explicit one-frame stop time after AssetDatabase reload.");
            AssertFiniteAndApproximately(keys[0].time, 0f, "The first PPtr key must remain at the start after reload.");
            AssertFiniteAndApproximately(clip.length, 1f / clip.frameRate,
                "A one-frame clip must retain one-frame AnimationClip.length after AssetDatabase reload.");
            foreach (ObjectReferenceKeyframe key in keys) Assert.That(key.value, Is.EqualTo(sprite));

            UnityEngine.Object[] packables = SpriteAtlasExtensions.GetPackables(atlas);
            Assert.That(packables.Length, Is.EqualTo(1));
            Assert.That(packables[0], Is.EqualTo(texture));
            string[] packableGuids = new string[packables.Length];
            long[] packableLocalIds = new long[packables.Length];
            for (int i = 0; i < packables.Length; i++)
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(packables[i],
                    out packableGuids[i], out packableLocalIds[i]), Is.True);

            var packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            var atlasTexture = SpriteAtlasExtensions.GetTextureSettings(atlas);
            return new FamilyIdentity
            {
                TextureGuid = textureGuid,
                SpriteGuid = spriteGuid,
                SpriteLocalId = spriteLocalId,
                ClipGuid = clipGuid,
                ClipLocalId = clipLocalId,
                AtlasGuid = atlasGuid,
                AtlasLocalId = atlasLocalId,
                ClipSpriteGuids = new string[keys.Length],
                ClipSpriteLocalIds = new long[keys.Length],
                AtlasPackableGuids = packableGuids,
                AtlasPackableLocalIds = packableLocalIds,
                FilterMode = importer.filterMode,
                MipmapsEnabled = importer.mipmapEnabled,
                PixelsPerUnit = importer.spritePixelsPerUnit,
                TextureCompression = importer.textureCompression,
                FallbackPhysicsShape = importerSettings.spriteGenerateFallbackPhysicsShape,
                PhysicsShapeCount = sprite.GetPhysicsShapeCount(),
                SpriteRect = spriteRect.rect,
                SpritePivot = spriteRect.pivot,
                ClipKeyCount = keys.Length,
                AtlasIncludeInBuild = SpriteAtlasExtensions.IsIncludeInBuild(atlas),
                AtlasFilterMode = atlasTexture.filterMode,
                AtlasMipmapsEnabled = atlasTexture.generateMipMaps,
                AtlasPadding = packing.padding,
                AtlasAllowsRotation = packing.enableRotation,
                AtlasTightPacking = packing.enableTightPacking
            }.CaptureKeyReferences(keys);
        }

        private static Dictionary<string, string> SnapshotFamilyFiles()
        {
            return SnapshotFamilyFiles(OutputFolder);
        }

        private static Dictionary<string, string> SnapshotFamilyFiles(string outputFolder)
        {
            string root = ProjectFullPath(outputFolder);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToList();
            if (File.Exists(root + ".meta")) files.Add(root + ".meta");
            return HashFiles(files, root);
        }

        private static Dictionary<string, string> SnapshotOutsideFamily()
        {
            return SnapshotOutsideFamily(OutputFolder);
        }

        private static Dictionary<string, string> SnapshotOutsideFamily(string outputFolder)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string familyRoot = Path.GetFullPath(ProjectFullPath(outputFolder));
            string familyMeta = familyRoot + ".meta";
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories))
                if (!IsInside(familyRoot, path) && !string.Equals(path, familyMeta, StringComparison.OrdinalIgnoreCase))
                    files.Add(path);
            return HashFiles(files, assetsRoot);
        }

        private static Dictionary<string, string> HashFiles(IEnumerable<string> files, string root)
        {
            var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string path in files.OrderBy(item => item, System.StringComparer.OrdinalIgnoreCase))
            {
                string relative = path.StartsWith(root, System.StringComparison.OrdinalIgnoreCase)
                    ? path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar)
                    : path;
                result.Add(relative, HashFile(path));
            }
            return result;
        }

        private static void AssertFileSnapshotsEqual(IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (string key in expected.Keys) Assert.That(actual[key], Is.EqualTo(expected[key]), key);
        }

        private static bool IsInside(string root, string path)
        {
            string normalized = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(normalized, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ProjectFullPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private sealed class MultiFamilyIdentity
        {
            public string[] ObjectIdentities;
            public string[] Geometry;
            public string[] ClipCurves;
            public string[] AtlasPackables;
        }

        private sealed class FamilyIdentity
        {
            public string TextureGuid;
            public string SpriteGuid;
            public long SpriteLocalId;
            public string ClipGuid;
            public long ClipLocalId;
            public string AtlasGuid;
            public long AtlasLocalId;
            public string[] ClipSpriteGuids;
            public long[] ClipSpriteLocalIds;
            public string[] AtlasPackableGuids;
            public long[] AtlasPackableLocalIds;
            public FilterMode FilterMode;
            public bool MipmapsEnabled;
            public float PixelsPerUnit;
            public TextureImporterCompression TextureCompression;
            public bool FallbackPhysicsShape;
            public int PhysicsShapeCount;
            public Rect SpriteRect;
            public Vector2 SpritePivot;
            public int ClipKeyCount;
            public bool AtlasIncludeInBuild;
            public FilterMode AtlasFilterMode;
            public bool AtlasMipmapsEnabled;
            public int AtlasPadding;
            public bool AtlasAllowsRotation;
            public bool AtlasTightPacking;

            public FamilyIdentity CaptureKeyReferences(ObjectReferenceKeyframe[] keys)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    string guid;
                    long localId;
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(keys[i].value, out guid, out localId), Is.True);
                    ClipSpriteGuids[i] = guid;
                    ClipSpriteLocalIds[i] = localId;
                }
                return this;
            }
        }
    }
}
