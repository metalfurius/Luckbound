using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace Luckbound.Editor
{
    /// <summary>
    /// Imports a manifest-owned sprite family from separate PNG sources. It reconciles only the
    /// declared output folder, assigns deterministic sprite IDs, and never derives physics shapes.
    /// </summary>
    public static class F02SpriteFamilyManifestImporter
    {
        private const int SchemaVersion = 3;
        private const int RequiredPixelsPerUnit = 16;

        [Serializable]
        private sealed class FamilyManifest
        {
            public int schemaVersion;
            public string familyId;
            public string resourceId;
            public string status;
            public string intendedUse;
            public string provenance;
            public string referenceId;
            public string referencePath;
            public string referenceSha256;
            public string promptReference;
            public string promptReferenceSha256;
            public string outputFolder;
            public int pixelsPerUnit;
            public string filterMode;
            public bool mipmaps;
            public bool alphaIsTransparency;
            public ImageSource[] images;
            public ClipSpec[] clips;
            public AtlasSpec atlas;
        }

        [Serializable]
        private sealed class ImageSource
        {
            public string sourcePath;
            public string assetFileName;
            public string resourceId;
            public string status;
            public string provenance;
            public string referenceId;
            public string referencePath;
            public string referenceSha256;
            public string promptReference;
            public string promptReferenceSha256;
            public string sha256;
            public int width;
            public int height;
            public int bitDepth;
            public int colorType;
            public SliceSpec[] slices;
        }

        [Serializable]
        private sealed class PromptRecord
        {
            public int schemaVersion;
            public string recordId;
            public int recordVersion;
            public string creationMethod;
            public string generator;
            public string literalPrompt;
            public string provenance;
            public PromptProcessing processing;
        }

        [Serializable]
        private sealed class PromptProcessing
        {
            public string tool;
            public string version;
            public string procedure;
            public string parameters;
        }

        [Serializable]
        private sealed class SliceSpec
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
        private sealed class ClipSpec
        {
            public string name;
            public int framesPerSecond;
            public bool loop;
            public string[] frames;
        }

        [Serializable]
        private sealed class AtlasSpec
        {
            public bool includeInBuild;
            public int padding;
        }

        // The fingerprint covers the normalized manifest plus its declared source hashes. All
        // source bytes are verified before this fingerprint is used to authorize a replay.
        private const string ManifestFingerprintAlgorithm = "f02-typed-json-canonical-v1";
        // Bump when importer behavior or fixed settings can change generated output independently
        // of manifest fields; old published output must never be silently reinterpreted.
        private const int OutputContractVersion = 1;
        private const int OwnershipSchemaVersion = 2;
        private const string OwnershipFileName = "F02FamilyOwnership.json";

        [Serializable]
        private sealed class OwnershipManifest
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
            public OwnedAsset[] assets;
        }

        [Serializable]
        private sealed class OwnedAsset
        {
            public string fileName;
            public string guid;
            public string contentSha256;
            public string metaSha256;
        }

        private sealed class DestinationPlan
        {
            public bool outputFolderExists;
            public bool isPublishedReplay;
            public OwnershipManifest ownership;
            public readonly HashSet<string> ownedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public bool Owns(string fileName) { return ownedFiles.Contains(fileName); }
        }

        /// <summary>Imports and validates one manifest using project-relative paths.</summary>
        public static void ImportFamily(string manifestPath)
        {
            FamilyManifest manifest = ReadAndValidateManifest(manifestPath);
            PreflightCanonicalPathAliases(manifest);
            string outputFullPath = ResolveProjectPath(manifest.outputFolder);

            // Preflight every source before the first AssetDatabase or file mutation.
            var validatedPayloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (ImageSource image in manifest.images)
            {
                string sourcePath = ResolveProjectPath(image.sourcePath);
                RejectReparsePointsInProjectPath(image.sourcePath);
                validatedPayloads.Add(image.assetFileName, ReadAndValidatePng(sourcePath, image));
            }
            string manifestSha256 = ComputeCanonicalManifestSha256(manifest);
            DestinationPlan plan = PreflightDestinations(manifest, manifestSha256);
            if (plan.isPublishedReplay) return;

            EnsureAssetFolder(manifest.outputFolder);

            var spritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var textures = new List<Texture2D>();
            foreach (ImageSource image in manifest.images.OrderBy(item => item.assetFileName, StringComparer.Ordinal))
            {
                string assetPath = manifest.outputFolder + "/" + image.assetFileName;
                string assetFullPath = ResolveProjectPath(assetPath);
                CopySourceIfChanged(validatedPayloads[image.assetFileName], image.sha256, assetFullPath,
                    plan.Owns(image.assetFileName));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Family PNG is not handled by TextureImporter: " + assetPath);
                ValidateSourceDimensions(importer, image);
                ApplyTextureSettings(importer, manifest);

                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter disappeared while applying family settings: " + assetPath);
                ApplySpriteMetadata(importer, manifest.familyId, image);
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter disappeared while applying family slices: " + assetPath);
                ValidateTextureSettings(importer, manifest, image);

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                    throw new InvalidOperationException("Unity did not import family texture: " + assetPath);
                textures.Add(texture);

                Sprite[] imported = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Sprite>()
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToArray();
                SliceSpec[] expected = image.slices.OrderBy(slice => slice.name, StringComparer.Ordinal).ToArray();
                if (imported.Length != expected.Length)
                    throw new InvalidOperationException("Imported slice count differs from the family manifest: " + assetPath);
                for (int index = 0; index < expected.Length; index++)
                {
                    Sprite sprite = imported[index];
                    SliceSpec slice = expected[index];
                    if (!string.Equals(sprite.name, slice.name, StringComparison.Ordinal) ||
                        sprite.rect != new Rect(slice.x, slice.y, slice.width, slice.height) ||
                        sprite.pixelsPerUnit != manifest.pixelsPerUnit || sprite.GetPhysicsShapeCount() != 0)
                        throw new InvalidOperationException("Imported sprite geometry, PPU, or physics shape differs: " + slice.name);
                    spritesByName.Add(slice.name, sprite);
                }
            }

            foreach (ClipSpec clip in manifest.clips.OrderBy(item => item.name, StringComparer.Ordinal))
                EnsureClip(manifest.outputFolder, clip, spritesByName, plan.Owns(clip.name + ".anim"));
            ValidateClipSet(outputFullPath, manifest);
            EnsureFamilyAtlas(manifest, textures, plan.Owns(manifest.familyId + ".spriteatlas"));
            WriteOwnershipManifest(manifest, manifestSha256);
        }

        private static FamilyManifest ReadAndValidateManifest(string manifestPath)
        {
            string manifestFullPath = ResolveProjectPath(manifestPath);
            if (!File.Exists(manifestFullPath))
                throw new FileNotFoundException("F02 family manifest is missing.", manifestFullPath);

            byte[] manifestSnapshot = File.ReadAllBytes(manifestFullPath);
            EncounterCatalogJsonValueV1 manifestTokens;
            EncounterCatalogError jsonError;
            if (!EncounterCatalogStrictJsonReaderV1.TryRead(manifestSnapshot, out manifestTokens, out jsonError))
                throw new InvalidDataException("F02 family manifest JSON is invalid: code=" + jsonError.Code +
                    "; path=" + jsonError.Path + ".");
            if (manifestTokens == null || manifestTokens.Kind != EncounterCatalogJsonValueKindV1.Object)
                throw new InvalidDataException("F02 family manifest JSON root must be an object.");

            RequireFamilyJsonString(manifestTokens, "familyId");
            RequireFamilyJsonString(manifestTokens, "resourceId");
            RequireFamilyJsonString(manifestTokens, "status");
            RequireFamilyJsonString(manifestTokens, "intendedUse");
            RequireFamilyJsonString(manifestTokens, "provenance");
            RequireFamilyJsonString(manifestTokens, "referenceId");
            RequireFamilyJsonString(manifestTokens, "referencePath");
            RequireFamilyJsonString(manifestTokens, "referenceSha256");
            RequireFamilyJsonString(manifestTokens, "promptReference");
            RequireFamilyJsonString(manifestTokens, "promptReferenceSha256");

            EncounterCatalogJsonValueV1 imageArray;
            if (!manifestTokens.Members.TryGetValue("images", out imageArray) || imageArray == null ||
                imageArray.Kind != EncounterCatalogJsonValueKindV1.Array || imageArray.Items == null)
                throw new InvalidDataException("F02 family manifest JSON images must be an array.");
            ValidateImageTraceabilityTokenTypes(imageArray);

            string manifestJson;
            try
            {
                manifestJson = new UTF8Encoding(false, true).GetString(manifestSnapshot);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("F02 family manifest JSON must be valid UTF-8.");
            }

            FamilyManifest manifest = JsonUtility.FromJson<FamilyManifest>(manifestJson);
            if (manifest == null || manifest.images == null || manifest.images.Length != imageArray.Items.Count)
                throw new InvalidDataException("F02 family manifest JSON images do not match the image DTO array.");
            ResolveImageTraceabilityOverrides(manifest, imageArray);

            if (manifest == null || manifest.schemaVersion != SchemaVersion || !IsSafeAssetName(manifest.familyId) ||
                string.IsNullOrWhiteSpace(manifest.resourceId) ||
                (manifest.status != "Prototype" && manifest.status != "Final") ||
                string.IsNullOrWhiteSpace(manifest.intendedUse) || string.IsNullOrWhiteSpace(manifest.provenance) ||
                string.IsNullOrWhiteSpace(manifest.referenceId) || string.IsNullOrWhiteSpace(manifest.promptReference) ||
                !IsSha256(manifest.referenceSha256) || !IsSha256(manifest.promptReferenceSha256) ||
                manifest.pixelsPerUnit != RequiredPixelsPerUnit ||
                manifest.filterMode != "Point" || manifest.mipmaps || !manifest.alphaIsTransparency ||
                manifest.images == null || manifest.images.Length == 0 || manifest.clips == null ||
                manifest.atlas == null || manifest.atlas.padding < 0 || manifest.atlas.padding > 16 ||
                !IsAssetFolderPath(manifest.outputFolder))
                throw new InvalidDataException("F02 family manifest does not match schema 3 and the fixed sprite import contract.");

            manifest.outputFolder = CanonicalizeOutputFolderPath(manifest.outputFolder);
            ValidateVersionedPromptReference(manifest.promptReference, manifest.promptReferenceSha256);
            ValidateProjectRelativePath(manifest.referencePath);
            RejectReparsePointsInProjectPath(manifest.referencePath);
            string referenceFullPath = ResolveProjectPath(manifest.referencePath);
            if (!File.Exists(referenceFullPath) ||
                !string.Equals(HashFile(referenceFullPath), manifest.referenceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 family reference file or its declared hash has changed.");

            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            var spriteNames = new HashSet<string>(StringComparer.Ordinal);
            var spriteNamesWithoutCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageSource image in manifest.images)
            {
                bool duplicateResourceIdBeforeValidation = !string.IsNullOrWhiteSpace(image.resourceId) &&
                    resourceIds.Contains(image.resourceId);
                bool duplicateSourcePathBeforeValidation = sourcePaths.Contains(image.sourcePath ?? string.Empty);
                if (string.IsNullOrWhiteSpace(image.resourceId) || !resourceIds.Add(image.resourceId) ||
                    (image.status != "Prototype" && image.status != "Final") ||
                    string.IsNullOrWhiteSpace(image.provenance) || string.IsNullOrWhiteSpace(image.referenceId) ||
                    string.IsNullOrWhiteSpace(image.promptReference) || !IsSha256(image.promptReferenceSha256) ||
                    !IsSha256(image.referenceSha256) ||
                    !IsSafePngName(image.assetFileName) ||
                    !sourcePaths.Add(image.sourcePath ?? string.Empty) ||
                    !IsSha256(image.sha256) || image.width <= 0 || image.height <= 0 || image.bitDepth != 8 ||
                    (image.colorType != 0 && image.colorType != 2 && image.colorType != 3 &&
                     image.colorType != 4 && image.colorType != 6) || image.slices == null || image.slices.Length == 0)
                    throw new InvalidDataException("F02 image traceability metadata is missing, malformed, or has a duplicate resource ID." +
                        DescribeImageTraceabilityFailure(image, duplicateResourceIdBeforeValidation,
                            duplicateSourcePathBeforeValidation));
                ValidateProjectRelativePath(image.sourcePath);
                ValidateVersionedPromptReference(image.promptReference, image.promptReferenceSha256);
                ValidateProjectRelativePath(image.referencePath);
                RejectReparsePointsInProjectPath(image.referencePath);
                string imageReferenceFullPath = ResolveProjectPath(image.referencePath);
                if (!File.Exists(imageReferenceFullPath) ||
                    !string.Equals(HashFile(imageReferenceFullPath), image.referenceSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("F02 image reference file or its declared hash has changed: " + image.referencePath);
                if (string.Equals(image.sha256, image.referenceSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("An F02 image source cannot also be its effective reference (identical content hash).");
                string canonicalSourcePath = CanonicalPathKey(ResolveProjectPath(image.sourcePath));
                string canonicalReferencePath = CanonicalPathKey(imageReferenceFullPath);
                if (string.Equals(canonicalSourcePath, canonicalReferencePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("An F02 image source cannot also be its effective reference.");
                if (string.Equals(Path.GetFileName(image.sourcePath), "F01_master_reference.png", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The F01 concept sheet is a visual reference only and cannot be an imported source.");

                foreach (SliceSpec slice in image.slices)
                {
                    if (slice == null || !IsSafeAssetName(slice.name) || !spriteNames.Add(slice.name) ||
                        !spriteNamesWithoutCase.Add(slice.name) ||
                        slice.x < 0 || slice.y < 0 || slice.width <= 0 || slice.height <= 0 ||
                        (long)slice.x + slice.width > image.width || (long)slice.y + slice.height > image.height ||
                        slice.pivotX < 0f || slice.pivotX > 1f || slice.pivotY < 0f || slice.pivotY > 1f)
                        throw new InvalidDataException("F02 family slices must have unique case-insensitive names, explicit pivots, and in-bounds rectangles.");
                }
            }

            var clipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ClipSpec clip in manifest.clips)
            {
                if (clip == null || !IsSafeAssetName(clip.name) || !clipNames.Add(clip.name) ||
                    clip.framesPerSecond <= 0 || clip.frames == null || clip.frames.Length == 0 ||
                    clip.frames.Any(frame => !spriteNames.Contains(frame)))
                    throw new InvalidDataException("F02 family clips must have unique case-insensitive names and reference declared sprites only.");
            }
            ValidateOutputFileNames(manifest);
            return manifest;
        }

        private static void RequireFamilyJsonString(EncounterCatalogJsonValueV1 manifestTokens, string propertyName)
        {
            EncounterCatalogJsonValueV1 value;
            if (!manifestTokens.Members.TryGetValue(propertyName, out value) || value == null ||
                value.Kind != EncounterCatalogJsonValueKindV1.String)
                throw new InvalidDataException("F02 family metadata field must be a JSON string: " + propertyName + ".");
        }

        private static void ResolveImageTraceabilityOverrides(
            FamilyManifest manifest, EncounterCatalogJsonValueV1 imageArray)
        {
            for (int index = 0; index < imageArray.Items.Count; index++)
            {
                EncounterCatalogJsonValueV1 imageTokens = imageArray.Items[index];
                ImageSource image = manifest.images[index];
                if (imageTokens == null || imageTokens.Kind != EncounterCatalogJsonValueKindV1.Object ||
                    imageTokens.Members == null || image == null)
                    throw new InvalidDataException("F02 family images must contain matching objects: index=" +
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

                image.resourceId = ResolveImageMetadataString(imageTokens, "resourceId", manifest.resourceId, index);
                image.status = ResolveImageMetadataString(imageTokens, "status", manifest.status, index);
                image.provenance = ResolveImageMetadataString(imageTokens, "provenance", manifest.provenance, index);
                image.referenceId = ResolveImageMetadataString(imageTokens, "referenceId", manifest.referenceId, index);
                image.referencePath = ResolveImageMetadataString(imageTokens, "referencePath", manifest.referencePath, index);
                image.referenceSha256 = ResolveImageMetadataString(imageTokens, "referenceSha256", manifest.referenceSha256, index);
                image.promptReference = ResolveImageMetadataString(imageTokens, "promptReference", manifest.promptReference, index);
                image.promptReferenceSha256 = ResolveImageMetadataString(
                    imageTokens, "promptReferenceSha256", manifest.promptReferenceSha256, index);
            }
        }

        private static void ValidateImageTraceabilityTokenTypes(EncounterCatalogJsonValueV1 imageArray)
        {
            string[] metadataFields =
            {
                "resourceId", "status", "provenance", "referenceId", "referencePath", "referenceSha256",
                "promptReference", "promptReferenceSha256"
            };
            for (int index = 0; index < imageArray.Items.Count; index++)
            {
                EncounterCatalogJsonValueV1 imageTokens = imageArray.Items[index];
                if (imageTokens == null || imageTokens.Kind != EncounterCatalogJsonValueKindV1.Object ||
                    imageTokens.Members == null)
                    throw new InvalidDataException("F02 family images must contain matching objects: index=" +
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

                foreach (string propertyName in metadataFields)
                {
                    EncounterCatalogJsonValueV1 value;
                    if (imageTokens.Members.TryGetValue(propertyName, out value) && value != null &&
                        value.Kind != EncounterCatalogJsonValueKindV1.Null &&
                        value.Kind != EncounterCatalogJsonValueKindV1.String)
                        throw new InvalidDataException("F02 image metadata override must be a JSON string or null: images[" +
                            index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]." + propertyName +
                            " (type=" + value.Kind + ").");
                }
            }
        }

        private static string ResolveImageMetadataString(
            EncounterCatalogJsonValueV1 imageTokens, string propertyName, string familyDefault, int imageIndex)
        {
            EncounterCatalogJsonValueV1 value;
            if (!imageTokens.Members.TryGetValue(propertyName, out value) || value == null ||
                value.Kind == EncounterCatalogJsonValueKindV1.Null)
                return familyDefault;
            if (value.Kind != EncounterCatalogJsonValueKindV1.String)
                throw new InvalidDataException("F02 image metadata override must be a JSON string or null: images[" +
                    imageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]." + propertyName +
                    " (type=" + value.Kind + ").");
            return value.Text;
        }

        private static void ValidateVersionedPromptReference(string projectRelativePath, string expectedSha256)
        {
            ValidateProjectRelativePath(projectRelativePath);
            const string recordsDirectory = "docs/art/records/";
            if (!projectRelativePath.StartsWith(recordsDirectory, StringComparison.Ordinal) ||
                projectRelativePath.Substring(recordsDirectory.Length).IndexOf('/') >= 0)
                throw new InvalidDataException("F02 promptReference records must live directly under docs/art/records/.");
            RejectReparsePointsInProjectPath(projectRelativePath);
            string fullPath = ResolveProjectPath(projectRelativePath);
            if (!File.Exists(fullPath))
                throw new InvalidDataException("F02 promptReference must point to an existing versioned creation record: " + projectRelativePath);
            if (!IsSha256(expectedSha256) ||
                !string.Equals(HashFile(fullPath), expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 promptReference creation record hash differs from its manifest declaration: " + projectRelativePath);

            string fileName = Path.GetFileName(projectRelativePath);
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 promptReference must point to a versioned JSON creation record.");
            int versionMarker = fileName.LastIndexOf(".v", StringComparison.OrdinalIgnoreCase);
            string versionText = versionMarker < 1
                ? string.Empty
                : fileName.Substring(versionMarker + 2, fileName.Length - versionMarker - 2 - ".json".Length);
            int fileVersion;
            if (versionText.Length == 0 || versionText.Any(character => character < '0' || character > '9') ||
                !int.TryParse(versionText, out fileVersion) || fileVersion < 1)
                throw new InvalidDataException("F02 promptReference filename must end with a positive .vN.json version.");

            PromptRecord record;
            try
            {
                record = JsonUtility.FromJson<PromptRecord>(File.ReadAllText(fullPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("F02 promptReference creation record is malformed: " + projectRelativePath, exception);
            }

            bool generated = record != null && record.creationMethod == "generated";
            bool unprompted = record != null && (record.creationMethod == "manual" || record.creationMethod == "programmatic");
            bool creationDetailsValid = generated
                ? !string.IsNullOrWhiteSpace(record.generator) && !string.IsNullOrWhiteSpace(record.literalPrompt)
                : unprompted && !string.IsNullOrWhiteSpace(record.provenance) &&
                  string.IsNullOrWhiteSpace(record.literalPrompt);
            if (record == null || record.schemaVersion != 1 || record.recordVersion != fileVersion ||
                !IsSafeAssetName(record.recordId) ||
                !string.Equals(fileName, record.recordId + ".v" + record.recordVersion + ".json", StringComparison.Ordinal) ||
                !creationDetailsValid || record.processing == null ||
                string.IsNullOrWhiteSpace(record.processing.tool) ||
                string.IsNullOrWhiteSpace(record.processing.version) ||
                string.IsNullOrWhiteSpace(record.processing.procedure) ||
                string.IsNullOrWhiteSpace(record.processing.parameters))
                throw new InvalidDataException("F02 promptReference must resolve to a versioned record with literal prompt or non-prompt provenance and reproducible processing details.");
        }

        private static byte[] ReadAndValidatePng(string fullPath, ImageSource image)
        {
            if (!File.Exists(fullPath)) throw new FileNotFoundException("F02 family source PNG is missing.", fullPath);
            // Capture once: validation, decoding, and the later output write must all use the
            // same immutable in-memory payload. This closes the hash/validate/copy reopen gap;
            // it does not replace the filesystem identity and AssetDatabase exclusion gates.
            byte[] payload = File.ReadAllBytes(fullPath);
            if (!string.Equals(HashBytes(payload), image.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 family source hash differs from the manifest: " + image.sourcePath);
            byte[] header = new byte[26];
            if (payload.Length < header.Length)
                throw new InvalidDataException("F02 family PNG header is incomplete: " + image.sourcePath);
            Array.Copy(payload, header, header.Length);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int index = 0; index < signature.Length; index++)
                if (header[index] != signature[index]) throw new InvalidDataException("F02 family source is not a PNG.");
            if (ReadBigEndianInt32(header, 16) != image.width || ReadBigEndianInt32(header, 20) != image.height ||
                header[24] != image.bitDepth || header[25] != image.colorType)
                throw new InvalidDataException("F02 family PNG dimensions or encoding differ from the manifest: " + image.sourcePath);

            // Decode every source during preflight so a corrupt later PNG cannot fail after earlier
            // family outputs have already been copied or reimported.
            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            decoded.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                if (!ImageConversion.LoadImage(decoded, payload, false) ||
                    decoded.width != image.width || decoded.height != image.height)
                    throw new InvalidDataException("F02 family PNG cannot be decoded or differs from its declared dimensions: " + image.sourcePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }

            return payload;
        }

        private static void ValidateSourceDimensions(TextureImporter importer, ImageSource image)
        {
            int width;
            int height;
            importer.GetSourceTextureWidthAndHeight(out width, out height);
            if (width != image.width || height != image.height)
                throw new InvalidDataException("Unity source dimensions differ from the F02 manifest: " + image.assetFileName);
        }

        private static void CopySourceIfChanged(byte[] sourcePayload, string expectedSourceSha256,
            string assetFullPath, bool alreadyOwned)
        {
            if (sourcePayload == null ||
                !string.Equals(HashBytes(sourcePayload), expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 captured source payload differs from its manifest hash.");
            if (File.Exists(assetFullPath) && !alreadyOwned)
                throw new InvalidDataException("A declared F02 PNG exists without matching family ownership and was left untouched: " + assetFullPath);
            if (!File.Exists(assetFullPath) && File.Exists(assetFullPath + ".meta"))
                throw new InvalidDataException("A declared F02 PNG has orphaned metadata and was left untouched: " + assetFullPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetFullPath));
            if (!File.Exists(assetFullPath) ||
                !string.Equals(HashFile(assetFullPath), expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
                File.WriteAllBytes(assetFullPath, sourcePayload);
            if (!string.Equals(HashFile(assetFullPath), expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 output bytes differ from the validated source payload: " + assetFullPath);
        }

        private static void ApplyTextureSettings(TextureImporter importer, FamilyManifest manifest)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Multiple) { importer.spriteImportMode = SpriteImportMode.Multiple; changed = true; }
            if (importer.spritePixelsPerUnit != RequiredPixelsPerUnit) { importer.spritePixelsPerUnit = RequiredPixelsPerUnit; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool settingsChanged = false;
            if (settings.spriteGenerateFallbackPhysicsShape) { settings.spriteGenerateFallbackPhysicsShape = false; settingsChanged = true; }
            if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; settingsChanged = true; }
            if (settingsChanged) { importer.SetTextureSettings(settings); changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        private static void ApplySpriteMetadata(TextureImporter importer, string familyId, ImageSource image)
        {
            ISpriteEditorDataProvider provider = CreateSpriteDataProvider(importer);
            SpriteRect[] actual = provider.GetSpriteRects();
            SpriteRect[] expected = image.slices
                .OrderBy(slice => slice.y).ThenBy(slice => slice.x).ThenBy(slice => slice.name, StringComparer.Ordinal)
                .Select(slice => new SpriteRect
                {
                    name = slice.name,
                    rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(slice.pivotX, slice.pivotY),
                    border = Vector4.zero,
                    spriteID = CreateDeterministicSpriteId(familyId, image.assetFileName, slice.name)
                }).ToArray();
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            ISpritePhysicsOutlineDataProvider physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            if (names == null || physics == null)
                throw new InvalidOperationException("Unity lacks the sprite ID or physics-outline provider required by F02.");
            SpriteNameFileIdPair[] expectedNames = expected.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)).ToArray();
            bool changed = !SpriteRectsMatch(actual, expected) ||
                !SpriteNamePairsMatch(names.GetNameFileIdPairs(), expectedNames) ||
                expected.Any(sprite => HasPhysicsOutlines(physics.GetOutlines(sprite.spriteID)));
            if (!changed) return;
            provider.SetSpriteRects(expected);
            names.SetNameFileIdPairs(expectedNames);
            foreach (SpriteRect sprite in expected) physics.SetOutlines(sprite.spriteID, new List<Vector2[]>());
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static ISpriteEditorDataProvider CreateSpriteDataProvider(TextureImporter importer)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Unity could not create an F02 sprite data provider.");
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        private static UnityEngine.GUID CreateDeterministicSpriteId(string familyId, string assetFileName, string spriteName)
        {
            string identity = "luckbound:f02:" + familyId + ":" + assetFileName + ":" + spriteName;
            using (SHA256 sha256 = SHA256.Create())
            {
                string hex = string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(identity))
                    .Take(16).Select(value => value.ToString("x2")));
                return new UnityEngine.GUID(hex);
            }
        }

        private static bool SpriteRectsMatch(SpriteRect[] actual, SpriteRect[] expected)
        {
            if (actual == null || actual.Length != expected.Length) return false;
            SpriteRect[] left = actual.OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
            SpriteRect[] right = expected.OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < right.Length; i++)
                if (left[i] == null || !string.Equals(left[i].name, right[i].name, StringComparison.Ordinal) ||
                    left[i].rect != right[i].rect || left[i].alignment != right[i].alignment ||
                    left[i].pivot != right[i].pivot || left[i].border != right[i].border || left[i].spriteID != right[i].spriteID)
                    return false;
            return true;
        }

        private static bool SpriteNamePairsMatch(IEnumerable<SpriteNameFileIdPair> actual, IEnumerable<SpriteNameFileIdPair> expected)
        {
            SpriteNameFileIdPair[] left = (actual ?? Enumerable.Empty<SpriteNameFileIdPair>())
                .OrderBy(pair => pair.name, StringComparer.Ordinal).ToArray();
            SpriteNameFileIdPair[] right = expected.OrderBy(pair => pair.name, StringComparer.Ordinal).ToArray();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < right.Length; i++)
                if (!string.Equals(left[i].name, right[i].name, StringComparison.Ordinal) ||
                    left[i].GetFileGUID() != right[i].GetFileGUID()) return false;
            return true;
        }

        private static bool HasPhysicsOutlines(List<Vector2[]> outlines)
        {
            return outlines != null && outlines.Any(outline => outline != null && outline.Length > 0);
        }

        private static void ValidateTextureSettings(TextureImporter importer, FamilyManifest manifest, ImageSource image)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.spritePixelsPerUnit != manifest.pixelsPerUnit ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.spriteGenerateFallbackPhysicsShape || settings.spriteMeshType != SpriteMeshType.FullRect)
                throw new InvalidOperationException("Unity did not retain the declared F02 texture settings.");

            ISpriteEditorDataProvider provider = CreateSpriteDataProvider(importer);
            SpriteRect[] actual = provider.GetSpriteRects();
            SpriteRect[] expected = image.slices.Select(slice => new SpriteRect
            {
                name = slice.name,
                rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(slice.pivotX, slice.pivotY),
                border = Vector4.zero,
                spriteID = CreateDeterministicSpriteId(manifest.familyId, image.assetFileName, slice.name)
            }).ToArray();
            ISpritePhysicsOutlineDataProvider physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            if (physics == null || !SpriteRectsMatch(actual, expected) ||
                expected.Any(sprite => HasPhysicsOutlines(physics.GetOutlines(sprite.spriteID))))
                throw new InvalidOperationException("Unity did not retain F02 slices, pivots, stable IDs, or empty physics outlines.");
        }

        private static void EnsureClip(string outputFolder, ClipSpec spec, IReadOnlyDictionary<string, Sprite> spritesByName, bool alreadyOwned)
        {
            string clipPath = outputFolder + "/" + spec.name + ".anim";
            string fullPath = ResolveProjectPath(clipPath);
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (File.Exists(fullPath) && !alreadyOwned)
                throw new InvalidDataException("A declared F02 clip exists without matching family ownership and was left untouched: " + spec.name);
            if (!File.Exists(fullPath) && File.Exists(fullPath + ".meta"))
                throw new InvalidDataException("A declared F02 clip has orphaned metadata and was left untouched: " + spec.name);
            if (alreadyOwned && existing == null)
                throw new InvalidDataException("An owned F02 clip could not be loaded and was left untouched: " + spec.name);
            if (existing != null)
            {
                ValidateClip(existing, spec, spritesByName);
                return;
            }

            var clip = new AnimationClip { name = spec.name, frameRate = spec.framesPerSecond };
            var keys = new ObjectReferenceKeyframe[spec.frames.Length];
            for (int i = 0; i < spec.frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = (float)i / spec.framesPerSecond, value = spritesByName[spec.frames[i]] };
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = ExpectedClipDuration(spec);
            settings.loopTime = spec.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string preCreateAssetMeasurements = CaptureClipMeasurements(
                clip, spec, AnimationUtility.GetAnimationClipSettings(clip), keys, ExpectedClipDuration(spec));
            AssetDatabase.CreateAsset(clip, clipPath);
            ValidateClip(clip, spec, spritesByName, preCreateAssetMeasurements);
        }

        private static void ValidateClip(AnimationClip clip, ClipSpec spec, IReadOnlyDictionary<string, Sprite> spritesByName,
            string preCreateAssetMeasurements = null)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!string.Equals(clip.name, spec.name, StringComparison.Ordinal) ||
                !IsFiniteAndApproximately(clip.frameRate, spec.framesPerSecond) || settings.loopTime != spec.loop)
                throw new InvalidDataException("An existing F02 clip differs from its manifest; it was not overwritten: " + spec.name);

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            EditorCurveBinding[] objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (objectReferenceBindings == null || objectReferenceBindings.Length != 1 ||
                !string.Equals(objectReferenceBindings[0].path, binding.path, StringComparison.Ordinal) ||
                objectReferenceBindings[0].type != binding.type ||
                !string.Equals(objectReferenceBindings[0].propertyName, binding.propertyName, StringComparison.Ordinal))
                throw new InvalidDataException("An F02 clip must contain only its declared SpriteRenderer sprite binding: " + spec.name);

            EditorCurveBinding[] floatCurveBindings = AnimationUtility.GetCurveBindings(clip);
            if (floatCurveBindings == null || floatCurveBindings.Length != 0)
                throw new InvalidDataException("An F02 clip cannot contain undeclared float-curve bindings: " + spec.name);

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            if (events == null || events.Length != 0)
                throw new InvalidDataException("An F02 clip cannot contain undeclared animation events: " + spec.name);

            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length != spec.frames.Length)
                throw new InvalidDataException("Existing F02 clip frame count differs from its manifest: " + spec.name);
            float expectedDuration = ExpectedClipDuration(spec);
            if (!IsFiniteAndApproximately(settings.startTime, 0f) ||
                !IsFiniteAndApproximately(settings.stopTime, expectedDuration))
                throw new InvalidDataException(DescribeClipDuration(clip, spec, settings, keys, expectedDuration, "AnimationClipSettings.startTime/stopTime", preCreateAssetMeasurements));
            for (int i = 0; i < spec.frames.Length; i++)
            {
                Sprite expected;
                if (!spritesByName.TryGetValue(spec.frames[i], out expected) ||
                    !IsFiniteAndApproximately(keys[i].time, (float)i / spec.framesPerSecond) ||
                    keys[i].value != expected)
                    throw new InvalidDataException("Existing F02 clip frame reference differs from its manifest: " + spec.name);
            }
            if (!IsFiniteAndApproximately(clip.length, expectedDuration))
                throw new InvalidDataException(DescribeClipDuration(clip, spec, settings, keys, expectedDuration, "AnimationClip.length", preCreateAssetMeasurements));
        }

        private static float ExpectedClipDuration(ClipSpec spec)
        {
            return (float)spec.frames.Length / spec.framesPerSecond;
        }

        private static string DescribeImageTraceabilityFailure(
            ImageSource image, bool duplicateResourceIdBeforeValidation, bool duplicateSourcePathBeforeValidation)
        {
            var failedPredicates = new List<string>();
            if (string.IsNullOrWhiteSpace(image.resourceId)) failedPredicates.Add("resourceId.blank");
            if (duplicateResourceIdBeforeValidation) failedPredicates.Add("resourceId.duplicate");
            if (image.status != "Prototype" && image.status != "Final") failedPredicates.Add("status.invalid");
            if (string.IsNullOrWhiteSpace(image.provenance)) failedPredicates.Add("provenance.blank");
            if (string.IsNullOrWhiteSpace(image.referenceId)) failedPredicates.Add("referenceId.blank");
            if (string.IsNullOrWhiteSpace(image.promptReference)) failedPredicates.Add("promptReference.blank");
            if (!IsSha256(image.promptReferenceSha256)) failedPredicates.Add("promptReferenceSha256.invalid");
            if (!IsSha256(image.referenceSha256)) failedPredicates.Add("referenceSha256.invalid");
            if (!IsSafePngName(image.assetFileName)) failedPredicates.Add("assetFileName.invalid");
            if (duplicateSourcePathBeforeValidation) failedPredicates.Add("sourcePath.duplicate");
            if (!IsSha256(image.sha256)) failedPredicates.Add("sha256.invalid");
            if (image.width <= 0) failedPredicates.Add("width.nonpositive");
            if (image.height <= 0) failedPredicates.Add("height.nonpositive");
            if (image.bitDepth != 8) failedPredicates.Add("bitDepth.not8");
            if (image.colorType != 0 && image.colorType != 2 && image.colorType != 3 &&
                image.colorType != 4 && image.colorType != 6) failedPredicates.Add("colorType.invalid");
            if (image.slices == null || image.slices.Length == 0) failedPredicates.Add("slices.empty");

            string sliceCount = image.slices == null
                ? "<null>"
                : image.slices.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return " [failedPredicates=" + string.Join(",", failedPredicates.ToArray()) +
                "; resourceId=" + DescribeStringState(image.resourceId) +
                "; duplicateResourceIdBeforeValidation=" + duplicateResourceIdBeforeValidation +
                "; status=" + DescribeStringState(image.status) +
                "; provenance=" + DescribeStringState(image.provenance) +
                "; referenceId=" + DescribeStringState(image.referenceId) +
                "; referencePath=" + DescribeStringState(image.referencePath) +
                "; referenceSha256=" + DescribeStringState(image.referenceSha256) +
                "; promptReference=" + DescribeStringState(image.promptReference) +
                "; promptReferenceSha256=" + DescribeStringState(image.promptReferenceSha256) +
                "; sourcePath=" + DescribeStringState(image.sourcePath) +
                "; duplicateSourcePathBeforeValidation=" + duplicateSourcePathBeforeValidation +
                "; assetFileName=" + DescribeStringState(image.assetFileName) +
                "; sha256=" + DescribeStringState(image.sha256) +
                "; width=" + image.width.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; height=" + image.height.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; bitDepth=" + image.bitDepth.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; colorType=" + image.colorType.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; sliceCount=" + sliceCount + "]";
        }

        private static string DescribeStringState(string value)
        {
            if (value == null) return "<null>";
            if (value.Length == 0) return "<empty:length=0>";
            if (string.IsNullOrWhiteSpace(value)) return "<whitespace:length=" +
                value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">";
            return "<value:length=" + value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">";
        }

        private static string DescribeClipDuration(
            AnimationClip clip, ClipSpec spec, AnimationClipSettings settings,
            ObjectReferenceKeyframe[] keys, float expectedDuration, string failedInvariant,
            string preCreateAssetMeasurements)
        {
            return "Existing F02 clip duration differs from its manifest: " + spec.name +
                " [failedInvariant=" + failedInvariant +
                "; current=" + CaptureClipMeasurements(clip, spec, settings, keys, expectedDuration) +
                "; preCreateAsset=" + (preCreateAssetMeasurements ?? "<not-created-in-this-call>") + "].";
        }

        private static string CaptureClipMeasurements(
            AnimationClip clip, ClipSpec spec, AnimationClipSettings settings,
            ObjectReferenceKeyframe[] keys, float expectedDuration)
        {
            string firstKeyTime = keys == null || keys.Length == 0 ? "<none>" : FormatClipTime(keys[0].time);
            string lastFrameKeyTime = keys == null || keys.Length == 0
                ? "<none>"
                : FormatClipTime(keys[keys.Length - 1].time);
            string keyCount = keys == null ? "<null>" : keys.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string keyTimes = keys == null
                ? "<null>"
                : "[" + string.Join(",", keys.Select((key, index) =>
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "@" + FormatClipTime(key.time)).ToArray()) + "]";
            return "{expectedDurationSeconds=" + FormatClipTime(expectedDuration) +
                "; durationBoundary=" + FormatClipTime(expectedDuration) +
                "; clip.length=" + FormatClipTime(clip.length) +
                "; frameRate=" + FormatClipTime(clip.frameRate) +
                "; frameCount=" + spec.frames.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; objectReferenceKeyCount=" + keyCount +
                "; startTime=" + FormatClipTime(settings.startTime) +
                "; stopTime=" + FormatClipTime(settings.stopTime) +
                "; firstKeyTime=" + firstKeyTime +
                "; lastFrameKeyTime=" + lastFrameKeyTime +
                "; keyTimes=" + keyTimes + "}";
        }

        private static string FormatClipTime(float value)
        {
            return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsFiniteAndApproximately(float actual, float expected)
        {
            return !float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Approximately(actual, expected);
        }

        private static void ValidateClipSet(string outputFullPath, FamilyManifest manifest)
        {
            var expected = new HashSet<string>(manifest.clips.Select(clip => clip.name + ".anim"), StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(outputFullPath, "*.anim", SearchOption.TopDirectoryOnly))
                if (!expected.Contains(Path.GetFileName(path)))
                    throw new InvalidDataException("Undeclared animation clip exists in the family output folder: " + path);
        }

        private static DestinationPlan PreflightDestinations(FamilyManifest manifest, string manifestSha256)
        {
            var plan = new DestinationPlan();
            string outputFullPath = ResolveProjectPath(manifest.outputFolder);
            plan.outputFolderExists = ValidateOutputFolderPath(manifest.outputFolder);
            PreflightDestinationReparsePoints(manifest, outputFullPath, plan.outputFolderExists);
            if (!plan.outputFolderExists)
            {
                if (File.Exists(outputFullPath) || File.Exists(outputFullPath + ".meta"))
                    throw new InvalidDataException("The first F02 output folder path is occupied by an unowned file or metadata.");
                foreach (string fileName in PlannedAssetNames(manifest))
                {
                    string target = Path.Combine(outputFullPath, fileName);
                    if (File.Exists(target) || File.Exists(target + ".meta"))
                        throw new InvalidDataException("A declared F02 destination exists before family ownership was established: " + target);
                }
                return plan;
            }

            string markerPath = manifest.outputFolder + "/" + OwnershipFileName;
            string markerFullPath = ResolveProjectPath(markerPath);
            if (!File.Exists(markerFullPath))
                throw new InvalidDataException("The existing F02 output folder has no ownership marker and was left untouched.");
            if (!File.Exists(markerFullPath + ".meta"))
                throw new InvalidDataException("The existing F02 ownership marker has no Unity metadata and was left untouched.");

            OwnershipManifest ownership = JsonUtility.FromJson<OwnershipManifest>(File.ReadAllText(markerFullPath, Encoding.UTF8));
            if (ownership == null || ownership.schemaVersion != OwnershipSchemaVersion)
                throw new InvalidDataException("The existing F02 ownership marker has no immutable publication fingerprint; preserve it and publish under a new versioned family identity and path.");
            if (
                !string.Equals(ownership.familyId, manifest.familyId, StringComparison.Ordinal) ||
                !string.Equals(ownership.outputFolder, manifest.outputFolder, StringComparison.Ordinal) ||
                !IsGuid(ownership.folderGuid) || !IsGuid(ownership.markerGuid) || ownership.assets == null)
                throw new InvalidDataException("The existing F02 ownership marker does not match this family and folder.");
            if (!string.Equals(ownership.manifestFingerprintAlgorithm, ManifestFingerprintAlgorithm, StringComparison.Ordinal) ||
                ownership.outputContractVersion != OutputContractVersion || !IsSha256(ownership.manifestSha256))
                throw new InvalidDataException("The existing F02 publication fingerprint is missing, unsupported, or malformed; the family was left untouched.");
            if (!string.Equals(ownership.manifestSha256, manifestSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 families are immutable: changed manifest or payload requires a new versioned familyId and outputFolder; the published family was left untouched.");
            if (!string.Equals(ReadGuidFromMeta(outputFullPath + ".meta"), ownership.folderGuid, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadGuidFromMeta(markerFullPath + ".meta"), ownership.markerGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 folder or ownership marker GUID differs from the recorded owner marker.");
            if (!IsSha256(ownership.folderMetaSha256) || !IsSha256(ownership.markerMetaSha256) ||
                !string.Equals(HashFile(outputFullPath + ".meta"), ownership.folderMetaSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(HashFile(markerFullPath + ".meta"), ownership.markerMetaSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The published F02 folder or ownership metadata differs from its immutable snapshot; the family was left untouched.");

            var expected = new HashSet<string>(PlannedAssetNames(manifest), StringComparer.OrdinalIgnoreCase);
            var recorded = new Dictionary<string, OwnedAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (OwnedAsset asset in ownership.assets)
            {
                if (asset == null || !IsSafeAssetName(asset.fileName) || !IsGuid(asset.guid) ||
                    !IsSha256(asset.contentSha256) || !IsSha256(asset.metaSha256) ||
                    !expected.Any(expectedName => string.Equals(expectedName, asset.fileName, StringComparison.Ordinal)) || recorded.ContainsKey(asset.fileName))
                    throw new InvalidDataException("The F02 ownership marker contains an invalid, duplicate, or no-longer-declared output.");
                recorded.Add(asset.fileName, asset);
            }

            // Marker records must be the exact manifest output set. A missing file and sidecar
            // cannot disappear from replay merely because their marker entry was also removed.
            if (recorded.Count != expected.Count ||
                expected.Any(fileName => !recorded.ContainsKey(fileName)))
                throw new InvalidDataException("The F02 ownership marker must exactly match the manifest output inventory.");

            var canonicalNames = expected.ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);
            canonicalNames.Add(OwnershipFileName, OwnershipFileName);
            var allowedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in canonicalNames.Keys)
            {
                allowedEntries.Add(name);
                allowedEntries.Add(name + ".meta");
            }
            foreach (string path in Directory.GetFiles(outputFullPath, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(path);
                if (!allowedEntries.Contains(name))
                    throw new InvalidDataException("Undeclared output artifact exists in the family folder and was left untouched: " + path);
                bool isMeta = name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
                string assetName = isMeta ? name.Substring(0, name.Length - ".meta".Length) : name;
                string expectedName;
                if (!canonicalNames.TryGetValue(assetName, out expectedName) ||
                    !string.Equals(name, isMeta ? expectedName + ".meta" : expectedName, StringComparison.Ordinal))
                    throw new InvalidDataException("An F02 destination differs only by case from a declared path; it was left untouched: " + path);
            }
            foreach (string path in Directory.GetDirectories(outputFullPath, "*", SearchOption.TopDirectoryOnly))
                throw new InvalidDataException("Undeclared output directory exists in the family folder and was left untouched: " + path);

            foreach (string fileName in expected)
            {
                string path = Path.Combine(outputFullPath, fileName);
                bool fileExists = File.Exists(path);
                bool metaExists = File.Exists(path + ".meta");
                OwnedAsset ownedAsset;
                bool isRecorded = recorded.TryGetValue(fileName, out ownedAsset);
                if (!fileExists || !metaExists || !isRecorded)
                    throw new InvalidDataException("Every manifest output, its .meta sidecar, and its ownership record must exist exactly once: " + path);
                if (fileExists)
                {
                    if (!string.Equals(ReadGuidFromMeta(path + ".meta"), ownedAsset.guid, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("An F02 asset GUID differs from the ownership marker and was left untouched: " + path);
                    plan.ownedFiles.Add(fileName);
                }
            }
            plan.ownership = ownership;
            ValidateExistingAtlasPackables(manifest);
            ValidateExistingClips(manifest, plan);
            foreach (OwnedAsset ownedAsset in ownership.assets)
            {
                string path = Path.Combine(outputFullPath, ownedAsset.fileName);
                if (!string.Equals(HashFile(path), ownedAsset.contentSha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(HashFile(path + ".meta"), ownedAsset.metaSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The published immutable F02 output differs from its recorded payload or importer settings and was left untouched: " + path);
            }
            plan.isPublishedReplay = true;
            return plan;
        }

        private static void PreflightDestinationReparsePoints(FamilyManifest manifest, string outputFullPath,
            bool outputFolderExists)
        {
            RejectReparsePoint(outputFullPath + ".meta", "F02 destination is a filesystem link");
            foreach (string fileName in PlannedAssetNames(manifest).Concat(new[] { OwnershipFileName }))
            {
                string destination = Path.Combine(outputFullPath, fileName);
                RejectReparsePoint(destination, "F02 destination is a filesystem link");
                RejectReparsePoint(destination + ".meta", "F02 destination is a filesystem link");
            }

            if (!outputFolderExists) return;
            foreach (string entry in Directory.GetFileSystemEntries(outputFullPath, "*", SearchOption.TopDirectoryOnly))
                RejectReparsePoint(entry, "F02 destination is a filesystem link");
        }

        private static void PreflightCanonicalPathAliases(FamilyManifest manifest)
        {
            var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string outputFullPath = ResolveProjectPath(manifest.outputFolder);
            AddUniqueCanonicalOutputPath(outputPaths, outputFullPath + ".meta");
            foreach (string fileName in PlannedAssetNames(manifest).Concat(new[] { OwnershipFileName }))
            {
                string fullPath = ResolveProjectPath(manifest.outputFolder + "/" + fileName);
                AddUniqueCanonicalOutputPath(outputPaths, fullPath);
                AddUniqueCanonicalOutputPath(outputPaths, fullPath + ".meta");
            }

            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageSource image in manifest.images)
            {
                string sourcePath = ResolveProjectPath(image.sourcePath);
                string canonicalSource = CanonicalPathKey(sourcePath);
                if (!sourcePaths.Add(canonicalSource))
                    throw new InvalidDataException("F02 source paths alias after canonicalization.");
                if (outputPaths.Contains(canonicalSource))
                    throw new InvalidDataException("An F02 source aliases a planned family output path.");
            }
        }

        private static void AddUniqueCanonicalOutputPath(HashSet<string> outputPaths, string fullPath)
        {
            if (!outputPaths.Add(CanonicalPathKey(fullPath)))
                throw new InvalidDataException("F02 planned output paths collide after case-insensitive canonicalization.");
        }

        private static string CanonicalPathKey(string fullPath)
        {
            string canonical = Path.GetFullPath(fullPath);
            return canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static IEnumerable<string> PlannedAssetNames(FamilyManifest manifest)
        {
            foreach (ImageSource image in manifest.images) yield return image.assetFileName;
            foreach (ClipSpec clip in manifest.clips) yield return clip.name + ".anim";
            yield return manifest.familyId + ".spriteatlas";
        }

        private static string ComputeCanonicalManifestSha256(FamilyManifest manifest)
        {
            // JsonUtility parses object properties into typed fields, so source property order and
            // whitespace do not affect this identity. Normalize only collections that the importer
            // itself consumes in a deterministic set order; clip frame order remains significant.
            manifest.referenceSha256 = NormalizeSha256(manifest.referenceSha256);
            manifest.promptReferenceSha256 = NormalizeSha256(manifest.promptReferenceSha256);
            manifest.images = manifest.images.OrderBy(image => image.assetFileName, StringComparer.Ordinal).ToArray();
            foreach (ImageSource image in manifest.images)
            {
                image.referenceSha256 = NormalizeSha256(image.referenceSha256);
                image.promptReferenceSha256 = NormalizeSha256(image.promptReferenceSha256);
                image.sha256 = NormalizeSha256(image.sha256);
                image.slices = image.slices
                    .OrderBy(slice => slice.y)
                    .ThenBy(slice => slice.x)
                    .ThenBy(slice => slice.name, StringComparer.Ordinal)
                    .ToArray();
            }
            manifest.clips = manifest.clips.OrderBy(clip => clip.name, StringComparer.Ordinal).ToArray();

            string canonicalJson = JsonUtility.ToJson(manifest, false);
            string fingerprintInput = ManifestFingerprintAlgorithm + "\n" +
                OutputContractVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                canonicalJson;
            using (SHA256 sha256 = SHA256.Create())
                return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprintInput))
                    .Select(value => value.ToString("x2")));
        }

        private static string NormalizeSha256(string value)
        {
            return value.ToLowerInvariant();
        }

        private static void ValidateExistingAtlasPackables(FamilyManifest manifest)
        {
            string atlasPath = manifest.outputFolder + "/" + manifest.familyId + ".spriteatlas";
            if (!File.Exists(ResolveProjectPath(atlasPath))) return;
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
                throw new InvalidDataException("An owned F02 atlas cannot be loaded as a SpriteAtlas and was left untouched.");
            var expectedPaths = new HashSet<string>(manifest.images.Select(image =>
                manifest.outputFolder + "/" + image.assetFileName), StringComparer.OrdinalIgnoreCase);
            UnityEngine.Object[] packables = SpriteAtlasExtensions.GetPackables(atlas) ?? Array.Empty<UnityEngine.Object>();
            foreach (UnityEngine.Object packable in packables)
            {
                string path = AssetDatabase.GetAssetPath(packable);
                if (string.IsNullOrWhiteSpace(path) || !expectedPaths.Contains(path) ||
                    !expectedPaths.Any(expected => string.Equals(expected, path, StringComparison.Ordinal)))
                    throw new InvalidDataException("An existing F02 atlas contains a foreign or case-aliased packable and was left untouched: " + path);
            }
        }

        private static void ValidateExistingClips(FamilyManifest manifest, DestinationPlan plan)
        {
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (ImageSource image in manifest.images)
            {
                string assetPath = manifest.outputFolder + "/" + image.assetFileName;
                if (!File.Exists(ResolveProjectPath(assetPath))) continue;
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>())
                    if (!sprites.ContainsKey(sprite.name)) sprites.Add(sprite.name, sprite);
            }
            foreach (ClipSpec spec in manifest.clips)
            {
                string fileName = spec.name + ".anim";
                if (!plan.Owns(fileName)) continue;
                string assetPath = manifest.outputFolder + "/" + fileName;
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                    throw new InvalidDataException("An owned F02 clip cannot be loaded and was left untouched: " + assetPath);
                ValidateClip(clip, spec, sprites);
            }
        }

        private static bool ValidateOutputFolderPath(string assetFolderPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (string segment in assetFolderPath.Split('/'))
            {
                string match = FindCaseInsensitiveEntry(projectRoot, segment);
                if (match == null) return false;
                string actualName = Path.GetFileName(match);
                if (!string.Equals(actualName, segment, StringComparison.Ordinal))
                    throw new InvalidDataException("An F02 destination folder differs only by case from an existing path: " + match);
                RejectReparsePoint(match, "F02 output folder path traverses a filesystem link");
                if (!Directory.Exists(match))
                    throw new InvalidDataException("An F02 destination folder path is occupied by a file: " + match);
                projectRoot = match;
            }
            return true;
        }

        private static string FindCaseInsensitiveEntry(string parent, string name)
        {
            if (!Directory.Exists(parent)) return null;
            string[] matches = Directory.GetFileSystemEntries(parent).Where(path =>
                string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1)
                throw new InvalidDataException("Multiple existing F02 destination entries collide on Windows when compared without case: " + name);
            return matches.SingleOrDefault();
        }

        private static void ValidateOutputFileNames(FamilyManifest manifest)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in PlannedAssetNames(manifest).Concat(new[] { OwnershipFileName }))
                if (!names.Add(name))
                    throw new InvalidDataException("F02 output file paths collide on Windows when compared without case: " + name);
        }

        private static string CanonicalizeOutputFolderPath(string value)
        {
            string[] segments = value.Split('/');
            if (segments.Length < 2 || segments[0] != "Assets" ||
                segments.Skip(1).Any(segment => !IsSafeAssetName(segment)))
                throw new InvalidDataException("F02 output path contains an invalid Windows path segment.");

            var uniqueSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string segment in segments)
                if (!uniqueSegments.Add(segment))
                    throw new InvalidDataException("F02 output folder segments collide on Windows when compared without case.");

            string canonicalAssetPath = string.Join("/", segments);
            string canonicalFullPath = CanonicalPathKey(ResolveProjectPath(canonicalAssetPath));
            string assetsFullPath = CanonicalPathKey(ResolveProjectPath("Assets"));
            string assetsPrefix = assetsFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!canonicalFullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 output path escapes the canonical Assets directory.");
            ValidateOutputFolderPath(canonicalAssetPath);
            return canonicalAssetPath;
        }

        private static void RejectReparsePointsInProjectPath(string projectRelativePath)
        {
            string current = Directory.GetParent(Application.dataPath).FullName;
            foreach (string segment in projectRelativePath.Split('/'))
            {
                current = Path.Combine(current, segment);
                if (File.Exists(current) || Directory.Exists(current))
                    RejectReparsePoint(current, "F02 source path traverses a filesystem link");
            }
        }

        private static void RejectReparsePoint(string fullPath, string message)
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
                throw new InvalidDataException(message + ": " + fullPath);
        }

        private static string ReadGuidFromMeta(string metaPath)
        {
            if (!File.Exists(metaPath)) return string.Empty;
            foreach (string line in File.ReadAllLines(metaPath))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("guid:", StringComparison.Ordinal)) continue;
                string guid = trimmed.Substring("guid:".Length).Trim();
                return guid.Length == 32 && guid.All(Uri.IsHexDigit) ? guid : string.Empty;
            }
            return string.Empty;
        }

        private static void WriteOwnershipManifest(FamilyManifest manifest, string manifestSha256)
        {
            string outputFullPath = ResolveProjectPath(manifest.outputFolder);
            string folderGuid = ReadGuidFromMeta(outputFullPath + ".meta");
            if (!IsGuid(folderGuid))
                throw new InvalidOperationException("Unity did not create a stable GUID for the F02 family output folder.");

            string markerPath = manifest.outputFolder + "/" + OwnershipFileName;
            string markerFullPath = ResolveProjectPath(markerPath);
            if (!File.Exists(markerFullPath))
            {
                File.WriteAllText(markerFullPath, "{}", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(markerPath, ImportAssetOptions.ForceSynchronousImport);
            }
            string markerGuid = ReadGuidFromMeta(markerFullPath + ".meta");
            if (!IsGuid(markerGuid))
                throw new InvalidOperationException("Unity did not create a stable GUID for the F02 ownership marker.");

            var assets = new List<OwnedAsset>();
            foreach (string fileName in PlannedAssetNames(manifest).OrderBy(name => name, StringComparer.Ordinal))
            {
                string assetFullPath = Path.Combine(outputFullPath, fileName);
                string guid = ReadGuidFromMeta(assetFullPath + ".meta");
                if (!File.Exists(assetFullPath) || !IsGuid(guid))
                    throw new InvalidOperationException("Unity did not persist owned F02 output metadata: " + fileName);
                assets.Add(new OwnedAsset
                {
                    fileName = fileName,
                    guid = guid,
                    contentSha256 = HashFile(assetFullPath),
                    metaSha256 = HashFile(assetFullPath + ".meta")
                });
            }

            OwnershipManifest ownership = new OwnershipManifest
            {
                schemaVersion = OwnershipSchemaVersion,
                familyId = manifest.familyId,
                outputFolder = manifest.outputFolder,
                folderGuid = folderGuid,
                markerGuid = markerGuid,
                manifestFingerprintAlgorithm = ManifestFingerprintAlgorithm,
                outputContractVersion = OutputContractVersion,
                manifestSha256 = manifestSha256,
                folderMetaSha256 = HashFile(outputFullPath + ".meta"),
                markerMetaSha256 = HashFile(markerFullPath + ".meta"),
                assets = assets.ToArray()
            };
            string json = JsonUtility.ToJson(ownership, true) + "\n";
            string current = File.ReadAllText(markerFullPath, Encoding.UTF8);
            if (!string.Equals(current, json, StringComparison.Ordinal))
            {
                File.WriteAllText(markerFullPath, json, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(markerPath, ImportAssetOptions.ForceSynchronousImport);
            }
            if (!string.Equals(ReadGuidFromMeta(markerFullPath + ".meta"), markerGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unity changed the F02 ownership marker GUID while updating it.");
            if (!string.Equals(HashFile(markerFullPath + ".meta"), ownership.markerMetaSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unity changed the F02 ownership marker metadata after its immutable snapshot was recorded.");
        }

        private static void EnsureFamilyAtlas(FamilyManifest manifest, IReadOnlyCollection<Texture2D> textures, bool alreadyOwned)
        {
            string atlasPath = manifest.outputFolder + "/" + manifest.familyId + ".spriteatlas";
            string fullPath = ResolveProjectPath(atlasPath);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            bool isNew = !File.Exists(fullPath);
            if (!isNew && !alreadyOwned)
                throw new InvalidDataException("A declared F02 atlas exists without matching family ownership and was left untouched.");
            if (!isNew && atlas == null)
                throw new InvalidDataException("The owned F02 atlas cannot be loaded and was left untouched.");
            if (isNew) atlas = new SpriteAtlas();

            bool changed = ReconcilePackables(atlas, textures);
            SpriteAtlasPackingSettings packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            if (packing.enableRotation || packing.enableTightPacking || packing.padding != manifest.atlas.padding ||
                packing.enableAlphaDilation || packing.blockOffset != 1)
            {
                packing.enableRotation = false;
                packing.enableTightPacking = false;
                packing.padding = manifest.atlas.padding;
                packing.enableAlphaDilation = false;
                packing.blockOffset = 1;
                SpriteAtlasExtensions.SetPackingSettings(atlas, packing);
                changed = true;
            }

            SpriteAtlasTextureSettings textureSettings = SpriteAtlasExtensions.GetTextureSettings(atlas);
            if (textureSettings.readable || textureSettings.generateMipMaps || textureSettings.filterMode != FilterMode.Point ||
                textureSettings.anisoLevel != 0)
            {
                textureSettings.readable = false;
                textureSettings.generateMipMaps = false;
                textureSettings.filterMode = FilterMode.Point;
                textureSettings.anisoLevel = 0;
                SpriteAtlasExtensions.SetTextureSettings(atlas, textureSettings);
                changed = true;
            }
            if (SpriteAtlasExtensions.IsIncludeInBuild(atlas) != manifest.atlas.includeInBuild)
            {
                SpriteAtlasExtensions.SetIncludeInBuild(atlas, manifest.atlas.includeInBuild);
                changed = true;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(atlas, atlasPath);
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssetIfDirty(atlas);
            }
            ValidateAtlas(atlas, manifest, textures);
        }

        private static bool ReconcilePackables(SpriteAtlas atlas, IReadOnlyCollection<Texture2D> textures)
        {
            UnityEngine.Object[] expected = textures.OrderBy(item => AssetDatabase.GetAssetPath(item), StringComparer.Ordinal)
                .Cast<UnityEngine.Object>().ToArray();
            UnityEngine.Object[] actual = SpriteAtlasExtensions.GetPackables(atlas) ?? Array.Empty<UnityEngine.Object>();
            string[] actualPaths = actual.Select(AssetDatabase.GetAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string actualPath in actualPaths)
                if (!expected.Any(item => string.Equals(AssetDatabase.GetAssetPath(item), actualPath, StringComparison.Ordinal)))
                    throw new InvalidDataException("F02 atlas reconciliation cannot remove a non-family packable: " + actualPath);
            UnityEngine.Object[] missing = expected.Where(item =>
                !actualPaths.Contains(AssetDatabase.GetAssetPath(item), StringComparer.Ordinal)).ToArray();
            if (missing.Length == 0) return false;
            SpriteAtlasExtensions.Add(atlas, missing);
            return true;
        }

        private static bool PackablesMatch(UnityEngine.Object[] actual, UnityEngine.Object[] expected)
        {
            if (actual.Length != expected.Length) return false;
            string[] left = actual.Select(AssetDatabase.GetAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] right = expected.Select(AssetDatabase.GetAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            return left.SequenceEqual(right, StringComparer.Ordinal);
        }

        private static void ValidateAtlas(SpriteAtlas atlas, FamilyManifest manifest, IReadOnlyCollection<Texture2D> textures)
        {
            UnityEngine.Object[] expected = textures.Cast<UnityEngine.Object>().ToArray();
            SpriteAtlasPackingSettings packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            SpriteAtlasTextureSettings textureSettings = SpriteAtlasExtensions.GetTextureSettings(atlas);
            if (!PackablesMatch(SpriteAtlasExtensions.GetPackables(atlas), expected) ||
                packing.enableRotation || packing.enableTightPacking || packing.padding != manifest.atlas.padding ||
                packing.enableAlphaDilation || packing.blockOffset != 1 || textureSettings.readable ||
                textureSettings.generateMipMaps || textureSettings.filterMode != FilterMode.Point ||
                textureSettings.anisoLevel != 0 ||
                SpriteAtlasExtensions.IsIncludeInBuild(atlas) != manifest.atlas.includeInBuild)
                throw new InvalidOperationException("Unity did not retain deterministic F02 family atlas members and settings.");
            if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(atlas))))
                throw new InvalidOperationException("Unity did not assign a stable family atlas GUID.");
        }

        private const string InvalidWindowsAssetNameCharacters = "<>:\"/\\|?*";

        private static bool IsSafeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Any(character => char.IsControl(character) ||
                    InvalidWindowsAssetNameCharacters.IndexOf(character) >= 0) ||
                value != value.TrimEnd(' ', '.') || value == "." || value == "..") return false;

            // Windows reserves these DOS device stems even when a filename extension follows.
            string upperStem = value.Split('.')[0].TrimEnd(' ', '.').ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5",
                "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
                "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³" };
            return !reserved.Contains(upperStem);
        }

        private static bool IsSafePngName(string value)
        {
            return IsSafeAssetName(value) && string.Equals(Path.GetExtension(value), ".png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAssetFolderPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\\') >= 0 ||
                !value.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            return value.Split('/').All(part => !string.IsNullOrWhiteSpace(part) && part != "." && part != "..");
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            string[] segments = assetFolderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[i]);
                    if (string.IsNullOrWhiteSpace(guid))
                        throw new IOException("Unity could not create the declared F02 family folder: " + next);
                }
                current = next;
            }
            if (!Directory.Exists(ResolveProjectPath(assetFolderPath)))
                throw new IOException("The declared F02 family output folder is unavailable: " + assetFolderPath);
        }

        private static void ValidateProjectRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 ||
                value.StartsWith("/", StringComparison.Ordinal) || value.Split('/').Any(part =>
                    !IsSafeAssetName(part)))
                throw new InvalidDataException("F02 manifest paths must be safe project-relative paths.");
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            ValidateProjectRelativePath(projectRelativePath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("F02 manifest path escaped the Unity project root.");
            return fullPath;
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F'));
        }

        private static bool IsGuid(string value)
        {
            return value != null && value.Length == 32 && value.All(Uri.IsHexDigit);
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
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
    }
}
