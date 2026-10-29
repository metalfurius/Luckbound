using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Luckbound.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Luckbound.Tests.Editor
{
    public sealed class F02SpriteFamilyImporterTests
    {
#if UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
            EntryPoint = "CreateSymbolicLinkW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectorySymbolicLinkWindows(string symbolicLinkPath, string targetPath, uint flags);
#else
        [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true,
            EntryPoint = "symlink", ExactSpelling = true)]
        private static extern int CreateDirectorySymbolicLinkUnix(string targetPath, string symbolicLinkPath);
#endif

        [Test]
        public void FixtureOutputDirectoryReparsePoint_IsRejectedBeforeImportCallbackOrExternalWrite()
        {
            string probeName = "F02FixtureReparseProbe-" + Guid.NewGuid().ToString("N");
            string probeDirectory = F02SpriteFamilyImporter.FixtureFolder + "/" + probeName;
            string probeLink = ProjectFullPath(probeDirectory);
            string externalDirectory = Path.Combine(Path.GetTempPath(), probeName);
            string sentinelPath = Path.Combine(externalDirectory, "sentinel.bin");
            byte[] sentinel = { 0x46, 0x30, 0x32, 0x2D, 0x63, 0x61, 0x6E, 0x61, 0x72, 0x79 };
            bool linkCreated = false;
            bool autoRefreshDisallowed = false;

            Assert.That(Directory.Exists(probeLink) || File.Exists(probeLink), Is.False,
                "The unique probe output directory must not already exist.");
            Assert.That(Directory.Exists(externalDirectory) || File.Exists(externalDirectory), Is.False,
                "The unique external target directory must not already exist.");
            Assert.That(IsInside(
                    Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName),
                    Path.GetFullPath(externalDirectory)),
                Is.False, "The output link target must be outside the Unity project.");

            try
            {
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshDisallowed = true;
                Directory.CreateDirectory(externalDirectory);
                File.WriteAllBytes(sentinelPath, sentinel);
                string sentinelHash = HashFile(sentinelPath);
                CreateDirectorySymbolicLink(probeLink, externalDirectory);
                linkCreated = true;

                MethodInfo guardedImport = typeof(F02SpriteFamilyImporter).GetMethod(
                    "ExecuteWithSafeOutputDirectory", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(guardedImport, Is.Not.Null,
                    "ImportFixture must use the guarded callback seam before its file and AssetDatabase operations.");

                int callbackCount = 0;
                TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() =>
                    guardedImport.Invoke(null, new object[]
                    {
                        probeLink,
                        new Action(() => callbackCount++)
                    }));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidDataException>());
                Assert.That(callbackCount, Is.Zero,
                    "A rejected output directory must not enter the fixture import callback.");
                Assert.That(HashFile(sentinelPath), Is.EqualTo(sentinelHash),
                    "Rejecting the output directory link must leave the external canary bytes unchanged.");
                CollectionAssert.AreEquivalent(new[] { "sentinel.bin" },
                    Directory.GetFileSystemEntries(externalDirectory).Select(Path.GetFileName).ToArray(),
                    "The rejected import callback must not add files to the external target.");
            }
            finally
            {
                try
                {
                    if (linkCreated && IsReparsePoint(probeLink))
                        Directory.Delete(probeLink, false);

                    if (Directory.Exists(externalDirectory) && !IsReparsePoint(externalDirectory))
                    {
                        if (File.Exists(sentinelPath) && !IsReparsePoint(sentinelPath))
                            File.Delete(sentinelPath);
                        if (!Directory.GetFileSystemEntries(externalDirectory).Any())
                            Directory.Delete(externalDirectory, false);
                    }
                }
                finally
                {
                    if (autoRefreshDisallowed) AssetDatabase.AllowAutoRefresh();
                }
            }
        }

        [Test]
        public void FixtureImport_SecondRunPreservesGuidClipReferencesAndFilesOutsideFixture()
        {
            string clipPath = F02SpriteFamilyImporter.FixtureFolder + "/F02SpriteImportFixture_Idle.anim";
            string clipFullPath = ProjectFullPath(clipPath);
            string fixtureFullPath = ProjectFullPath(F02SpriteFamilyImporter.FixturePath);
            bool clipExistedBefore = File.Exists(clipFullPath);
            bool fixtureExistedBefore = File.Exists(fixtureFullPath);
            Dictionary<string, string> outsideBefore = SnapshotProtectedFiles();

            try
            {
                F02SpriteFamilyImporter.ImportFixture();
                ImportIdentity first = CaptureIdentity(clipPath);
                byte[] firstTextureMeta = File.ReadAllBytes(fixtureFullPath + ".meta");
                byte[] firstClipBytes = File.ReadAllBytes(clipFullPath);

                F02SpriteFamilyImporter.ImportFixture();
                ImportIdentity second = CaptureIdentity(clipPath);
                byte[] secondTextureMeta = File.ReadAllBytes(fixtureFullPath + ".meta");
                byte[] secondClipBytes = File.ReadAllBytes(clipFullPath);

                Assert.That(second.TextureGuid, Is.EqualTo(first.TextureGuid));
                Assert.That(second.SpriteGuid, Is.EqualTo(first.SpriteGuid));
                Assert.That(second.SpriteLocalId, Is.EqualTo(first.SpriteLocalId));
                Assert.That(second.ClipGuid, Is.EqualTo(first.ClipGuid));
                Assert.That(second.ClipLocalId, Is.EqualTo(first.ClipLocalId));
                CollectionAssert.AreEqual(first.CurveSpriteGuids, second.CurveSpriteGuids);
                CollectionAssert.AreEqual(first.CurveSpriteLocalIds, second.CurveSpriteLocalIds);
                CollectionAssert.AreEqual(firstTextureMeta, secondTextureMeta,
                    "A second import rewrote the fixture importer metadata.");
                CollectionAssert.AreEqual(firstClipBytes, secondClipBytes,
                    "A second import rewrote or duplicated the fixture clip.");

                Assert.That(first.TextureGuid, Is.Not.Empty);
                Assert.That(first.SliceCount, Is.EqualTo(1));
                Assert.That(first.FilterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(first.MipmapsEnabled, Is.False);
                Assert.That(first.PixelsPerUnit, Is.EqualTo(16f));
                Assert.That(first.AlphaIsTransparency, Is.True);
                Assert.That(first.GenerateFallbackPhysicsShape, Is.False);
                Assert.That(first.PhysicsShapeCount, Is.EqualTo(0));
                Assert.That(first.SpriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
                Assert.That(first.SliceName, Is.EqualTo("F02SpriteImportFixture_0"));
                Assert.That(first.SliceRect, Is.EqualTo(new Rect(0, 0, 32, 48)));
                Assert.That(first.SlicePivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(first.ClipKeyCount, Is.EqualTo(2));
                Assert.That(first.ClipLoops, Is.True);

                string[] clips = Directory.GetFiles(
                    Path.GetDirectoryName(clipFullPath),
                    "F02SpriteImportFixture_Idle*.anim",
                    SearchOption.TopDirectoryOnly);
                Assert.That(clips.Length, Is.EqualTo(1), "The second import created a duplicate clip.");

                Dictionary<string, string> outsideAfter = SnapshotProtectedFiles();
                AssertSnapshotsEqual(outsideBefore, outsideAfter);
            }
            finally
            {
                if (!clipExistedBefore)
                {
                    AssetDatabase.DeleteAsset(clipPath);
                }
                if (!fixtureExistedBefore)
                {
                    AssetDatabase.DeleteAsset(F02SpriteFamilyImporter.FixturePath);
                }
            }
        }

        private static ImportIdentity CaptureIdentity(string clipPath)
        {
            string textureGuid = AssetDatabase.AssetPathToGUID(F02SpriteFamilyImporter.FixturePath);
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(F02SpriteFamilyImporter.FixturePath)
                .OfType<Sprite>()
                .Single(item => item.name == "F02SpriteImportFixture_0");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip, Is.Not.Null);

            string spriteGuid;
            long spriteLocalId;
            string clipGuid;
            long clipLocalId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out spriteGuid, out spriteLocalId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out clipGuid, out clipLocalId), Is.True);

            var importer = AssetImporter.GetAtPath(F02SpriteFamilyImporter.FixturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            var importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider spriteData = factories.GetSpriteEditorDataProviderFromObject(importer);
            Assert.That(spriteData, Is.Not.Null);
            spriteData.InitSpriteEditorDataProvider();
            SpriteRect slice = spriteData.GetSpriteRects().Single();
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            Assert.That(keys, Is.Not.Null);

            var identity = new ImportIdentity
            {
                TextureGuid = textureGuid,
                SpriteGuid = spriteGuid,
                SpriteLocalId = spriteLocalId,
                ClipGuid = clipGuid,
                ClipLocalId = clipLocalId,
                CurveSpriteGuids = new string[keys.Length],
                CurveSpriteLocalIds = new long[keys.Length],
                SliceCount = spriteData.GetSpriteRects().Length,
                FilterMode = importer.filterMode,
                MipmapsEnabled = importer.mipmapEnabled,
                PixelsPerUnit = importer.spritePixelsPerUnit,
                AlphaIsTransparency = importer.alphaIsTransparency,
                GenerateFallbackPhysicsShape = importerSettings.spriteGenerateFallbackPhysicsShape,
                PhysicsShapeCount = sprite.GetPhysicsShapeCount(),
                SpriteImportMode = importer.spriteImportMode,
                SliceName = slice.name,
                SliceRect = slice.rect,
                SlicePivot = slice.pivot,
                ClipKeyCount = keys.Length,
                ClipLoops = AnimationUtility.GetAnimationClipSettings(clip).loopTime
            };

            for (int index = 0; index < keys.Length; index++)
            {
                Assert.That(keys[index].value, Is.EqualTo(sprite));
                long localId;
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    keys[index].value,
                    out identity.CurveSpriteGuids[index],
                    out localId), Is.True);
                identity.CurveSpriteLocalIds[index] = localId;
            }

            return identity;
        }

        private static Dictionary<string, string> SnapshotProtectedFiles()
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string fixtureRoot = Path.GetFullPath(ProjectFullPath(F02SpriteFamilyImporter.FixtureFolder));
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(assetsRoot, "*.meta", SearchOption.AllDirectories))
            {
                if (!IsInside(fixtureRoot, path))
                {
                    paths.Add(path);
                }
            }

            string artRoot = Path.Combine(assetsRoot, "Luckbound", "Art");
            if (Directory.Exists(artRoot))
            {
                foreach (string path in Directory.GetFiles(artRoot, "*", SearchOption.AllDirectories))
                {
                    if (!IsInside(fixtureRoot, path))
                    {
                        paths.Add(path);
                    }
                }
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                result.Add(path, HashFile(path));
            }
            return result;
        }

        private static void AssertSnapshotsEqual(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys,
                "Import changed the set of protected assets or metadata outside the fixture.");
            foreach (string key in expected.Keys)
            {
                Assert.That(actual[key], Is.EqualTo(expected[key]),
                    "Import changed a protected asset or metadata outside the fixture: " + key);
            }
        }

        private static bool IsInside(string root, string path)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
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
            {
                byte[] hash = sha256.ComputeHash(stream);
                return string.Concat(hash.Select(value => value.ToString("x2")));
            }
        }

        private static void CreateDirectorySymbolicLink(string linkPath, string targetPath)
        {
#if UNITY_EDITOR_WIN
            const uint directoryFlag = 0x1;
            const uint allowUnprivilegedCreate = 0x2;
            if (!CreateDirectorySymbolicLinkWindows(linkPath, targetPath, directoryFlag | allowUnprivilegedCreate))
                Assert.Inconclusive("The host could not create the directory symlink required by this regression; Win32 error " +
                    Marshal.GetLastWin32Error() + ".");
#else
            if (CreateDirectorySymbolicLinkUnix(targetPath, linkPath) != 0)
                Assert.Inconclusive("The host could not create the directory symlink required by this regression; errno " +
                    Marshal.GetLastWin32Error() + ".");
#endif
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private sealed class ImportIdentity
        {
            public string TextureGuid;
            public string SpriteGuid;
            public long SpriteLocalId;
            public string ClipGuid;
            public long ClipLocalId;
            public string[] CurveSpriteGuids;
            public long[] CurveSpriteLocalIds;
            public int SliceCount;
            public FilterMode FilterMode;
            public bool MipmapsEnabled;
            public float PixelsPerUnit;
            public bool AlphaIsTransparency;
            public bool GenerateFallbackPhysicsShape;
            public int PhysicsShapeCount;
            public SpriteImportMode SpriteImportMode;
            public string SliceName;
            public Rect SliceRect;
            public Vector2 SlicePivot;
            public int ClipKeyCount;
            public bool ClipLoops;
        }
    }
}
