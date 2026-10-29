using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Luckbound.Core.Expeditions
{
    public static class EncounterCatalogCanonicalV1
    {
        private const string ManifestDomain = "Luckbound.EncounterCatalog.v1";
        private const string DefinitionDomain = "Luckbound.EncounterDefinition.v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const string LowerHexDigits = "0123456789abcdef";

        public static byte[] EncodeManifest(EncounterCatalogManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var writer = new CanonicalWriter(ManifestDomain);
            writer.WriteUInt32(manifest.SchemaVersion);
            writer.WriteString(manifest.CatalogId);
            writer.WriteUInt32(manifest.CatalogRevision);
            writer.WriteString(manifest.IdentityCodec);
            writer.WriteString(manifest.ResolverId);
            writer.WriteUInt32(manifest.ResolverRevision);

            int[] generations = manifest.SupportedRouteGenerationVersions.OrderBy(value => value).ToArray();
            writer.WriteCount(generations.Length);
            for (int index = 0; index < generations.Length; index++)
                writer.WriteUInt32(generations[index]);

            EncounterCatalogDescriptor[] descriptors = manifest.DescriptorRefs
                .OrderBy(item => item.DescriptorId, StringComparer.Ordinal)
                .ThenBy(item => item.Revision)
                .ToArray();
            writer.WriteCount(descriptors.Length);
            for (int index = 0; index < descriptors.Length; index++)
            {
                EncounterCatalogDescriptor descriptor = descriptors[index];
                writer.WriteString(descriptor.DescriptorId);
                writer.WriteUInt32(descriptor.Revision);
                writer.WriteString(EncounterCatalogTokensV1.GetToken(descriptor.Kind));
                writer.WriteString(descriptor.Sha256);
            }

            EncounterCatalogBinding[] bindings = manifest.Bindings
                .OrderBy(item => item.GenerationVersion)
                .ThenBy(item => item.TemplateId, StringComparer.Ordinal)
                .ThenBy(item => EncounterCatalogTokensV1.GetToken(item.NodeKind), StringComparer.Ordinal)
                .ToArray();
            writer.WriteCount(bindings.Length);
            for (int index = 0; index < bindings.Length; index++)
            {
                EncounterCatalogBinding binding = bindings[index];
                writer.WriteUInt32(binding.GenerationVersion);
                writer.WriteString(binding.TemplateId);
                writer.WriteString(EncounterCatalogTokensV1.GetToken(binding.NodeKind));
                writer.WriteString(EncounterCatalogTokensV1.GetToken(binding.Mode));
                bool hasEncounter = binding.Mode == EncounterCatalogBindingMode.Encounter;
                writer.WriteOptionalMarker(hasEncounter);
                if (hasEncounter)
                {
                    writer.WriteString(binding.EncounterId);
                    writer.WriteUInt32(binding.DefinitionRevision);
                }
            }

            EncounterCatalogDefinition[] definitions = manifest.Definitions
                .OrderBy(item => item.EncounterId, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionRevision)
                .ToArray();
            writer.WriteCount(definitions.Length);
            for (int index = 0; index < definitions.Length; index++)
                WriteDefinitionRecord(writer, definitions[index]);

            return writer.ToArray();
        }

        public static string ComputeManifestSha256(EncounterCatalogManifest manifest)
        {
            return ComputeSha256(EncodeManifest(manifest));
        }

        public static byte[] EncodeDefinition(EncounterCatalogDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var writer = new CanonicalWriter(DefinitionDomain);
            WriteDefinitionRecord(writer, definition);
            return writer.ToArray();
        }

        public static string ComputeDefinitionSha256(EncounterCatalogDefinition definition)
        {
            return ComputeSha256(EncodeDefinition(definition));
        }

        private static void WriteDefinitionRecord(CanonicalWriter writer,
            EncounterCatalogDefinition definition)
        {
            writer.WriteString(definition.EncounterId);
            writer.WriteUInt32(definition.DefinitionRevision);

            EncounterCatalogVariant[] variants = definition.Variants
                .OrderBy(item => item.VariantId, StringComparer.Ordinal)
                .ToArray();
            writer.WriteCount(variants.Length);
            for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++)
                WriteVariant(writer, variants[variantIndex]);
        }

        private static void WriteVariant(CanonicalWriter writer, EncounterCatalogVariant variant)
        {
            writer.WriteString(variant.VariantId);
            WriteStringSet(writer, variant.RequiredPactIds);
            WriteStringSet(writer, variant.ForbiddenPactIds);
            writer.WriteUInt32(variant.ScheduledVinculoOrdinal);
            WriteDescriptorRef(writer, variant.RoomDescriptorRef);

            writer.WriteCount(variant.Waves.Count);
            for (int waveIndex = 0; waveIndex < variant.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave wave = variant.Waves[waveIndex];
                writer.WriteString(wave.WaveId);
                EncounterCatalogSpawn[] spawns = wave.Spawns
                    .OrderBy(item => item.SpawnId, StringComparer.Ordinal)
                    .ToArray();
                writer.WriteCount(spawns.Length);
                for (int spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex++)
                {
                    EncounterCatalogSpawn spawn = spawns[spawnIndex];
                    writer.WriteString(spawn.SpawnId);
                    writer.WriteString(spawn.EnemyId);
                    WriteDescriptorRef(writer, spawn.EnemyDescriptorRef);
                    writer.WriteString(spawn.SpawnAnchorId);
                    writer.WriteString(EncounterCatalogTokensV1.GetToken(spawn.RewardKind));
                    writer.WriteUInt32(spawn.VinculoSequenceOrdinal);
                }
            }

            writer.WriteOptionalMarker(variant.Cache != null);
            if (variant.Cache != null)
            {
                writer.WriteString(variant.Cache.CacheId);
                WriteDescriptorRef(writer, variant.Cache.CacheDescriptorRef);
                writer.WriteString(variant.Cache.AnchorId);
            }
        }

        private static void WriteStringSet(CanonicalWriter writer, IReadOnlyList<string> values)
        {
            string[] sorted = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            writer.WriteCount(sorted.Length);
            for (int index = 0; index < sorted.Length; index++)
                writer.WriteString(sorted[index]);
        }

        private static void WriteDescriptorRef(CanonicalWriter writer,
            EncounterCatalogDescriptorRef descriptorRef)
        {
            writer.WriteString(descriptorRef.DescriptorId);
            writer.WriteUInt32(descriptorRef.Revision);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
                digest = sha256.ComputeHash(bytes);
            var hex = new char[digest.Length * 2];
            for (int index = 0; index < digest.Length; index++)
            {
                hex[index * 2] = LowerHexDigits[digest[index] >> 4];
                hex[index * 2 + 1] = LowerHexDigits[digest[index] & 0x0f];
            }
            return new string(hex);
        }

        private sealed class CanonicalWriter
        {
            private readonly MemoryStream _stream = new MemoryStream();

            internal CanonicalWriter(string domain)
            {
                byte[] domainBytes = Encoding.ASCII.GetBytes(domain);
                _stream.Write(domainBytes, 0, domainBytes.Length);
                _stream.WriteByte(0);
            }

            internal void WriteOptionalMarker(bool present)
            {
                _stream.WriteByte(present ? (byte)1 : (byte)0);
            }

            internal void WriteCount(int count)
            {
                if (count < 0) throw new OverflowException("Collection count cannot be negative.");
                WriteUInt32((uint)count);
            }

            internal void WriteUInt32(int value)
            {
                if (value < 0) throw new OverflowException("Canonical UInt32 values cannot be negative.");
                WriteUInt32((uint)value);
            }

            internal void WriteUInt32(uint value)
            {
                _stream.WriteByte((byte)(value >> 24));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)value);
            }

            internal void WriteString(string value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                byte[] bytes = StrictUtf8.GetBytes(value);
                if ((ulong)bytes.LongLength > uint.MaxValue)
                    throw new OverflowException("Canonical UTF-8 string exceeds the UInt32 framing limit.");
                WriteUInt32((uint)bytes.Length);
                _stream.Write(bytes, 0, bytes.Length);
            }

            internal byte[] ToArray()
            {
                return _stream.ToArray();
            }
        }
    }
}
