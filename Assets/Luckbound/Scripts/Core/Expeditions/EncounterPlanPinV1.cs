using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Expeditions
{
    public enum EncounterPlanPinErrorCodeV1
    {
        None = 0,
        NullInput,
        InvalidPin,
        UnsupportedSchema,
        MalformedEncoding,
        MissingBinding,
        PlanMismatch,
        CatalogRevisionUnavailable,
        MissingResolutionAuthority
    }

    public readonly struct EncounterPlanPinErrorV1
    {
        public EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1 code, string path, string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public EncounterPlanPinErrorCodeV1 Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class EncounterPlanPinValidationExceptionV1 : Exception
    {
        public EncounterPlanPinValidationExceptionV1(EncounterPlanPinErrorV1 error)
            : base(error.Message)
        {
            if (error.Code == EncounterPlanPinErrorCodeV1.None)
                throw new ArgumentException("A validation exception requires an error code.", nameof(error));
            Error = error;
        }

        public EncounterPlanPinErrorV1 Error { get; }
    }

    /// <summary>An immutable node binding to one exact, logically resolved encounter plan.</summary>
    public sealed class EncounterPlanNodeBindingV1
    {
        private static readonly HashSet<string> CanonicalPactIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "pact.avarice", "pact.fervor", "pact.haste", "pact.clairvoyance",
            "pact.ember", "pact.silence", "pact.gravity", "pact.bond"
        };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal EncounterPlanNodeBindingV1(string nodeId, int generationVersion, string templateId,
            RoomInstanceId roomInstanceId, EncounterCatalogBindingMode mode,
            string encounterId, int definitionRevision, string definitionSha256, string variantId,
            IEnumerable<string> contextPactIds, int calendarOrdinal, string resolvedPlanSha256)
        {
            NodeId = nodeId;
            GenerationVersion = generationVersion;
            TemplateId = templateId;
            RoomInstanceId = roomInstanceId;
            Mode = mode;
            EncounterId = encounterId;
            DefinitionRevision = definitionRevision;
            DefinitionSha256 = definitionSha256;
            VariantId = variantId;
            ContextPactIds = Array.AsReadOnly((contextPactIds ?? Array.Empty<string>()).ToArray());
            CalendarOrdinal = calendarOrdinal;
            ResolvedPlanSha256 = resolvedPlanSha256;
        }

        public string NodeId { get; }
        public int GenerationVersion { get; }
        public string TemplateId { get; }
        public RoomInstanceId RoomInstanceId { get; }
        public EncounterCatalogBindingMode Mode { get; }
        public string EncounterId { get; }
        public int DefinitionRevision { get; }
        public string DefinitionSha256 { get; }
        public string VariantId { get; }
        public IReadOnlyList<string> ContextPactIds { get; }
        /// <summary>Zero means that no authoritative Vínculo calendar applies.</summary>
        public int CalendarOrdinal { get; }
        public string ResolvedPlanSha256 { get; }

        internal EncounterPlanPinErrorV1 Validate(string path)
        {
            if (!IsStableText(NodeId) || GenerationVersion <= 0 || !IsStableText(TemplateId)
                || !RoomInstanceId.IsValid || !IsLowerSha256(ResolvedPlanSha256))
                return Invalid(path, "Binding identity, generation, room instance, or plan digest is invalid.");
            if (Mode != EncounterCatalogBindingMode.None && Mode != EncounterCatalogBindingMode.Encounter)
                return Invalid(path + ".mode", "Binding mode is not supported.");
            if (CalendarOrdinal != 0)
                return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MissingResolutionAuthority,
                    path + ".calendarOrdinal", "No authoritative Vínculo calendar is available in this cut.");
            if (ContextPactIds == null || ContextPactIds.Count > 2)
                return Invalid(path + ".contextPactIds", "Pact context must contain at most two IDs.");

            string previous = null;
            for (int index = 0; index < ContextPactIds.Count; index++)
            {
                string pactId = ContextPactIds[index];
                if (!IsStableText(pactId) || !CanonicalPactIds.Contains(pactId)
                    || (previous != null && string.CompareOrdinal(previous, pactId) >= 0))
                    return Invalid(path + ".contextPactIds[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Pact IDs must be known, non-empty, unique, and ordinally sorted.");
                previous = pactId;
            }

            if (Mode == EncounterCatalogBindingMode.None)
            {
                if (EncounterId != null || DefinitionRevision != 0 || DefinitionSha256 != null || VariantId != null)
                    return Invalid(path, "A None binding must omit all encounter definition payload.");
                return default(EncounterPlanPinErrorV1);
            }

            if (!IsStableText(EncounterId) || DefinitionRevision <= 0
                || !IsLowerSha256(DefinitionSha256) || !IsStableText(VariantId))
                return Invalid(path, "Encounter binding payload is incomplete or malformed.");
            return default(EncounterPlanPinErrorV1);
        }

        internal static bool IsStableText(string value)
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

        internal static bool IsLowerSha256(string value)
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

        internal static EncounterPlanPinErrorV1 Invalid(string path, string message)
        {
            return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.InvalidPin, path, message);
        }
    }

    public sealed class EncounterPlanEntryReferenceV1
    {
        internal EncounterPlanEntryReferenceV1(string nodeId, string resolvedPlanSha256)
        {
            NodeId = nodeId;
            ResolvedPlanSha256 = resolvedPlanSha256;
        }

        public string NodeId { get; }
        public string ResolvedPlanSha256 { get; }
    }

    /// <summary>
    /// A preproduction domain pin. It records exact logical plan identity only; it is not save or
    /// physical-spawn authority.
    /// </summary>
    public sealed class EncounterPlanPinV1
    {
        public const int SupportedSchemaVersion = 1;
        public const string SupportedIdentityCodec = "encounter-spawn.v1";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> CanonicalPactIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "pact.avarice", "pact.fervor", "pact.haste", "pact.clairvoyance",
            "pact.ember", "pact.silence", "pact.gravity", "pact.bond"
        };
        private readonly IReadOnlyList<EncounterPlanNodeBindingV1> _bindings;

        internal EncounterPlanPinV1(int schemaVersion, RunId runId, string catalogId,
            int catalogRevision, string catalogSha256, string routeSha256, string identityCodec,
            IEnumerable<EncounterPlanNodeBindingV1> bindings, EncounterPlanEntryReferenceV1 entry)
        {
            SchemaVersion = schemaVersion;
            RunId = runId;
            CatalogId = catalogId;
            CatalogRevision = catalogRevision;
            CatalogSha256 = catalogSha256;
            RouteSha256 = routeSha256;
            IdentityCodec = identityCodec;
            _bindings = Array.AsReadOnly((bindings ?? Array.Empty<EncounterPlanNodeBindingV1>()).ToArray());
            Entry = entry;
        }

        public int SchemaVersion { get; }
        public RunId RunId { get; }
        public string CatalogId { get; }
        public int CatalogRevision { get; }
        public string CatalogSha256 { get; }
        public string RouteSha256 { get; }
        public string IdentityCodec { get; }
        public IReadOnlyList<EncounterPlanNodeBindingV1> Bindings => _bindings;
        public EncounterPlanEntryReferenceV1 Entry { get; }

        public static EncounterPlanPinV1 Capture(EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run, IEnumerable<ResolvedEncounterPlanV1> resolvedPlans,
            string entryNodeId)
        {
            if (manifest == null) throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "manifest", "A catalog manifest is required.");
            if (run == null) throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "run", "An expedition run is required.");
            if (resolvedPlans == null) throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "resolvedPlans", "Resolved plans are required.");
            if (!EncounterPlanNodeBindingV1.IsStableText(entryNodeId))
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "entryNodeId", "A stable entry node ID is required.");
            if (!string.Equals(manifest.IdentityCodec, SupportedIdentityCodec, StringComparison.Ordinal))
                throw Failure(EncounterPlanPinErrorCodeV1.MissingResolutionAuthority, "manifest.identityCodec",
                    "The manifest identity codec is not supported by this resolver.");

            string catalogSha256 = EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest);
            string routeSha256 = ComputeRouteSha256(run.Route.CanonicalForm);
            if (!manifest.SupportedRouteGenerationVersions.Contains(run.Route.GenerationVersion))
                throw Failure(EncounterPlanPinErrorCodeV1.MissingResolutionAuthority, "run.route.generationVersion",
                    "The catalog does not support the run route generation.");

            var byNode = new Dictionary<string, EncounterPlanNodeBindingV1>(StringComparer.Ordinal);
            int count = 0;
            foreach (ResolvedEncounterPlanV1 plan in resolvedPlans)
            {
                if (plan == null)
                    throw Failure(EncounterPlanPinErrorCodeV1.NullInput,
                        "resolvedPlans[" + count.ToString(CultureInfo.InvariantCulture) + "]", "Resolved plans cannot contain null entries.");
                EncounterPlanNodeBindingV1 binding = BuildBinding(plan);
                ValidatePlanAgainstRunAndManifest(plan, binding, manifest, run, catalogSha256, routeSha256);
                if (byNode.TryGetValue(binding.NodeId, out EncounterPlanNodeBindingV1 existing))
                {
                    if (!BindingsEqual(existing, binding))
                        throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch,
                            "resolvedPlans[" + count.ToString(CultureInfo.InvariantCulture) + "]",
                            "A run/node slot cannot contain two different plans.");
                }
                else
                {
                    byNode.Add(binding.NodeId, binding);
                }
                count++;
                if (count > run.Route.Nodes.Count)
                    throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "resolvedPlans", "Plan count exceeds the route node count.");
            }

            if (!byNode.TryGetValue(entryNodeId, out EncounterPlanNodeBindingV1 entryBinding))
                throw Failure(EncounterPlanPinErrorCodeV1.MissingBinding, "entryNodeId", "Entry node must have a resolved plan.");

            return new EncounterPlanPinV1(SupportedSchemaVersion, run.RunId, manifest.CatalogId,
                manifest.CatalogRevision, catalogSha256, routeSha256, SupportedIdentityCodec,
                byNode.Values.OrderBy(binding => binding.NodeId, StringComparer.Ordinal),
                new EncounterPlanEntryReferenceV1(entryBinding.NodeId, entryBinding.ResolvedPlanSha256));
        }

        public static EncounterPlanPinV1 Append(EncounterPlanPinV1 existing,
            ResolvedEncounterPlanV1 nextResolvedPlan, string entryNodeId)
        {
            if (existing == null) throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "existing", "An existing pin is required.");
            if (nextResolvedPlan == null) throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "nextResolvedPlan", "A resolved plan is required.");
            if (!EncounterPlanNodeBindingV1.IsStableText(entryNodeId))
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "entryNodeId", "A stable entry node ID is required.");
            EnsureValid(existing, "pin");

            EncounterPlanNodeBindingV1 next = BuildBinding(nextResolvedPlan);
            if (nextResolvedPlan.RunId != existing.RunId
                || !string.Equals(nextResolvedPlan.CatalogDigest, existing.CatalogSha256, StringComparison.Ordinal)
                || !string.Equals(nextResolvedPlan.RouteDigest, existing.RouteSha256, StringComparison.Ordinal))
                throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch, "nextResolvedPlan",
                    "Appended plan must preserve the pinned run, exact catalog, and route.");

            var byNode = existing.Bindings.ToDictionary(binding => binding.NodeId, StringComparer.Ordinal);
            if (byNode.TryGetValue(next.NodeId, out EncounterPlanNodeBindingV1 prior))
            {
                if (!BindingsEqual(prior, next))
                    throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch, "nextResolvedPlan.nodeId",
                        "Append cannot replace an existing run/node binding.");
            }
            else
            {
                byNode.Add(next.NodeId, next);
            }

            if (!byNode.TryGetValue(entryNodeId, out EncounterPlanNodeBindingV1 entryBinding))
                throw Failure(EncounterPlanPinErrorCodeV1.MissingBinding, "entryNodeId",
                    "Entry node must reference an existing binding after append.");

            return new EncounterPlanPinV1(existing.SchemaVersion, existing.RunId, existing.CatalogId,
                existing.CatalogRevision, existing.CatalogSha256, existing.RouteSha256, existing.IdentityCodec,
                byNode.Values.OrderBy(binding => binding.NodeId, StringComparer.Ordinal),
                new EncounterPlanEntryReferenceV1(entryBinding.NodeId, entryBinding.ResolvedPlanSha256));
        }

        internal static EncounterPlanPinErrorV1 Validate(EncounterPlanPinV1 pin)
        {
            if (pin == null)
                return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.NullInput, "pin", "An encounter plan pin is required.");
            if (pin.SchemaVersion != SupportedSchemaVersion)
                return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.UnsupportedSchema, "schemaVersion",
                    "Only EncounterPlanPin schema 1 is supported.");
            if (!pin.RunId.IsValid || !EncounterPlanNodeBindingV1.IsStableText(pin.CatalogId)
                || pin.CatalogRevision <= 0 || !EncounterPlanNodeBindingV1.IsLowerSha256(pin.CatalogSha256)
                || !EncounterPlanNodeBindingV1.IsLowerSha256(pin.RouteSha256)
                || !string.Equals(pin.IdentityCodec, SupportedIdentityCodec, StringComparison.Ordinal))
                return EncounterPlanNodeBindingV1.Invalid("pin", "Pin root identity or digest is invalid.");
            if (pin.Bindings == null || pin.Bindings.Count == 0)
                return EncounterPlanNodeBindingV1.Invalid("bindings", "At least one binding is required.");

            string previousNodeId = null;
            EncounterPlanNodeBindingV1 foundEntry = null;
            for (int index = 0; index < pin.Bindings.Count; index++)
            {
                EncounterPlanNodeBindingV1 binding = pin.Bindings[index];
                if (binding == null)
                    return EncounterPlanNodeBindingV1.Invalid("bindings[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Bindings cannot contain null entries.");
                EncounterPlanPinErrorV1 bindingError = binding.Validate("bindings[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                if (bindingError.Code != EncounterPlanPinErrorCodeV1.None) return bindingError;
                if (previousNodeId != null && string.CompareOrdinal(previousNodeId, binding.NodeId) >= 0)
                    return EncounterPlanNodeBindingV1.Invalid("bindings", "Bindings must be unique and sorted by node ID.");
                previousNodeId = binding.NodeId;
                if (pin.Entry != null && string.Equals(pin.Entry.NodeId, binding.NodeId, StringComparison.Ordinal))
                    foundEntry = binding;
            }

            if (pin.Entry == null || !EncounterPlanNodeBindingV1.IsStableText(pin.Entry.NodeId)
                || !EncounterPlanNodeBindingV1.IsLowerSha256(pin.Entry.ResolvedPlanSha256))
                return EncounterPlanNodeBindingV1.Invalid("entry", "Entry reference is malformed.");
            if (foundEntry == null)
                return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.MissingBinding,
                    "entry.nodeId", "Entry reference does not name a binding.");
            if (!string.Equals(foundEntry.ResolvedPlanSha256, pin.Entry.ResolvedPlanSha256, StringComparison.Ordinal))
                return new EncounterPlanPinErrorV1(EncounterPlanPinErrorCodeV1.PlanMismatch,
                    "entry.resolvedPlanSha256", "Entry reference must match its binding plan digest exactly.");
            return default(EncounterPlanPinErrorV1);
        }

        internal static EncounterPlanNodeBindingV1 FindBinding(EncounterPlanPinV1 pin, string nodeId)
        {
            for (int index = 0; index < pin.Bindings.Count; index++)
            {
                EncounterPlanNodeBindingV1 binding = pin.Bindings[index];
                if (string.Equals(binding.NodeId, nodeId, StringComparison.Ordinal)) return binding;
            }
            return null;
        }

        internal static bool BindingsEqual(EncounterPlanNodeBindingV1 left, EncounterPlanNodeBindingV1 right)
        {
            if (left == null || right == null) return left == right;
            return string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal)
                && left.GenerationVersion == right.GenerationVersion
                && string.Equals(left.TemplateId, right.TemplateId, StringComparison.Ordinal)
                && left.RoomInstanceId == right.RoomInstanceId
                && left.Mode == right.Mode
                && string.Equals(left.EncounterId, right.EncounterId, StringComparison.Ordinal)
                && left.DefinitionRevision == right.DefinitionRevision
                && string.Equals(left.DefinitionSha256, right.DefinitionSha256, StringComparison.Ordinal)
                && string.Equals(left.VariantId, right.VariantId, StringComparison.Ordinal)
                && left.CalendarOrdinal == right.CalendarOrdinal
                && string.Equals(left.ResolvedPlanSha256, right.ResolvedPlanSha256, StringComparison.Ordinal)
                && left.ContextPactIds.SequenceEqual(right.ContextPactIds, StringComparer.Ordinal);
        }

        internal static EncounterPlanNodeBindingV1 BuildBinding(ResolvedEncounterPlanV1 plan)
        {
            if (plan == null)
                throw Failure(EncounterPlanPinErrorCodeV1.NullInput, "plan", "A resolved encounter plan is required.");
            if (!plan.RunId.IsValid || !EncounterPlanNodeBindingV1.IsStableText(plan.NodeId)
                || !EncounterPlanNodeBindingV1.IsStableText(plan.TemplateId)
                || !plan.RoomInstanceId.IsValid || plan.GenerationVersion <= 0
                || !EncounterPlanNodeBindingV1.IsLowerSha256(plan.CatalogDigest)
                || !EncounterPlanNodeBindingV1.IsLowerSha256(plan.RouteDigest))
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan", "Resolved plan identity is malformed.");

            RoomInstanceId expectedRoom;
            try
            {
                expectedRoom = ExpeditionRoomIdentity.ForNode(plan.RunId, plan.NodeId);
            }
            catch (ArgumentException exception)
            {
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan.roomInstanceId", exception.Message);
            }
            if (expectedRoom != plan.RoomInstanceId)
                throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch, "plan.roomInstanceId",
                    "Resolved plan RoomInstanceId must equal ForNode(RunId, NodeId).");

            string definitionSha256 = null;
            string encounterId = null;
            int definitionRevision = 0;
            string variantId = null;
            if (plan.Mode == EncounterCatalogBindingMode.Encounter)
            {
                if (plan.SelectedVariant == null || !EncounterPlanNodeBindingV1.IsStableText(plan.EncounterId)
                    || plan.DefinitionRevision <= 0 || !EncounterPlanNodeBindingV1.IsLowerSha256(plan.DefinitionDigest)
                    || !EncounterPlanNodeBindingV1.IsStableText(plan.VariantId))
                    throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan.encounter",
                        "Encounter plan definition or selected variant is incomplete.");
                definitionSha256 = plan.DefinitionDigest;
                encounterId = plan.EncounterId;
                definitionRevision = plan.DefinitionRevision;
                variantId = plan.VariantId;
            }
            else if (plan.Mode != EncounterCatalogBindingMode.None)
            {
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan.mode", "Resolved plan mode is unsupported.");
            }

            string[] pactIds = plan.ContextPactIds.ToArray();
            string previous = null;
            for (int index = 0; index < pactIds.Length; index++)
            {
                string pactId = pactIds[index];
                if (!EncounterPlanNodeBindingV1.IsStableText(pactId) || !CanonicalPactIds.Contains(pactId)
                    || (previous != null && string.CompareOrdinal(previous, pactId) >= 0))
                    throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan.contextPactIds",
                        "Resolver context must contain known, unique, ordinally sorted pact IDs.");
                previous = pactId;
            }
            if (pactIds.Length > 2)
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "plan.contextPactIds", "At most two pact IDs are supported.");

            string planSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan);
            return new EncounterPlanNodeBindingV1(plan.NodeId, plan.GenerationVersion, plan.TemplateId,
                plan.RoomInstanceId, plan.Mode, encounterId, definitionRevision, definitionSha256,
                variantId, pactIds, 0, planSha256);
        }

        internal static void ValidatePlanAgainstRunAndManifest(ResolvedEncounterPlanV1 plan,
            EncounterPlanNodeBindingV1 binding, EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run, string catalogSha256, string routeSha256)
        {
            if (plan.RunId != run.RunId || plan.Seed != run.Seed
                || plan.GenerationVersion != run.Route.GenerationVersion
                || !string.Equals(plan.RouteDigest, routeSha256, StringComparison.Ordinal)
                || !string.Equals(plan.CatalogDigest, catalogSha256, StringComparison.Ordinal))
                throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch, "resolvedPlan",
                    "All captured plans must match the supplied run, route, and exact manifest digest.");
            if (manifest.CatalogRevision <= 0 || !EncounterPlanNodeBindingV1.IsStableText(manifest.CatalogId))
                throw Failure(EncounterPlanPinErrorCodeV1.InvalidPin, "manifest", "Manifest identity is invalid.");
            ExpeditionRouteNode node = null;
            for (int index = 0; index < run.Route.Nodes.Count; index++)
            {
                if (string.Equals(run.Route.Nodes[index].NodeId, binding.NodeId, StringComparison.Ordinal))
                {
                    node = run.Route.Nodes[index];
                    break;
                }
            }
            if (node == null || node.Kind != plan.NodeKind
                || !string.Equals(node.TemplateId, plan.TemplateId, StringComparison.Ordinal)
                || !manifest.SupportedRouteGenerationVersions.Contains(plan.GenerationVersion))
                throw Failure(EncounterPlanPinErrorCodeV1.PlanMismatch, "resolvedPlan.nodeId",
                    "Resolved plan must refer to the exact route node and supported generation.");
        }

        internal static void EnsureValid(EncounterPlanPinV1 pin, string path)
        {
            EncounterPlanPinErrorV1 error = Validate(pin);
            if (error.Code != EncounterPlanPinErrorCodeV1.None)
                throw new EncounterPlanPinValidationExceptionV1(
                    new EncounterPlanPinErrorV1(error.Code, path + "." + error.Path, error.Message));
        }

        internal static string ComputeRouteSha256(string canonicalForm)
        {
            byte[] preimage = StrictUtf8.GetBytes(canonicalForm);
            using (SHA256 sha256 = SHA256.Create()) return ToLowerHex(sha256.ComputeHash(preimage));
        }

        internal static EncounterPlanPinValidationExceptionV1 Failure(
            EncounterPlanPinErrorCodeV1 code, string path, string message)
        {
            return new EncounterPlanPinValidationExceptionV1(new EncounterPlanPinErrorV1(code, path, message));
        }

        private static bool IsLowerSha256(string value)
        {
            return EncounterPlanNodeBindingV1.IsLowerSha256(value);
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
    }
}
