using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Expeditions;

namespace Luckbound.Content
{
    /// <summary>Restores a positive logical plan from the exact catalog revision pinned by a run.</summary>
    public static class EncounterPlanPinRestorerV1
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static bool TryRestore(EncounterPlanPinV1 pin, ExpeditionRunConfiguration run,
            string nodeId, EncounterCatalogRegistryV1 registry,
            out ResolvedEncounterPlanV1 plan, out EncounterPlanPinErrorV1 error)
        {
            plan = null;
            if (pin == null)
                return Fail(EncounterPlanPinErrorCodeV1.NullInput, "pin", "An encounter plan pin is required.", out plan, out error);
            if (run == null)
                return Fail(EncounterPlanPinErrorCodeV1.NullInput, "run", "An expedition run is required.", out plan, out error);
            if (nodeId == null)
                return Fail(EncounterPlanPinErrorCodeV1.NullInput, "nodeId", "A route node ID is required.", out plan, out error);
            if (registry == null)
                return Fail(EncounterPlanPinErrorCodeV1.NullInput, "registry", "An exact catalog registry is required.", out plan, out error);

            try
            {
                // Encode is the public Core validation boundary; it checks the entire immutable value
                // before the restorer performs any registry lookup.
                EncounterPlanPinCanonicalV1.Encode(pin);
            }
            catch (EncounterPlanPinValidationExceptionV1 exception)
            {
                error = exception.Error;
                plan = null;
                return false;
            }

            EncounterPlanNodeBindingV1 binding = null;
            for (int index = 0; index < pin.Bindings.Count; index++)
            {
                if (!string.Equals(pin.Bindings[index].NodeId, nodeId, StringComparison.Ordinal)) continue;
                binding = pin.Bindings[index];
                break;
            }
            if (binding == null)
                return Fail(EncounterPlanPinErrorCodeV1.MissingBinding, "nodeId",
                    "The pin has no binding for the requested route node.", out plan, out error);
            if (!string.Equals(pin.Entry.NodeId, nodeId, StringComparison.Ordinal)
                || !string.Equals(pin.Entry.ResolvedPlanSha256, binding.ResolvedPlanSha256, StringComparison.Ordinal))
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "entry",
                    "Restore is restricted to the exact node and digest named by the entry reference.", out plan, out error);

            if (pin.RunId != run.RunId)
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "run.runId",
                    "The supplied run does not match the pin RunId.", out plan, out error);

            string routeSha256;
            try
            {
                routeSha256 = ComputeRouteSha256(run.Route.CanonicalForm);
            }
            catch (EncoderFallbackException exception)
            {
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "run.route.canonicalForm",
                    "The route cannot be encoded as strict UTF-8: " + exception.Message, out plan, out error);
            }
            if (!string.Equals(routeSha256, pin.RouteSha256, StringComparison.Ordinal))
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "run.routeSha256",
                    "The supplied route does not match the pinned canonical route digest.", out plan, out error);

            ExpeditionRouteNode routeNode = null;
            for (int index = 0; index < run.Route.Nodes.Count; index++)
            {
                if (!string.Equals(run.Route.Nodes[index].NodeId, nodeId, StringComparison.Ordinal)) continue;
                routeNode = run.Route.Nodes[index];
                break;
            }
            if (routeNode == null)
                return Fail(EncounterPlanPinErrorCodeV1.MissingBinding, "nodeId",
                    "The requested node is not part of the supplied route.", out plan, out error);

            if (binding.GenerationVersion != run.Route.GenerationVersion
                || !string.Equals(binding.TemplateId, routeNode.TemplateId, StringComparison.Ordinal)
                || !ExpeditionRoomIdentity.ForNode(run.RunId, nodeId).Equals(binding.RoomInstanceId))
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "bindings.nodeId",
                    "The pinned generation, template, or deterministic room instance differs from the route.", out plan, out error);

            if (!registry.TryGetExact(pin.CatalogId, pin.CatalogRevision, pin.CatalogSha256,
                    out EncounterCatalogManifest manifest, out EncounterCatalogError registryError))
                return Fail(EncounterPlanPinErrorCodeV1.CatalogRevisionUnavailable,
                    string.IsNullOrEmpty(registryError.Path) ? "catalogRevision" : registryError.Path,
                    registryError.Message, out plan, out error);

            if (!string.Equals(manifest.IdentityCodec, pin.IdentityCodec, StringComparison.Ordinal)
                || !string.Equals(EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest),
                    pin.CatalogSha256, StringComparison.Ordinal))
                return Fail(EncounterPlanPinErrorCodeV1.CatalogRevisionUnavailable, "catalogSha256",
                    "Registry catalog bytes do not match the exact pinned digest and identity codec.", out plan, out error);

            if (!EncounterPlanResolverV1.TryResolve(manifest, run, nodeId, binding.ContextPactIds,
                    out ResolvedEncounterPlanV1 restored, out EncounterPlanResolutionErrorV1 resolutionError))
                return MapResolutionFailure(resolutionError, out plan, out error);

            string restoredSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(restored);
            if (restored.RunId != pin.RunId || restored.Seed != run.Seed
                || restored.GenerationVersion != binding.GenerationVersion
                || restored.NodeKind != routeNode.Kind
                || !string.Equals(restored.NodeId, binding.NodeId, StringComparison.Ordinal)
                || !string.Equals(restored.TemplateId, binding.TemplateId, StringComparison.Ordinal)
                || !restored.RoomInstanceId.Equals(binding.RoomInstanceId)
                || restored.Mode != binding.Mode
                || !restored.ContextPactIds.SequenceEqual(binding.ContextPactIds, StringComparer.Ordinal)
                || !string.Equals(restored.RouteDigest, pin.RouteSha256, StringComparison.Ordinal)
                || !string.Equals(restored.CatalogDigest, pin.CatalogSha256, StringComparison.Ordinal)
                || !string.Equals(restoredSha256, binding.ResolvedPlanSha256, StringComparison.Ordinal))
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "bindings.resolvedPlanSha256",
                    "The exact catalog and saved resolution context do not reproduce the pinned plan digest.", out plan, out error);

            if (binding.Mode == EncounterCatalogBindingMode.None)
            {
                if (restored.SelectedVariant != null || restored.DefinitionDigest != null
                    || restored.EncounterId != null || restored.DefinitionRevision != 0)
                    return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "bindings.mode",
                        "A None binding cannot carry encounter payload or certify a clear.", out plan, out error);
            }
            else if (!string.Equals(restored.EncounterId, binding.EncounterId, StringComparison.Ordinal)
                || restored.DefinitionRevision != binding.DefinitionRevision
                || !string.Equals(restored.DefinitionDigest, binding.DefinitionSha256, StringComparison.Ordinal)
                || !string.Equals(restored.VariantId, binding.VariantId, StringComparison.Ordinal))
            {
                return Fail(EncounterPlanPinErrorCodeV1.PlanMismatch, "bindings.encounter",
                    "Restored definition, revision, digest, or variant differs from the pinned binding.", out plan, out error);
            }

            plan = restored;
            error = default(EncounterPlanPinErrorV1);
            return true;
        }

        private static bool MapResolutionFailure(EncounterPlanResolutionErrorV1 resolutionError,
            out ResolvedEncounterPlanV1 plan, out EncounterPlanPinErrorV1 error)
        {
            EncounterPlanPinErrorCodeV1 code;
            switch (resolutionError.Code)
            {
                case EncounterPlanResolutionErrorCodeV1.MissingResolutionAuthority:
                case EncounterPlanResolutionErrorCodeV1.UnsupportedIdentityCodec:
                case EncounterPlanResolutionErrorCodeV1.UnsupportedResolver:
                case EncounterPlanResolutionErrorCodeV1.UnsupportedRouteGeneration:
                case EncounterPlanResolutionErrorCodeV1.NoApplicableVariant:
                case EncounterPlanResolutionErrorCodeV1.AmbiguousVariant:
                case EncounterPlanResolutionErrorCodeV1.UnsupportedVariant:
                    code = EncounterPlanPinErrorCodeV1.MissingResolutionAuthority;
                    break;
                case EncounterPlanResolutionErrorCodeV1.MissingDescriptor:
                case EncounterPlanResolutionErrorCodeV1.MissingDefinition:
                    code = EncounterPlanPinErrorCodeV1.CatalogRevisionUnavailable;
                    break;
                case EncounterPlanResolutionErrorCodeV1.MissingBinding:
                case EncounterPlanResolutionErrorCodeV1.RouteNodeNotFound:
                    code = EncounterPlanPinErrorCodeV1.MissingBinding;
                    break;
                default:
                    code = EncounterPlanPinErrorCodeV1.PlanMismatch;
                    break;
            }

            plan = null;
            error = new EncounterPlanPinErrorV1(code, resolutionError.Path, resolutionError.Message);
            return false;
        }

        private static string ComputeRouteSha256(string canonicalForm)
        {
            byte[] preimage = StrictUtf8.GetBytes(canonicalForm);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(preimage);
                const string digits = "0123456789abcdef";
                var characters = new char[digest.Length * 2];
                for (int index = 0; index < digest.Length; index++)
                {
                    characters[index * 2] = digits[digest[index] >> 4];
                    characters[index * 2 + 1] = digits[digest[index] & 0x0f];
                }
                return new string(characters);
            }
        }

        private static bool Fail(EncounterPlanPinErrorCodeV1 code, string path, string message,
            out ResolvedEncounterPlanV1 plan, out EncounterPlanPinErrorV1 error)
        {
            plan = null;
            error = new EncounterPlanPinErrorV1(code, path, message);
            return false;
        }
    }
}
