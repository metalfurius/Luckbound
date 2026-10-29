using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Expeditions
{
    /// <summary>
    /// A canonical, deterministic encoding of the durable tuple for one encounter spawn.
    /// This value does not prove that the encounter or spawn belongs to an authorized plan.
    /// </summary>
    public sealed class EncounterSpawnIdentityV1
    {
        private const string KeyPrefix = "encounter-spawn.v1:";
        private const string Domain = "Luckbound.EncounterSpawn.v1";
        private const int FieldCount = 6;
        private const int DigestByteCount = 32;
        private const int DigestHexLength = DigestByteCount * 2;

        private static readonly byte[] DomainBytes = Encoding.ASCII.GetBytes(Domain + "\0");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const string LowerHexDigits = "0123456789abcdef";

        private EncounterSpawnIdentityV1(
            RunId runId,
            uint routeGenerationVersion,
            string nodeId,
            RoomInstanceId roomInstanceId,
            string encounterId,
            string localSpawnId,
            string key)
        {
            RunId = runId;
            RouteGenerationVersion = routeGenerationVersion;
            NodeId = nodeId;
            RoomInstanceId = roomInstanceId;
            EncounterId = encounterId;
            LocalSpawnId = localSpawnId;
            Key = key;
        }

        public RunId RunId { get; }

        public uint RouteGenerationVersion { get; }

        public string NodeId { get; }

        public RoomInstanceId RoomInstanceId { get; }

        public string EncounterId { get; }

        public string LocalSpawnId { get; }

        public string Key { get; }

        /// <summary>Creates a codec value after validating the canonical tuple structure.</summary>
        public static EncounterSpawnIdentityV1 Create(
            RunId runId,
            uint routeGenerationVersion,
            string nodeId,
            RoomInstanceId roomInstanceId,
            string encounterId,
            string localSpawnId)
        {
            if (!runId.IsValid)
            {
                throw new ArgumentException("A spawn identity requires a valid RunId.", nameof(runId));
            }

            if (routeGenerationVersion == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(routeGenerationVersion),
                    "Route generation version must be positive.");
            }

            byte[] runBytes = EncodeNonEmpty(runId.ToString(), nameof(runId));
            byte[] generationBytes = EncodeNonEmpty(
                routeGenerationVersion.ToString(CultureInfo.InvariantCulture), nameof(routeGenerationVersion));
            byte[] nodeBytes = EncodeNonEmpty(nodeId, nameof(nodeId));

            if (!roomInstanceId.IsValid)
            {
                throw new ArgumentException("A spawn identity requires a valid RoomInstanceId.", nameof(roomInstanceId));
            }

            RoomInstanceId expectedRoom = ExpeditionRoomIdentity.ForNode(runId, nodeId);
            if (roomInstanceId != expectedRoom)
            {
                throw new ArgumentException(
                    "RoomInstanceId must equal ExpeditionRoomIdentity.ForNode(RunId, NodeId).",
                    nameof(roomInstanceId));
            }

            byte[] roomBytes = EncodeNonEmpty(roomInstanceId.ToString(), nameof(roomInstanceId));
            byte[] encounterBytes = EncodeNonEmpty(encounterId, nameof(encounterId));
            ValidateLocalSpawnId(localSpawnId);
            byte[] localSpawnBytes = EncodeNonEmpty(localSpawnId, nameof(localSpawnId));

            byte[] preimage = EncodePreimage(new[]
            {
                runBytes,
                generationBytes,
                nodeBytes,
                roomBytes,
                encounterBytes,
                localSpawnBytes
            });

            return new EncounterSpawnIdentityV1(runId, routeGenerationVersion, nodeId, roomInstanceId,
                encounterId, localSpawnId, ComputeKey(preimage));
        }

        /// <summary>Returns a fresh copy of the canonical domain-and-fields preimage.</summary>
        public byte[] ToCanonicalPreimage()
        {
            return EncodePreimage(new[]
            {
                EncodeNonEmpty(RunId.ToString(), nameof(RunId)),
                EncodeNonEmpty(RouteGenerationVersion.ToString(CultureInfo.InvariantCulture), nameof(RouteGenerationVersion)),
                EncodeNonEmpty(NodeId, nameof(NodeId)),
                EncodeNonEmpty(RoomInstanceId.ToString(), nameof(RoomInstanceId)),
                EncodeNonEmpty(EncounterId, nameof(EncounterId)),
                EncodeNonEmpty(LocalSpawnId, nameof(LocalSpawnId))
            });
        }

        /// <summary>
        /// Decodes exactly one canonical v1 preimage. Unknown domains, malformed UTF-8,
        /// noncanonical fields, truncation, and trailing bytes are rejected.
        /// </summary>
        public static EncounterSpawnIdentityV1 DecodeCanonicalPreimage(byte[] preimage)
        {
            if (preimage == null) throw new ArgumentNullException(nameof(preimage));

            byte[] input = (byte[])preimage.Clone();
            if (input.Length < DomainBytes.Length || !HasPrefix(input, DomainBytes))
            {
                throw new FormatException("Preimage has an unknown domain/version or a truncated domain.");
            }

            int offset = DomainBytes.Length;
            var fields = new string[FieldCount];
            for (int index = 0; index < FieldCount; index++)
            {
                uint byteLength = ReadUInt32BigEndian(input, ref offset);
                int remaining = input.Length - offset;
                if (byteLength > (uint)remaining)
                {
                    throw new FormatException("Preimage contains a truncated field.");
                }

                int fieldLength = checked((int)byteLength);
                try
                {
                    fields[index] = StrictUtf8.GetString(input, offset, fieldLength);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FormatException("Preimage contains invalid UTF-8.", exception);
                }

                offset += fieldLength;
            }

            if (offset != input.Length)
            {
                throw new FormatException("Preimage contains trailing bytes after its six fields.");
            }

            EncounterSpawnIdentityV1 identity = CreateFromCanonicalFields(fields);
            if (!BytesEqual(input, identity.ToCanonicalPreimage()))
            {
                throw new FormatException("Preimage is not in canonical v1 form.");
            }

            return identity;
        }

        /// <summary>
        /// Validates a persisted key against this tuple. A key by itself cannot reconstruct
        /// or authorize an identity.
        /// </summary>
        public void EnsureCanonicalKeyMatches(string candidateKey)
        {
            ValidateKeySyntax(candidateKey);
            if (!string.Equals(Key, candidateKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The supplied key does not match this spawn tuple.");
            }
        }

        /// <summary>
        /// Requires both the digest and all six tuple fields to match. This rejects a digest
        /// collision that is presented with a different tuple.
        /// </summary>
        public void EnsureSameIdentity(EncounterSpawnIdentityV1 other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            if (string.Equals(Key, other.Key, StringComparison.Ordinal) && !HasSameTuple(other))
            {
                throw new InvalidOperationException("A spawn key is already associated with a different tuple.");
            }

            if (!string.Equals(Key, other.Key, StringComparison.Ordinal) || !HasSameTuple(other))
            {
                throw new InvalidOperationException("The spawn identities do not match.");
            }
        }

        public bool HasSameTuple(EncounterSpawnIdentityV1 other)
        {
            return other != null
                && RunId == other.RunId
                && RouteGenerationVersion == other.RouteGenerationVersion
                && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
                && RoomInstanceId == other.RoomInstanceId
                && string.Equals(EncounterId, other.EncounterId, StringComparison.Ordinal)
                && string.Equals(LocalSpawnId, other.LocalSpawnId, StringComparison.Ordinal);
        }

        private static EncounterSpawnIdentityV1 CreateFromCanonicalFields(string[] fields)
        {
            if (fields == null || fields.Length != FieldCount)
            {
                throw new FormatException("Preimage must contain exactly six fields.");
            }

            if (!RunId.TryParse(fields[0], out RunId runId)
                || !string.Equals(fields[0], runId.ToString(), StringComparison.Ordinal))
            {
                throw new FormatException("RunId must be a nonempty lowercase GUID in N format.");
            }

            if (!uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint generation)
                || generation == 0
                || !string.Equals(fields[1], generation.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                throw new FormatException("Route generation must be a positive canonical unsigned decimal.");
            }

            if (!RoomInstanceId.TryParse(fields[3], out RoomInstanceId roomInstanceId)
                || !string.Equals(fields[3], roomInstanceId.ToString(), StringComparison.Ordinal))
            {
                throw new FormatException("RoomInstanceId must be a nonempty lowercase GUID in N format.");
            }

            try
            {
                return Create(runId, generation, fields[2], roomInstanceId, fields[4], fields[5]);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Preimage fields do not form a valid canonical spawn tuple.", exception);
            }
        }

        private static byte[] EncodeNonEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Spawn identity fields must be nonempty.", parameterName);
            }

            try
            {
                return StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw new ArgumentException("Spawn identity fields must contain valid UTF-16 text.",
                    parameterName, exception);
            }
        }

        private static void ValidateLocalSpawnId(string localSpawnId)
        {
            if (string.IsNullOrEmpty(localSpawnId))
            {
                throw new ArgumentException("LocalSpawnId must be nonempty.", nameof(localSpawnId));
            }

            for (int index = 0; index < localSpawnId.Length; index++)
            {
                char value = localSpawnId[index];
                bool allowed = (value >= 'A' && value <= 'Z')
                    || (value >= 'a' && value <= 'z')
                    || (value >= '0' && value <= '9')
                    || value == '.'
                    || value == '_'
                    || value == '-';

                if (!allowed)
                {
                    throw new ArgumentException(
                        "LocalSpawnId may contain only ASCII letters, digits, dot, underscore, and hyphen.",
                        nameof(localSpawnId));
                }
            }
        }

        private static byte[] EncodePreimage(byte[][] fields)
        {
            if (fields == null || fields.Length != FieldCount)
            {
                throw new ArgumentException("The canonical codec requires exactly six fields.", nameof(fields));
            }

            int totalLength = DomainBytes.Length;
            for (int index = 0; index < fields.Length; index++)
            {
                byte[] field = fields[index];
                if (field == null) throw new ArgumentException("A canonical field cannot be null.", nameof(fields));
                if (field.LongLength > uint.MaxValue)
                {
                    throw new OverflowException("A UTF-8 field exceeds the UInt32 length framing limit.");
                }

                totalLength = checked(totalLength + 4 + field.Length);
            }

            var output = new byte[totalLength];
            Buffer.BlockCopy(DomainBytes, 0, output, 0, DomainBytes.Length);
            int offset = DomainBytes.Length;
            for (int index = 0; index < fields.Length; index++)
            {
                uint fieldLength = (uint)fields[index].Length;
                output[offset++] = (byte)(fieldLength >> 24);
                output[offset++] = (byte)(fieldLength >> 16);
                output[offset++] = (byte)(fieldLength >> 8);
                output[offset++] = (byte)fieldLength;
                Buffer.BlockCopy(fields[index], 0, output, offset, fields[index].Length);
                offset += fields[index].Length;
            }

            return output;
        }

        private static string ComputeKey(byte[] preimage)
        {
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(preimage);
            }

            if (digest.Length != DigestByteCount)
            {
                throw new CryptographicException("SHA-256 returned an unexpected digest length.");
            }

            var hex = new char[DigestHexLength];
            for (int index = 0; index < digest.Length; index++)
            {
                hex[index * 2] = LowerHexDigits[digest[index] >> 4];
                hex[index * 2 + 1] = LowerHexDigits[digest[index] & 0x0f];
            }

            return KeyPrefix + new string(hex);
        }

        private static void ValidateKeySyntax(string candidateKey)
        {
            if (candidateKey == null) throw new ArgumentNullException(nameof(candidateKey));

            if (!candidateKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                throw new FormatException("Unknown encounter spawn key prefix or version.");
            }

            if (candidateKey.Length != KeyPrefix.Length + DigestHexLength)
            {
                throw new FormatException("Encounter spawn key must contain a 64-character SHA-256 digest.");
            }

            for (int index = KeyPrefix.Length; index < candidateKey.Length; index++)
            {
                char value = candidateKey[index];
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f')))
                {
                    throw new FormatException("Encounter spawn key digest must be lowercase hexadecimal.");
                }
            }
        }

        private static uint ReadUInt32BigEndian(byte[] input, ref int offset)
        {
            if (input.Length - offset < 4)
            {
                throw new FormatException("Preimage contains a truncated field length.");
            }

            uint value = ((uint)input[offset] << 24)
                | ((uint)input[offset + 1] << 16)
                | ((uint)input[offset + 2] << 8)
                | input[offset + 3];
            offset += 4;
            return value;
        }

        private static bool HasPrefix(byte[] input, byte[] prefix)
        {
            if (input.Length < prefix.Length) return false;
            for (int index = 0; index < prefix.Length; index++)
            {
                if (input[index] != prefix[index]) return false;
            }

            return true;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }

            return true;
        }
    }
}
