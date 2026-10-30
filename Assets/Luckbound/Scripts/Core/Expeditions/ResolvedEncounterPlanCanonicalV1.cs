using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Luckbound.Core.Expeditions
{
    public static class ResolvedEncounterPlanCanonicalV1
    {
        private const string EncounterDomain = "Luckbound.ResolvedEncounterPlan.v1";
        private const string NoneDomain = "Luckbound.NoEncounterBinding.v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(ResolvedEncounterPlanV1 plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            if (plan.Mode == EncounterCatalogBindingMode.None)
                return EncodeNone(plan);
            if (plan.Mode != EncounterCatalogBindingMode.Encounter)
                throw new ArgumentOutOfRangeException(nameof(plan), "Resolved plan mode is not supported.");

            var writer = new CanonicalWriter(EncounterDomain);
            writer.WriteString(plan.CatalogDigest);
            writer.WriteString(plan.DefinitionDigest);
            writer.WriteString(plan.RouteDigest);
            writer.WriteString(plan.RunId.ToString());
            writer.WriteString(plan.Seed.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteUInt32(plan.GenerationVersion);
            writer.WriteString(plan.NodeId);
            writer.WriteString(plan.TemplateId);
            writer.WriteString(plan.RoomInstanceId.ToString());
            writer.WriteString(plan.EncounterId);
            writer.WriteUInt32(plan.DefinitionRevision);
            writer.WriteString(plan.VariantId);
            WriteResolutionContext(writer, plan);
            WriteVariant(writer, plan.SelectedVariant);
            WriteUsedDescriptors(writer, plan);
            return writer.ToArray();
        }

        public static string ComputeSha256(ResolvedEncounterPlanV1 plan)
        {
            byte[] preimage = Encode(plan);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create()) digest = sha256.ComputeHash(preimage);
            return ToLowerHex(digest);
        }

        private static byte[] EncodeNone(ResolvedEncounterPlanV1 plan)
        {
            var writer = new CanonicalWriter(NoneDomain);
            writer.WriteString(plan.CatalogDigest);
            writer.WriteString(plan.RouteDigest);
            writer.WriteString(plan.RunId.ToString());
            writer.WriteString(plan.Seed.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteUInt32(plan.GenerationVersion);
            writer.WriteString(plan.NodeId);
            writer.WriteString(plan.TemplateId);
            writer.WriteString(plan.RoomInstanceId.ToString());
            writer.WriteString(EncounterCatalogTokensV1.GetToken(EncounterCatalogBindingMode.None));
            WriteResolutionContext(writer, plan);
            return writer.ToArray();
        }

        private static void WriteResolutionContext(CanonicalWriter writer, ResolvedEncounterPlanV1 plan)
        {
            string[] pactIds = plan.ContextPactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            writer.WriteCount(pactIds.Length);
            for (int index = 0; index < pactIds.Length; index++) writer.WriteString(pactIds[index]);
            // Marker 0 means the no-Vínculo calendar is explicitly not applicable.
            writer.WriteOptionalMarker(false);
        }

        private static void WriteVariant(CanonicalWriter writer, EncounterCatalogVariant variant)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));

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

        private static void WriteStringSet(CanonicalWriter writer, System.Collections.Generic.IReadOnlyList<string> values)
        {
            string[] sorted = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            writer.WriteCount(sorted.Length);
            for (int index = 0; index < sorted.Length; index++) writer.WriteString(sorted[index]);
        }

        private static void WriteDescriptorRef(CanonicalWriter writer, EncounterCatalogDescriptorRef descriptorRef)
        {
            writer.WriteString(descriptorRef.DescriptorId);
            writer.WriteUInt32(descriptorRef.Revision);
        }

        private static void WriteUsedDescriptors(CanonicalWriter writer, ResolvedEncounterPlanV1 plan)
        {
            EncounterCatalogDescriptor[] descriptors = plan.UsedDescriptors
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
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[index * 2 + 1] = digits[bytes[index] & 0x0f];
            }
            return new string(characters);
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
                if (count < 0) throw new OverflowException("Canonical collection count cannot be negative.");
                WriteUInt32((uint)count);
            }

            internal void WriteUInt32(int value)
            {
                if (value < 0) throw new OverflowException("Canonical UInt32 value cannot be negative.");
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
