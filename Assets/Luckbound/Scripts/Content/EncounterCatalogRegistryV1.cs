using System;
using System.Collections.Generic;
using System.Text;
using Luckbound.Core.Expeditions;

namespace Luckbound.Content
{
    public sealed class EncounterCatalogRegistryV1
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly Dictionary<CatalogRevisionKey, RegistryEntry> _entries;

        internal EncounterCatalogRegistryV1(Dictionary<CatalogRevisionKey, RegistryEntry> entries)
        {
            _entries = new Dictionary<CatalogRevisionKey, RegistryEntry>(entries);
        }

        public static bool TryCreate(IEnumerable<EncounterCatalogManifest> manifests,
            out EncounterCatalogRegistryV1 registry, out EncounterCatalogError error)
        {
            registry = null;
            if (manifests == null)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.NullInput,
                    "manifests", "Manifest collection is required.");
                return false;
            }

            var entries = new Dictionary<CatalogRevisionKey, RegistryEntry>();
            int index = 0;
            foreach (EncounterCatalogManifest manifest in manifests)
            {
                if (manifest == null)
                {
                    error = new EncounterCatalogError(EncounterCatalogErrorCode.NullInput,
                        IndexPath("manifests", index), "Manifest entries cannot be null.");
                    return false;
                }

                byte[] canonicalBytes = EncounterCatalogCanonicalV1.EncodeManifest(manifest);
                string digest = EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest);
                var key = new CatalogRevisionKey(manifest.CatalogId, manifest.CatalogRevision);
                if (entries.TryGetValue(key, out RegistryEntry existing))
                {
                    if (!string.Equals(existing.Digest, digest, StringComparison.Ordinal)
                        || !BytesEqual(existing.CanonicalBytes, canonicalBytes))
                    {
                        error = new EncounterCatalogError(EncounterCatalogErrorCode.CatalogRevisionConflict,
                            IndexPath("manifests", index),
                            "The same catalog ID and revision have different canonical content.");
                        return false;
                    }
                }
                else
                {
                    entries.Add(key, new RegistryEntry(manifest, digest, canonicalBytes));
                }
                index++;
            }

            registry = new EncounterCatalogRegistryV1(entries);
            error = default(EncounterCatalogError);
            return true;
        }

        public bool TryGetExact(string catalogId, int catalogRevision, string catalogSha256,
            out EncounterCatalogManifest manifest, out EncounterCatalogError error)
        {
            manifest = null;
            if (!IsStableText(catalogId))
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                    "catalogId", "Catalog ID must be non-empty stable text.");
                return false;
            }
            if (catalogRevision <= 0)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                    "catalogRevision", "Catalog revision must be positive.");
                return false;
            }
            if (!IsLowerSha256(catalogSha256))
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                    "catalogSha256", "Catalog digest must be 64 lowercase hexadecimal characters.");
                return false;
            }

            var key = new CatalogRevisionKey(catalogId, catalogRevision);
            if (_entries.TryGetValue(key, out RegistryEntry entry)
                && string.Equals(entry.Digest, catalogSha256, StringComparison.Ordinal))
            {
                manifest = entry.Manifest;
                error = default(EncounterCatalogError);
                return true;
            }

            error = new EncounterCatalogError(EncounterCatalogErrorCode.CatalogRevisionUnavailable,
                "catalogRevision", "The exact catalog ID, revision, and digest are unavailable.");
            return false;
        }

        private static bool IsStableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsWhiteSpace(value[index])) return false;
            }
            try
            {
                StrictUtf8.GetByteCount(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        private static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }

        private static string IndexPath(string path, int index)
        {
            return path + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
        }

        internal sealed class RegistryEntry
        {
            internal RegistryEntry(EncounterCatalogManifest manifest, string digest, byte[] canonicalBytes)
            {
                Manifest = manifest;
                Digest = digest;
                CanonicalBytes = (byte[])canonicalBytes.Clone();
            }

            internal EncounterCatalogManifest Manifest { get; }
            internal string Digest { get; }
            internal byte[] CanonicalBytes { get; }
        }

        internal readonly struct CatalogRevisionKey : IEquatable<CatalogRevisionKey>
        {
            internal CatalogRevisionKey(string catalogId, int catalogRevision)
            {
                CatalogId = catalogId;
                CatalogRevision = catalogRevision;
            }

            private string CatalogId { get; }
            private int CatalogRevision { get; }

            public bool Equals(CatalogRevisionKey other)
            {
                return CatalogRevision == other.CatalogRevision
                    && string.Equals(CatalogId, other.CatalogId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CatalogRevisionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(CatalogId) * 397) ^ CatalogRevision;
                }
            }
        }
    }
}
