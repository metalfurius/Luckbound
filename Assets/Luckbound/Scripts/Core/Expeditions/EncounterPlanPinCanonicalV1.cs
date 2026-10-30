using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Canonical domain codec for the isolated EncounterPlanPin schema 1.</summary>
    public static class EncounterPlanPinCanonicalV1
    {
        private const string Domain = "Luckbound.EncounterPins.v1";
        private const int MinimumBindingBytes = 132;
        private static readonly byte[] DomainPrefix = Encoding.ASCII.GetBytes(Domain + "\0");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(EncounterPlanPinV1 pin)
        {
            if (pin == null)
                throw EncounterPlanPinV1.Failure(EncounterPlanPinErrorCodeV1.NullInput, "pin", "An encounter plan pin is required.");
            EncounterPlanPinV1.EnsureValid(pin, "pin");

            var writer = new CanonicalWriter();
            writer.WriteUInt32((uint)pin.SchemaVersion);
            writer.WriteString(pin.RunId.ToString());
            writer.WriteString(pin.CatalogId);
            writer.WriteUInt32((uint)pin.CatalogRevision);
            writer.WriteString(pin.CatalogSha256);
            writer.WriteString(pin.RouteSha256);
            writer.WriteString(pin.IdentityCodec);
            writer.WriteCount(pin.Bindings.Count);
            for (int index = 0; index < pin.Bindings.Count; index++)
                WriteBinding(writer, pin.Bindings[index]);
            writer.WriteString(pin.Entry.NodeId);
            writer.WriteString(pin.Entry.ResolvedPlanSha256);
            return writer.ToArray();
        }

        public static bool TryDecode(byte[] bytes, out EncounterPlanPinV1 pin,
            out EncounterPlanPinErrorV1 error)
        {
            pin = null;
            if (bytes == null)
            {
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.NullInput,
                    "bytes", "Canonical pin bytes are required.");
                return false;
            }

            try
            {
                var reader = new CanonicalReader(bytes);
                reader.RequireDomainPrefix();
                uint schemaVersion = reader.ReadUInt32();
                if (schemaVersion != EncounterPlanPinV1.SupportedSchemaVersion)
                {
                    error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.UnsupportedSchema,
                        "schemaVersion", "Only EncounterPlanPin schema 1 is supported.");
                    return false;
                }

                string runIdText = reader.ReadString();
                if (!Luckbound.Core.Identifiers.RunId.TryParse(runIdText, out Luckbound.Core.Identifiers.RunId runId)
                    || !string.Equals(runId.ToString(), runIdText, StringComparison.Ordinal))
                    throw new WireFormatException("runId", "RunId must be a lowercase non-empty GUID in N format.");
                string catalogId = reader.ReadString();
                uint catalogRevisionValue = reader.ReadUInt32();
                if (catalogRevisionValue == 0 || catalogRevisionValue > int.MaxValue)
                    throw new WireFormatException("catalogRevision", "Catalog revision is outside the supported positive Int32 range.");
                string catalogSha256 = reader.ReadString();
                string routeSha256 = reader.ReadString();
                string identityCodec = reader.ReadString();
                if (!string.Equals(identityCodec, EncounterPlanPinV1.SupportedIdentityCodec, StringComparison.Ordinal))
                {
                    error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.UnsupportedSchema,
                        "identityCodec", "The encounter identity codec is not supported by this pin reader.");
                    return false;
                }

                uint bindingCountValue = reader.ReadUInt32();
                if (bindingCountValue > int.MaxValue
                    || bindingCountValue > (uint)(reader.Remaining / MinimumBindingBytes))
                    throw new WireFormatException("bindings", "Binding count exceeds the bytes remaining in the input.");
                var bindings = new List<EncounterPlanNodeBindingV1>((int)bindingCountValue);
                for (int index = 0; index < (int)bindingCountValue; index++)
                    bindings.Add(ReadBinding(reader, index));

                string entryNodeId = reader.ReadString();
                string entrySha256 = reader.ReadString();
                if (reader.Remaining != 0)
                    throw new WireFormatException("bytes", "Canonical pin bytes cannot contain trailing data.");

                pin = new EncounterPlanPinV1((int)schemaVersion, runId, catalogId,
                    (int)catalogRevisionValue, catalogSha256, routeSha256, identityCodec, bindings,
                    new EncounterPlanEntryReferenceV1(entryNodeId, entrySha256));
                EncounterPlanPinErrorV1 validationError = EncounterPlanPinV1.Validate(pin);
                if (validationError.Code != EncounterPlanPinErrorCodeV1.None)
                {
                    pin = null;
                    error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                        validationError.Path, validationError.Message);
                    return false;
                }

                byte[] canonical = Encode(pin);
                if (!BytesEqual(canonical, bytes))
                {
                    pin = null;
                    error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                        "bytes", "Input bytes are not the unique canonical encoding of this pin.");
                    return false;
                }

                error = default(EncounterPlanPinErrorV1);
                return true;
            }
            catch (WireFormatException exception)
            {
                pin = null;
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                    exception.Path, exception.Message);
                return false;
            }
            catch (DecoderFallbackException exception)
            {
                pin = null;
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                    "bytes", "Input contains invalid UTF-8: " + exception.Message);
                return false;
            }
            catch (FormatException exception)
            {
                pin = null;
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                    "bytes", exception.Message);
                return false;
            }
            catch (OverflowException exception)
            {
                pin = null;
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                    "bytes", exception.Message);
                return false;
            }
            catch (EncounterPlanPinValidationExceptionV1 exception)
            {
                pin = null;
                error = new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MalformedEncoding,
                    exception.Error.Path, exception.Error.Message);
                return false;
            }
        }

        private static void WriteBinding(CanonicalWriter writer, EncounterPlanNodeBindingV1 binding)
        {
            writer.WriteString(binding.NodeId);
            writer.WriteUInt32((uint)binding.GenerationVersion);
            writer.WriteString(binding.TemplateId);
            writer.WriteString(binding.RoomInstanceId.ToString());
            writer.WriteString(EncounterCatalogTokensV1.GetToken(binding.Mode));
            bool encounter = binding.Mode == EncounterCatalogBindingMode.Encounter;
            writer.WriteOptionalMarker(encounter);
            if (encounter)
            {
                writer.WriteString(binding.EncounterId);
                writer.WriteUInt32((uint)binding.DefinitionRevision);
                writer.WriteString(binding.DefinitionSha256);
                writer.WriteString(binding.VariantId);
            }
            writer.WriteCount(binding.ContextPactIds.Count);
            for (int index = 0; index < binding.ContextPactIds.Count; index++)
                writer.WriteString(binding.ContextPactIds[index]);
            // Marker zero is the frozen “no authoritative calendar applies” value for this cut.
            writer.WriteOptionalMarker(false);
            writer.WriteString(binding.ResolvedPlanSha256);
        }

        private static EncounterPlanNodeBindingV1 ReadBinding(CanonicalReader reader, int bindingIndex)
        {
            string path = "bindings[" + bindingIndex.ToString(CultureInfo.InvariantCulture) + "]";
            string nodeId = reader.ReadString(path + ".nodeId");
            uint generationValue = reader.ReadUInt32(path + ".generationVersion");
            if (generationValue == 0 || generationValue > int.MaxValue)
                throw new WireFormatException(path + ".generationVersion", "Generation must be a positive Int32.");
            string templateId = reader.ReadString(path + ".templateId");
            string roomIdText = reader.ReadString(path + ".roomInstanceId");
            if (!Luckbound.Core.Identifiers.RoomInstanceId.TryParse(roomIdText,
                    out Luckbound.Core.Identifiers.RoomInstanceId roomId)
                || !string.Equals(roomId.ToString(), roomIdText, StringComparison.Ordinal))
                throw new WireFormatException(path + ".roomInstanceId", "RoomInstanceId must be a lowercase non-empty GUID in N format.");
            string modeToken = reader.ReadString(path + ".mode");
            if (!EncounterCatalogTokensV1.TryParseBindingMode(modeToken, out EncounterCatalogBindingMode mode))
                throw new WireFormatException(path + ".mode", "Binding mode token is not supported.");

            byte encounterMarker = reader.ReadOptionalMarker(path + ".encounterMarker");
            bool hasEncounter = mode == EncounterCatalogBindingMode.Encounter;
            if (encounterMarker != (hasEncounter ? (byte)1 : (byte)0))
                throw new WireFormatException(path + ".encounterMarker", "Encounter payload marker does not match binding mode.");
            string encounterId = null;
            int definitionRevision = 0;
            string definitionSha256 = null;
            string variantId = null;
            if (hasEncounter)
            {
                encounterId = reader.ReadString(path + ".encounterId");
                uint revisionValue = reader.ReadUInt32(path + ".definitionRevision");
                if (revisionValue == 0 || revisionValue > int.MaxValue)
                    throw new WireFormatException(path + ".definitionRevision", "Definition revision must be a positive Int32.");
                definitionRevision = (int)revisionValue;
                definitionSha256 = reader.ReadString(path + ".definitionSha256");
                variantId = reader.ReadString(path + ".variantId");
            }

            uint pactCountValue = reader.ReadUInt32(path + ".contextPactIds");
            if (pactCountValue > 2 || pactCountValue > int.MaxValue
                || pactCountValue > (uint)(reader.Remaining / 5))
                throw new WireFormatException(path + ".contextPactIds", "Pact count exceeds the supported range or remaining input.");
            var pactIds = new List<string>((int)pactCountValue);
            for (int index = 0; index < (int)pactCountValue; index++)
                pactIds.Add(reader.ReadString(path + ".contextPactIds[" + index.ToString(CultureInfo.InvariantCulture) + "]"));

            byte calendarMarker = reader.ReadOptionalMarker(path + ".calendarOrdinal");
            if (calendarMarker != 0)
                throw new WireFormatException(path + ".calendarOrdinal", "This schema cannot invent or decode a Vínculo calendar.");
            string resolvedPlanSha256 = reader.ReadString(path + ".resolvedPlanSha256");
            return new EncounterPlanNodeBindingV1(nodeId, (int)generationValue, templateId,
                roomId, mode, encounterId, definitionRevision, definitionSha256, variantId,
                pactIds, 0, resolvedPlanSha256);
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

            internal CanonicalWriter()
            {
                _stream.Write(DomainPrefix, 0, DomainPrefix.Length);
            }

            internal void WriteOptionalMarker(bool present) => _stream.WriteByte(present ? (byte)1 : (byte)0);

            internal void WriteCount(int count)
            {
                if (count < 0) throw new OverflowException("Canonical collection count cannot be negative.");
                WriteUInt32((uint)count);
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
                byte[] encoded = StrictUtf8.GetBytes(value);
                WriteUInt32((uint)encoded.Length);
                _stream.Write(encoded, 0, encoded.Length);
            }

            internal byte[] ToArray() => _stream.ToArray();
        }

        private sealed class CanonicalReader
        {
            private readonly byte[] _bytes;
            private int _position;

            internal CanonicalReader(byte[] bytes)
            {
                _bytes = bytes;
            }

            internal int Remaining => _bytes.Length - _position;

            internal void RequireDomainPrefix()
            {
                if (Remaining < DomainPrefix.Length)
                    throw new WireFormatException("domain", "Canonical domain prefix is truncated.");
                for (int index = 0; index < DomainPrefix.Length; index++)
                {
                    if (_bytes[_position + index] != DomainPrefix[index])
                        throw new WireFormatException("domain", "Canonical domain prefix does not match EncounterPins.v1.");
                }
                _position += DomainPrefix.Length;
            }

            internal uint ReadUInt32(string path = "bytes")
            {
                Require(4, path);
                uint value = ((uint)_bytes[_position] << 24)
                    | ((uint)_bytes[_position + 1] << 16)
                    | ((uint)_bytes[_position + 2] << 8)
                    | _bytes[_position + 3];
                _position += 4;
                return value;
            }

            internal string ReadString(string path = "bytes")
            {
                uint lengthValue = ReadUInt32(path);
                if (lengthValue > int.MaxValue || lengthValue > Remaining)
                    throw new WireFormatException(path, "String length exceeds the bytes remaining in the input.");
                int length = (int)lengthValue;
                string value = StrictUtf8.GetString(_bytes, _position, length);
                _position += length;
                return value;
            }

            internal byte ReadOptionalMarker(string path)
            {
                Require(1, path);
                byte value = _bytes[_position++];
                if (value > 1) throw new WireFormatException(path, "Optional marker must be zero or one.");
                return value;
            }

            private void Require(int count, string path)
            {
                if (count < 0 || Remaining < count)
                    throw new WireFormatException(path, "Canonical input is truncated.");
            }
        }

        private sealed class WireFormatException : Exception
        {
            internal WireFormatException(string path, string message) : base(message)
            {
                Path = path ?? "bytes";
            }

            internal string Path { get; }
        }
    }
}
