using System;

namespace Luckbound.Core.Identifiers
{
    internal static class IdParsing
    {
        public static bool TryParse(string value, out Guid parsed)
        {
            if (Guid.TryParseExact(value, "N", out parsed) && parsed != Guid.Empty)
            {
                return true;
            }

            parsed = Guid.Empty;
            return false;
        }

        public static string ToString(Guid value)
        {
            return value == Guid.Empty ? string.Empty : value.ToString("N").ToLowerInvariant();
        }
    }
}
