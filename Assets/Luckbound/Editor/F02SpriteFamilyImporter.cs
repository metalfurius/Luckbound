using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Luckbound.Editor
{
    /// <summary>
    /// Reconciles only the isolated F02 technical fixture. F01's visual direction is approved;
    /// the Bellator cell remains a technical Prototype and this path never promotes it to Final.
    /// </summary>
    public static class F02SpriteFamilyImporter
    {
        public const string FixtureFolder = "Assets/Luckbound/Tests/EditMode/F02SpriteImport";
        public const string ManifestPath = FixtureFolder + "/manifest.json";
        public const string FixturePath = FixtureFolder + "/F02SpriteImportFixture.png";

        private const string SourcePath = "docs/art/F01_bellator_32x48.png";
        private const string ExpectedFixtureHash = "64f2078fc1c4d9183b896028918be04affc232d1a14a1ba2c6578fb7c502f457";
        private const string FixtureStatus = "Prototype";

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string status;
            public string provenance;
            public string sourcePath;
            public string sourceSha256;
            public int sourceBitDepth;
            public int sourceColorType;
            public string fixturePath;
            public int width;
            public int height;
            public int pixelsPerUnit;
            public string filterMode;
            public bool mipmaps;
            public bool alphaIsTransparency;
            public Slice[] slices;
            public Clip[] clips;
        }

        [Serializable]
        private sealed class Slice
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
        private sealed class Clip
        {
            public string name;
            public int framesPerSecond;
            public bool loop;
            public string[] frames;
        }

        [MenuItem("Luckbound/Art/Import F02 technical fixture")]
        private static void ImportFixtureMenu()
        {
            ImportFixture();
            Debug.Log("F02 technical fixture import is consistent.");
        }

        public static void ImportFixture()
        {
            ExecuteWithSafeOutputDirectory(ProjectFullPath(FixtureFolder), ImportFixtureAfterOutputPreflight);
        }

        private static void ImportFixtureAfterOutputPreflight()
        {
            Manifest manifest = ReadAndValidateManifest();
            string sourceFullPath = ProjectSourceFullPath(manifest.sourcePath);
            string fixtureFullPath = ProjectFullPath(manifest.fixturePath);

            ValidatePng(sourceFullPath, manifest);
            if (!File.Exists(fixtureFullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fixtureFullPath));
                File.Copy(sourceFullPath, fixtureFullPath);
            }
            ValidatePng(fixtureFullPath, manifest);

            AssetDatabase.ImportAsset(manifest.fixturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(manifest.fixturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("F02 fixture is not handled by TextureImporter: " + manifest.fixturePath);
            }

            int sourceWidth;
            int sourceHeight;
            importer.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
            if (sourceWidth != manifest.width || sourceHeight != manifest.height)
            {
                throw new InvalidOperationException(
                    "F02 fixture dimensions changed; expected " + manifest.width + "x" + manifest.height +
                    " but found " + sourceWidth + "x" + sourceHeight + ".");
            }

            ApplyTextureSettings(importer, manifest);
            importer = AssetImporter.GetAtPath(manifest.fixturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("TextureImporter disappeared after applying F02 settings.");
            }
            ApplySpriteMetadata(importer, manifest);
            importer = AssetImporter.GetAtPath(manifest.fixturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("TextureImporter disappeared after applying F02 sprite metadata.");
            }
            ValidateTextureSettings(importer, manifest);

            Sprite[] importedSprites = AssetDatabase.LoadAllAssetsAtPath(manifest.fixturePath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
            Slice[] expectedSlices = manifest.slices
                .OrderBy(slice => slice.name, StringComparer.Ordinal)
                .ToArray();
            if (importedSprites.Length != expectedSlices.Length)
            {
                throw new InvalidOperationException(
                    "F02 import produced " + importedSprites.Length + " sprites; expected " + expectedSlices.Length + ".");
            }

            var spritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (int index = 0; index < expectedSlices.Length; index++)
            {
                Slice slice = expectedSlices[index];
                Sprite sprite = importedSprites[index];
                if (!string.Equals(sprite.name, slice.name, StringComparison.Ordinal) ||
                    sprite.rect != new Rect(slice.x, slice.y, slice.width, slice.height) ||
                    sprite.pixelsPerUnit != manifest.pixelsPerUnit)
                {
                    throw new InvalidOperationException("F02 slice geometry or identity changed for " + slice.name + ".");
                }
                spritesByName.Add(slice.name, sprite);
            }

            foreach (Clip clip in manifest.clips.OrderBy(item => item.name, StringComparer.Ordinal))
            {
                EnsureClip(clip, spritesByName);
            }

            if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(manifest.fixturePath)))
            {
                throw new InvalidOperationException("Unity did not assign a stable GUID to the F02 fixture.");
            }
        }

        private static Manifest ReadAndValidateManifest()
        {
            string manifestFullPath = ProjectFullPath(ManifestPath);
            if (!File.Exists(manifestFullPath))
            {
                throw new FileNotFoundException("F02 fixture manifest is missing.", manifestFullPath);
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestFullPath, Encoding.UTF8));
            if (manifest == null ||
                manifest.schemaVersion != 1 ||
                !string.Equals(manifest.status, FixtureStatus, StringComparison.Ordinal) ||
                manifest.provenance == null ||
                manifest.provenance.IndexOf("unreviewed F01", StringComparison.Ordinal) < 0 ||
                !string.Equals(manifest.sourcePath, SourcePath, StringComparison.Ordinal) ||
                !string.Equals(manifest.sourceSha256, ExpectedFixtureHash, StringComparison.Ordinal) ||
                !string.Equals(manifest.fixturePath, FixturePath, StringComparison.Ordinal) ||
                manifest.width <= 0 ||
                manifest.height <= 0 ||
                manifest.pixelsPerUnit != 16 ||
                !string.Equals(manifest.filterMode, "Point", StringComparison.Ordinal) ||
                manifest.mipmaps ||
                !manifest.alphaIsTransparency ||
                manifest.slices == null || manifest.slices.Length == 0 ||
                manifest.clips == null || manifest.clips.Length == 0)
            {
                throw new InvalidDataException("F02 fixture manifest does not match the fixed Prototype import contract.");
            }

            var sliceNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Slice slice in manifest.slices)
            {
                if (slice == null ||
                    string.IsNullOrWhiteSpace(slice.name) ||
                    !IsSafeAssetName(slice.name) ||
                    !sliceNames.Add(slice.name) ||
                    slice.x < 0 || slice.y < 0 ||
                    slice.width <= 0 || slice.height <= 0 ||
                    slice.x + slice.width > manifest.width ||
                    slice.y + slice.height > manifest.height ||
                    slice.pivotX < 0f || slice.pivotX > 1f ||
                    slice.pivotY < 0f || slice.pivotY > 1f)
                {
                    throw new InvalidDataException("F02 fixture has a duplicate or out-of-bounds deterministic slice.");
                }
            }

            var clipNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Clip clip in manifest.clips)
            {
                if (clip == null ||
                    !IsSafeAssetName(clip.name) ||
                    !clipNames.Add(clip.name) ||
                    clip.framesPerSecond <= 0 ||
                    clip.frames == null || clip.frames.Length == 0 ||
                    clip.frames.Any(frame => !sliceNames.Contains(frame)))
                {
                    throw new InvalidDataException("F02 fixture has a duplicate or invalid animation clip declaration.");
                }
            }

            return manifest;
        }

        private static void ValidatePng(string fullPath, Manifest manifest)
        {
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("F02 fixture PNG is missing.", fullPath);
            }
            if (!string.Equals(HashFile(fullPath), manifest.sourceSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("F02 fixture PNG hash does not match its manifest.");
            }

            byte[] header = new byte[26];
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                {
                    throw new InvalidDataException("F02 fixture PNG header is incomplete.");
                }
            }

            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int index = 0; index < signature.Length; index++)
            {
                if (header[index] != signature[index])
                {
                    throw new InvalidDataException("F02 fixture source must remain a PNG.");
                }
            }

            int width = ReadBigEndianInt32(header, 16);
            int height = ReadBigEndianInt32(header, 20);
            if (width != manifest.width || height != manifest.height ||
                header[24] != manifest.sourceBitDepth ||
                header[25] != manifest.sourceColorType ||
                header[25] != 6)
            {
                throw new InvalidDataException("F02 fixture must remain the declared 8-bit RGBA PNG with alpha.");
            }
        }

        private static void ApplyTextureSettings(TextureImporter importer, Manifest manifest)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                changed = true;
            }
            if (importer.spritePixelsPerUnit != manifest.pixelsPerUnit)
            {
                importer.spritePixelsPerUnit = manifest.pixelsPerUnit;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }
            if (importer.mipmapEnabled != manifest.mipmaps)
            {
                importer.mipmapEnabled = manifest.mipmaps;
                changed = true;
            }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                changed = true;
            }
            if (importer.alphaIsTransparency != manifest.alphaIsTransparency)
            {
                importer.alphaIsTransparency = manifest.alphaIsTransparency;
                changed = true;
            }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool settingsChanged = false;
            if (settings.spriteGenerateFallbackPhysicsShape)
            {
                settings.spriteGenerateFallbackPhysicsShape = false;
                settingsChanged = true;
            }
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settingsChanged = true;
            }
            if (settingsChanged)
            {
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ApplySpriteMetadata(TextureImporter importer, Manifest manifest)
        {
            ISpriteEditorDataProvider provider = CreateSpriteDataProvider(importer);
            SpriteRect[] current = provider.GetSpriteRects();
            SpriteRect[] expected = BuildSpriteRects(manifest, current);
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            ISpritePhysicsOutlineDataProvider physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            if (names == null || physics == null)
                throw new InvalidOperationException("Unity does not expose sprite-name and physics-outline providers for F02.");

            SpriteNameFileIdPair[] expectedNames = expected
                .Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID))
                .ToArray();
            bool changed = !SpriteRectsMatch(current, expected) ||
                !SpriteNamePairsMatch(names.GetNameFileIdPairs(), expectedNames) ||
                expected.Any(sprite => HasPhysicsOutlines(physics.GetOutlines(sprite.spriteID)));
            if (!changed) return;

            provider.SetSpriteRects(expected);
            names.SetNameFileIdPairs(expectedNames);
            foreach (SpriteRect sprite in expected)
                physics.SetOutlines(sprite.spriteID, new List<Vector2[]>());
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static ISpriteEditorDataProvider CreateSpriteDataProvider(TextureImporter importer)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Unity could not create a sprite data provider for F02.");
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        private static SpriteRect[] BuildSpriteRects(Manifest manifest, SpriteRect[] current)
        {
            var existingByName = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
            foreach (SpriteRect existing in current ?? Array.Empty<SpriteRect>())
            {
                if (existing == null || string.IsNullOrWhiteSpace(existing.name)) continue;
                if (existingByName.ContainsKey(existing.name))
                    throw new InvalidDataException("F02 importer already contains duplicate sprite names.");
                existingByName.Add(existing.name, existing);
            }

            return manifest.slices
                .OrderBy(slice => slice.y)
                .ThenBy(slice => slice.x)
                .ThenBy(slice => slice.name, StringComparer.Ordinal)
                .Select(slice =>
                {
                    bool hasExisting = existingByName.TryGetValue(slice.name, out SpriteRect existing);
                    UnityEngine.GUID spriteId = hasExisting && !existing.spriteID.Empty()
                        ? existing.spriteID
                        : CreateDeterministicSpriteId(slice.name);
                    return new SpriteRect
                    {
                        name = slice.name,
                        rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(slice.pivotX, slice.pivotY),
                        border = Vector4.zero,
                        spriteID = spriteId
                    };
                })
                .ToArray();
        }

        private static UnityEngine.GUID CreateDeterministicSpriteId(string spriteName)
        {
            byte[] input = Encoding.UTF8.GetBytes(FixturePath + "/" + spriteName);
            using (MD5 md5 = MD5.Create())
            {
                string hex = string.Concat(md5.ComputeHash(input).Select(value => value.ToString("x2")));
                return new UnityEngine.GUID(hex);
            }
        }

        private static bool SpriteRectsMatch(SpriteRect[] actual, SpriteRect[] expected)
        {
            if (actual == null || actual.Length != expected.Length) return false;
            SpriteRect[] sortedActual = actual.OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            SpriteRect[] sortedExpected = expected.OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();

            for (int index = 0; index < sortedExpected.Length; index++)
            {
                SpriteRect left = sortedActual[index];
                SpriteRect right = sortedExpected[index];
                if (!string.Equals(left.name, right.name, StringComparison.Ordinal) || left.rect != right.rect ||
                    left.alignment != right.alignment || left.pivot != right.pivot || left.border != right.border ||
                    left.spriteID != right.spriteID) return false;
            }
            return true;
        }

        private static bool SpriteNamePairsMatch(IEnumerable<SpriteNameFileIdPair> actual,
            IEnumerable<SpriteNameFileIdPair> expected)
        {
            SpriteNameFileIdPair[] left = (actual ?? Enumerable.Empty<SpriteNameFileIdPair>())
                .OrderBy(pair => pair.name, StringComparer.Ordinal).ToArray();
            SpriteNameFileIdPair[] right = (expected ?? Enumerable.Empty<SpriteNameFileIdPair>())
                .OrderBy(pair => pair.name, StringComparer.Ordinal).ToArray();
            if (left.Length != right.Length) return false;
            for (int index = 0; index < right.Length; index++)
            {
                if (!string.Equals(left[index].name, right[index].name, StringComparison.Ordinal) ||
                    left[index].GetFileGUID() != right[index].GetFileGUID()) return false;
            }
            return true;
        }

        private static bool HasPhysicsOutlines(List<Vector2[]> outlines)
        {
            return outlines != null && outlines.Any(outline => outline != null && outline.Length > 0);
        }

        private static void ValidateTextureSettings(TextureImporter importer, Manifest manifest)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled ||
                importer.spritePixelsPerUnit != 16f ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                settings.spriteGenerateFallbackPhysicsShape ||
                settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                throw new InvalidOperationException("Unity did not retain the deterministic F02 texture settings.");
            }

            ISpriteEditorDataProvider provider = CreateSpriteDataProvider(importer);
            SpriteRect[] actual = provider.GetSpriteRects();
            SpriteRect[] expected = BuildSpriteRects(manifest, actual);
            ISpritePhysicsOutlineDataProvider physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            if (physics == null || !SpriteRectsMatch(actual, expected) ||
                expected.Any(sprite => HasPhysicsOutlines(physics.GetOutlines(sprite.spriteID))))
                throw new InvalidOperationException("Unity did not retain the deterministic F02 sprite data.");
        }

        private static void EnsureClip(Clip clipSpec, IReadOnlyDictionary<string, Sprite> spritesByName)
        {
            string clipPath = FixtureFolder + "/" + clipSpec.name + ".anim";
            string fullClipPath = ProjectFullPath(clipPath);
            string clipDirectory = Path.GetDirectoryName(fullClipPath);
            string[] sameNameClips = Directory.GetFiles(clipDirectory, clipSpec.name + "*.anim", SearchOption.TopDirectoryOnly);
            if (sameNameClips.Any(path => !string.Equals(path, fullClipPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("F02 would duplicate an animation clip named " + clipSpec.name + ".");
            }

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing == null && File.Exists(fullClipPath))
            {
                AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
                existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            }
            if (existing != null)
            {
                ValidateClip(existing, clipSpec, spritesByName);
                return;
            }

            var created = new AnimationClip
            {
                name = clipSpec.name,
                frameRate = clipSpec.framesPerSecond
            };
            var keys = new ObjectReferenceKeyframe[clipSpec.frames.Length + 1];
            for (int index = 0; index < clipSpec.frames.Length; index++)
            {
                keys[index] = new ObjectReferenceKeyframe
                {
                    time = (float)index / clipSpec.framesPerSecond,
                    value = spritesByName[clipSpec.frames[index]]
                };
            }
            keys[keys.Length - 1] = new ObjectReferenceKeyframe
            {
                time = (float)clipSpec.frames.Length / clipSpec.framesPerSecond,
                value = spritesByName[clipSpec.frames[clipSpec.frames.Length - 1]]
            };

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(created, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(created);
            settings.loopTime = clipSpec.loop;
            AnimationUtility.SetAnimationClipSettings(created, settings);
            AssetDatabase.CreateAsset(created, clipPath);
            ValidateClip(created, clipSpec, spritesByName);
        }

        private static void ValidateClip(AnimationClip clip, Clip clipSpec, IReadOnlyDictionary<string, Sprite> spritesByName)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!string.Equals(clip.name, clipSpec.name, StringComparison.Ordinal) ||
                !Mathf.Approximately(clip.frameRate, clipSpec.framesPerSecond) ||
                settings.loopTime != clipSpec.loop)
            {
                throw new InvalidDataException("Existing F02 clip settings differ from the manifest; the clip was left untouched.");
            }

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length != clipSpec.frames.Length + 1)
            {
                throw new InvalidDataException("Existing F02 clip keys differ from the manifest; the clip was left untouched.");
            }
            for (int index = 0; index < clipSpec.frames.Length; index++)
            {
                float expectedTime = (float)index / clipSpec.framesPerSecond;
                if (!Mathf.Approximately(keys[index].time, expectedTime) ||
                    keys[index].value != spritesByName[clipSpec.frames[index]])
                {
                    throw new InvalidDataException("Existing F02 clip references differ from the manifest; the clip was left untouched.");
                }
            }

            float terminalTime = (float)clipSpec.frames.Length / clipSpec.framesPerSecond;
            Sprite terminalSprite = spritesByName[clipSpec.frames[clipSpec.frames.Length - 1]];
            if (!Mathf.Approximately(keys[keys.Length - 1].time, terminalTime) ||
                keys[keys.Length - 1].value != terminalSprite)
            {
                throw new InvalidDataException("Existing F02 clip terminal key differs from the manifest; the clip was left untouched.");
            }
        }

        private static bool IsSafeAssetName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   value.IndexOf('/') < 0 &&
                   value.IndexOf('\\') < 0 &&
                   !string.Equals(value, ".", StringComparison.Ordinal) &&
                   !string.Equals(value, "..", StringComparison.Ordinal);
        }

        private static string ProjectFullPath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath) ||
                projectRelativePath.IndexOf('\\') >= 0 ||
                projectRelativePath.Contains("..") ||
                (projectRelativePath != FixtureFolder &&
                 !projectRelativePath.StartsWith(FixtureFolder + "/", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("F02 output path escaped its dedicated fixture folder.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string fixtureRoot = Path.GetFullPath(Path.Combine(projectRoot, FixtureFolder.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(fullPath, fixtureRoot, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(fixtureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("F02 output path escaped its dedicated fixture folder.");
            }
            return fullPath;
        }

        private static void ExecuteWithSafeOutputDirectory(string outputDirectoryFullPath, Action operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            RejectReparsePointsInOutputDirectory(outputDirectoryFullPath);
            operation();
        }

        private static void RejectReparsePointsInOutputDirectory(string outputDirectoryFullPath)
        {
            string projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsPrefix = assetsRoot + Path.DirectorySeparatorChar;
            string normalizedOutputDirectory = Path.GetFullPath(outputDirectoryFullPath);
            StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!normalizedOutputDirectory.StartsWith(projectPrefix, pathComparison) ||
                !normalizedOutputDirectory.StartsWith(assetsPrefix, pathComparison))
            {
                throw new InvalidDataException("F02 output directory is not under the project's Assets directory.");
            }

            string relativeDirectory = normalizedOutputDirectory.Substring(projectPrefix.Length);
            string current = projectRoot;
            foreach (string segment in relativeDirectory.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                RejectReparsePointIfPresent(current);
            }
        }

        private static void RejectReparsePointIfPresent(string fullPath)
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(fullPath);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("F02 output directory traverses a filesystem reparse point: " + fullPath);
        }

        private static string ProjectSourceFullPath(string projectRelativePath)
        {
            if (!string.Equals(projectRelativePath, SourcePath, StringComparison.Ordinal))
            {
                throw new InvalidDataException("F02 source path is not the fixed unreviewed reference fixture.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootWithSeparator = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("F02 source path escaped the project root.");
            }
            return fullPath;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    result.Append(value.ToString("x2"));
                }
                return result.ToString();
            }
        }
    }
}
