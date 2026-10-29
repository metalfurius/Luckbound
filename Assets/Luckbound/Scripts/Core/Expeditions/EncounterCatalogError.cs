using System;

namespace Luckbound.Core.Expeditions
{
    public enum EncounterCatalogErrorCode
    {
        None,
        NullInput,
        InvalidSchema,
        UnsupportedIdentityCodec,
        InvalidValue,
        DuplicateIdentifier,
        MissingReference,
        ReferenceKindMismatch,
        SizeOverflow,
        InvalidUtf8,
        MalformedJson,
        DuplicateMember,
        UnknownMember,
        MissingMember,
        WrongJsonType,
        TrailingJson,
        CatalogRevisionConflict,
        CatalogRevisionUnavailable
    }

    public readonly struct EncounterCatalogError
    {
        public EncounterCatalogError(EncounterCatalogErrorCode code, string path, string message)
        {
            Code = code;
            Path = path;
            Message = message;
        }

        public EncounterCatalogErrorCode Code { get; }
        public string Path { get; }
        public string Message { get; }
    }
}
