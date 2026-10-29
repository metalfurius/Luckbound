using System;
using System.Globalization;
using System.Text;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class EncounterSpawnIdentityV1Tests
    {
        private const string FixedRunId = "0123456789abcdef0123456789abcdef";
        private const uint FixedGeneration = 7;
        private const string FixedNodeId = "node.alpha";
        private const string FixedEncounterId = "encounter.boss";
        private const string FixedLocalSpawnId = "spawn-1";
        private const string FixedPreimageHex =
            "4c75636b626f756e642e456e636f756e746572537061776e2e763100"
            + "000000203031323334353637383961626364656630313233343536373839616263646566"
            + "0000000137"
            + "0000000a6e6f64652e616c706861"
            + "000000206139333063396230383131633466613363633033323230326538626632653731"
            + "0000000e656e636f756e7465722e626f7373"
            + "00000007737061776e2d31";
        private const string FixedKey =
            "encounter-spawn.v1:b1d3484528ea33757381d8bca4296e70eeeab273bc383976c71dc7f2d68599f4";

        [Test]
        public void FixedTupleProducesTheIndependentCanonicalPreimageAndKeyVector()
        {
            EncounterSpawnIdentityV1 identity = Create();

            Assert.That(identity.RoomInstanceId.ToString(), Is.EqualTo("a930c9b0811c4fa3cc032202e8bf2e71"));
            Assert.That(ToLowerHex(identity.ToCanonicalPreimage()), Is.EqualTo(FixedPreimageHex));
            Assert.That(identity.Key, Is.EqualTo(FixedKey));
        }

        [Test]
        public void RepeatedTupleConstructionProducesTheSamePreimageAndKey()
        {
            EncounterSpawnIdentityV1 first = Create();
            EncounterSpawnIdentityV1 second = Create();

            Assert.That(ToLowerHex(second.ToCanonicalPreimage()), Is.EqualTo(ToLowerHex(first.ToCanonicalPreimage())));
            Assert.That(second.Key, Is.EqualTo(first.Key));
            Assert.DoesNotThrow(() => first.EnsureSameIdentity(second));
        }

        [Test]
        public void KeyIsInvariantAcrossCurrentCultures()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                EncounterSpawnIdentityV1 baseline = Create(generation: 1234);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-EG");
                EncounterSpawnIdentityV1 localized = Create(generation: 1234);

                Assert.That(localized.Key, Is.EqualTo(baseline.Key));
                Assert.That(ToLowerHex(localized.ToCanonicalPreimage()),
                    Is.EqualTo(ToLowerHex(baseline.ToCanonicalPreimage())));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        [Test]
        public void MultibyteFieldsUseUtf8ByteLengthInTheirFrames()
        {
            const string nodeId = "nóde.雪";
            EncounterSpawnIdentityV1 identity = Create(nodeId: nodeId, encounterId: "boss.🦊");
            byte[] preimage = identity.ToCanonicalPreimage();
            int offset = Encoding.ASCII.GetByteCount("Luckbound.EncounterSpawn.v1\0");

            offset += 4 + Encoding.UTF8.GetByteCount(FixedRunId);
            offset += 4 + Encoding.UTF8.GetByteCount(FixedGeneration.ToString(CultureInfo.InvariantCulture));
            Assert.That(ReadUInt32BigEndian(preimage, offset), Is.EqualTo((uint)Encoding.UTF8.GetByteCount(nodeId)));
            Assert.That(Encoding.UTF8.GetByteCount(nodeId), Is.GreaterThan(nodeId.Length));
        }

        [Test]
        public void InvalidUtf16SurrogateIsRejectedInsteadOfReplaced()
        {
            Assert.Throws<ArgumentException>(() => Create(encounterId: "invalid-\ud800"));
        }

        [Test]
        public void LengthFramingSeparatesIdentifiersWithEmbeddedSeparators()
        {
            EncounterSpawnIdentityV1 first = Create(encounterId: "boss-alpha", localSpawnId: "beta");
            EncounterSpawnIdentityV1 second = Create(encounterId: "boss", localSpawnId: "alpha-beta");

            Assert.That(first.EncounterId + "-" + first.LocalSpawnId,
                Is.EqualTo(second.EncounterId + "-" + second.LocalSpawnId));
            Assert.That(first.Key, Is.Not.EqualTo(second.Key));
            Assert.That(ToLowerHex(first.ToCanonicalPreimage()), Is.Not.EqualTo(ToLowerHex(second.ToCanonicalPreimage())));
        }

        [Test]
        public void RoomInstanceMustMatchTheRunAndNodeDerivation()
        {
            RunId runId = RunId.Parse(FixedRunId);
            RoomInstanceId otherNodeRoom = ExpeditionRoomIdentity.ForNode(runId, "node.other");

            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId, FixedGeneration,
                FixedNodeId, otherNodeRoom, FixedEncounterId, FixedLocalSpawnId));
        }

        [Test]
        public void LocalSpawnIdRequiresTheAcceptedAsciiAlphabet()
        {
            foreach (string invalid in new[] { "", "spawn/1", "spawn 1", "spawn:1", "spáwn-1", "spawn\n1" })
            {
                Assert.Throws<ArgumentException>(() => Create(localSpawnId: invalid), "localSpawnId=" + invalid);
            }

            Assert.That(Create(localSpawnId: "Spawn_1.v1").LocalSpawnId, Is.EqualTo("Spawn_1.v1"));
        }

        [Test]
        public void ChangingVariableTupleFieldsChangesTheKey()
        {
            EncounterSpawnIdentityV1 baseline = Create();
            EncounterSpawnIdentityV1 changedRun = Create(runId: "0123456789abcdef0123456789abcdee");
            EncounterSpawnIdentityV1 changedNode = Create(nodeId: "node.beta");

            Assert.That(changedRun.Key, Is.Not.EqualTo(baseline.Key));
            Assert.That(changedRun.RoomInstanceId, Is.Not.EqualTo(baseline.RoomInstanceId));
            Assert.That(Create(generation: FixedGeneration + 1).Key, Is.Not.EqualTo(baseline.Key));
            Assert.That(changedNode.Key, Is.Not.EqualTo(baseline.Key));
            Assert.That(changedNode.RoomInstanceId, Is.Not.EqualTo(baseline.RoomInstanceId));
            Assert.That(Create(encounterId: "encounter.other").Key, Is.Not.EqualTo(baseline.Key));
            Assert.That(Create(localSpawnId: "spawn-2").Key, Is.Not.EqualTo(baseline.Key));
        }

        [Test]
        public void IdentityRetainsOrdinalIdentifierTextWithoutCaseFolding()
        {
            EncounterSpawnIdentityV1 upper = Create(encounterId: "Encounter.Boss", localSpawnId: "Spawn-A");
            EncounterSpawnIdentityV1 lower = Create(encounterId: "encounter.boss", localSpawnId: "spawn-a");

            Assert.That(upper.EncounterId, Is.EqualTo("Encounter.Boss"));
            Assert.That(upper.LocalSpawnId, Is.EqualTo("Spawn-A"));
            Assert.That(upper.Key, Is.Not.EqualTo(lower.Key));
        }

        [Test]
        public void CanonicalPreimageRoundTripsAndKeepsTheFullTuple()
        {
            EncounterSpawnIdentityV1 original = Create(nodeId: "node.雪", encounterId: "encounter.🦊");
            EncounterSpawnIdentityV1 decoded = EncounterSpawnIdentityV1.DecodeCanonicalPreimage(
                original.ToCanonicalPreimage());

            Assert.That(decoded.HasSameTuple(original), Is.True);
            Assert.That(decoded.Key, Is.EqualTo(original.Key));
            Assert.DoesNotThrow(() => decoded.EnsureCanonicalKeyMatches(original.Key));
        }

        [Test]
        public void TruncatedPreimageIsRejected()
        {
            byte[] preimage = Create().ToCanonicalPreimage();
            Array.Resize(ref preimage, preimage.Length - 1);

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage));
        }

        [Test]
        public void TrailingPreimageBytesAreRejected()
        {
            byte[] preimage = Create().ToCanonicalPreimage();
            Array.Resize(ref preimage, preimage.Length + 1);

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage));
        }

        [Test]
        public void MalformedUtf8FieldIsRejected()
        {
            RunId runId = RunId.Parse(FixedRunId);
            RoomInstanceId roomId = ExpeditionRoomIdentity.ForNode(runId, FixedNodeId);
            byte[][] fields =
            {
                Encoding.UTF8.GetBytes(FixedRunId),
                Encoding.UTF8.GetBytes(FixedGeneration.ToString(CultureInfo.InvariantCulture)),
                Encoding.UTF8.GetBytes(FixedNodeId),
                Encoding.UTF8.GetBytes(roomId.ToString()),
                Encoding.UTF8.GetBytes(FixedEncounterId),
                new byte[] { 0xc3, 0x28 }
            };

            Assert.Throws<FormatException>(() =>
                EncounterSpawnIdentityV1.DecodeCanonicalPreimage(BuildPreimage(fields)));
        }

        [Test]
        public void UnknownPreimageVersionIsRejected()
        {
            byte[] preimage = Create().ToCanonicalPreimage();
            int versionByte = Encoding.ASCII.GetByteCount("Luckbound.EncounterSpawn.v");
            preimage[versionByte] = (byte)'2';

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage));
        }

        [Test]
        public void UnknownKeyPrefixVersionAndMalformedDigestAreRejected()
        {
            EncounterSpawnIdentityV1 identity = Create();

            Assert.Throws<FormatException>(() => identity.EnsureCanonicalKeyMatches(
                identity.Key.Replace("encounter-spawn.v1:", "encounter-spawn.v2:")));
            Assert.Throws<FormatException>(() => identity.EnsureCanonicalKeyMatches(
                identity.Key.Substring(0, identity.Key.Length - 1) + "G"));
        }

        [Test]
        public void AKeyFromAnotherTupleIsRejected()
        {
            EncounterSpawnIdentityV1 first = Create();
            EncounterSpawnIdentityV1 differentTuple = Create(encounterId: "encounter.other");

            Assert.Throws<InvalidOperationException>(() => first.EnsureSameIdentity(differentTuple));
            Assert.Throws<InvalidOperationException>(() => first.EnsureCanonicalKeyMatches(differentTuple.Key));
        }

        [Test]
        public void CreateRejectsDefaultAndEmptyIdentifiersAndZeroGeneration()
        {
            RunId runId = RunId.Parse(FixedRunId);
            RoomInstanceId roomId = ExpeditionRoomIdentity.ForNode(runId, FixedNodeId);

            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(default(RunId),
                FixedGeneration, FixedNodeId, roomId, FixedEncounterId, FixedLocalSpawnId));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, FixedNodeId, default(RoomInstanceId), FixedEncounterId, FixedLocalSpawnId));
            Assert.Throws<ArgumentOutOfRangeException>(() => EncounterSpawnIdentityV1.Create(runId,
                0, FixedNodeId, roomId, FixedEncounterId, FixedLocalSpawnId));

            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, null, roomId, FixedEncounterId, FixedLocalSpawnId));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, string.Empty, roomId, FixedEncounterId, FixedLocalSpawnId));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, FixedNodeId, roomId, null, FixedLocalSpawnId));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, FixedNodeId, roomId, string.Empty, FixedLocalSpawnId));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, FixedNodeId, roomId, FixedEncounterId, null));
            Assert.Throws<ArgumentException>(() => EncounterSpawnIdentityV1.Create(runId,
                FixedGeneration, FixedNodeId, roomId, FixedEncounterId, string.Empty));
        }

        [Test]
        public void DecoderRejectsNoncanonicalAndOverflowGenerationDecimal()
        {
            foreach (string generation in new[] { "07", "+7", "4294967296" })
            {
                byte[] preimage = BuildTextPreimage(generation: generation);
                Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage),
                    "generation=" + generation);
            }
        }

        [Test]
        public void DecoderRejectsUppercaseGuidNFields()
        {
            byte[] uppercaseRunId = BuildTextPreimage(runId: FixedRunId.ToUpperInvariant());
            byte[] uppercaseRoomId = BuildTextPreimage(roomId:
                ExpeditionRoomIdentity.ForNode(RunId.Parse(FixedRunId), FixedNodeId).ToString().ToUpperInvariant());

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(uppercaseRunId));
            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(uppercaseRoomId));
        }

        [Test]
        public void DecoderRejectsTruncatedLengthPrefixHeader()
        {
            byte[] preimage = Create().ToCanonicalPreimage();
            int domainLength = Encoding.ASCII.GetByteCount("Luckbound.EncounterSpawn.v1\0");
            Array.Resize(ref preimage, domainLength + 2);

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage));
        }

        [Test]
        public void DecoderRejectsUInt32MaxLengthOnShortPreimage()
        {
            byte[] preimage = Create().ToCanonicalPreimage();
            int firstLengthOffset = Encoding.ASCII.GetByteCount("Luckbound.EncounterSpawn.v1\0");
            preimage[firstLengthOffset] = byte.MaxValue;
            preimage[firstLengthOffset + 1] = byte.MaxValue;
            preimage[firstLengthOffset + 2] = byte.MaxValue;
            preimage[firstLengthOffset + 3] = byte.MaxValue;

            Assert.Throws<FormatException>(() => EncounterSpawnIdentityV1.DecodeCanonicalPreimage(preimage));
        }

        [Test]
        public void MutatingReturnedPreimageDoesNotAlterIdentityOrKey()
        {
            EncounterSpawnIdentityV1 identity = Create();
            byte[] expectedPreimage = identity.ToCanonicalPreimage();
            string expectedKey = identity.Key;
            byte[] returnedPreimage = identity.ToCanonicalPreimage();
            returnedPreimage[0] ^= 0xff;
            returnedPreimage[returnedPreimage.Length - 1] ^= 0xff;

            Assert.That(identity.Key, Is.EqualTo(expectedKey));
            CollectionAssert.AreEqual(expectedPreimage, identity.ToCanonicalPreimage());
        }

        private static EncounterSpawnIdentityV1 Create(
            string runId = FixedRunId,
            uint generation = FixedGeneration,
            string nodeId = FixedNodeId,
            string encounterId = FixedEncounterId,
            string localSpawnId = FixedLocalSpawnId)
        {
            RunId typedRunId = RunId.Parse(runId);
            RoomInstanceId roomId = ExpeditionRoomIdentity.ForNode(typedRunId, nodeId);
            return EncounterSpawnIdentityV1.Create(typedRunId, generation, nodeId, roomId,
                encounterId, localSpawnId);
        }

        private static byte[] BuildTextPreimage(
            string runId = FixedRunId,
            string generation = "7",
            string nodeId = FixedNodeId,
            string roomId = null,
            string encounterId = FixedEncounterId,
            string localSpawnId = FixedLocalSpawnId)
        {
            if (roomId == null)
            {
                roomId = ExpeditionRoomIdentity.ForNode(RunId.Parse(FixedRunId), FixedNodeId).ToString();
            }

            return BuildPreimage(new[]
            {
                Encoding.UTF8.GetBytes(runId),
                Encoding.UTF8.GetBytes(generation),
                Encoding.UTF8.GetBytes(nodeId),
                Encoding.UTF8.GetBytes(roomId),
                Encoding.UTF8.GetBytes(encounterId),
                Encoding.UTF8.GetBytes(localSpawnId)
            });
        }

        private static byte[] BuildPreimage(byte[][] fields)
        {
            byte[] domain = Encoding.ASCII.GetBytes("Luckbound.EncounterSpawn.v1\0");
            int length = domain.Length;
            for (int index = 0; index < fields.Length; index++) length = checked(length + 4 + fields[index].Length);

            var output = new byte[length];
            Buffer.BlockCopy(domain, 0, output, 0, domain.Length);
            int offset = domain.Length;
            foreach (byte[] field in fields)
            {
                uint fieldLength = (uint)field.Length;
                output[offset++] = (byte)(fieldLength >> 24);
                output[offset++] = (byte)(fieldLength >> 16);
                output[offset++] = (byte)(fieldLength >> 8);
                output[offset++] = (byte)fieldLength;
                Buffer.BlockCopy(field, 0, output, offset, field.Length);
                offset += field.Length;
            }

            return output;
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var output = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                output[index * 2] = digits[bytes[index] >> 4];
                output[index * 2 + 1] = digits[bytes[index] & 0x0f];
            }

            return new string(output);
        }
    }
}
