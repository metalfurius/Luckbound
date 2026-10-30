using System;

namespace Luckbound.Core.Expeditions
{
    public enum EncounterPlanResolutionErrorCodeV1
    {
        None = 0,
        NullManifest,
        NullRun,
        NullPactIds,
        InvalidResolutionContext,
        MissingResolutionAuthority,
        UnsupportedIdentityCodec,
        UnsupportedResolver,
        UnsupportedRouteGeneration,
        InvalidNodeId,
        RouteNodeNotFound,
        MissingBinding,
        AmbiguousBinding,
        UnsupportedBindingMode,
        MissingDefinition,
        AmbiguousDefinition,
        NoApplicableVariant,
        AmbiguousVariant,
        UnsupportedVariant,
        MissingDescriptor,
        InvalidPlanInput
    }

    public readonly struct EncounterPlanResolutionErrorV1
    {
        public EncounterPlanResolutionErrorV1(
            EncounterPlanResolutionErrorCodeV1 code,
            string path,
            string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public EncounterPlanResolutionErrorCodeV1 Code { get; }
        public string Path { get; }
        public string Message { get; }
    }
}
