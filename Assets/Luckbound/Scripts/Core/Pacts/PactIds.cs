namespace Luckbound.Core.Pacts
{
    /// <summary>Stable content IDs for the pact definitions implemented by this package.</summary>
    public static class PactIds
    {
        public const string Avarice = "pact.avarice";
        public const string Fervor = "pact.fervor";
        public const string Haste = "pact.haste";
        public const string Silence = "pact.silence";

        public static bool IsKnown(string pactId)
        {
            return string.Equals(pactId, Avarice, System.StringComparison.Ordinal) ||
                string.Equals(pactId, Fervor, System.StringComparison.Ordinal) ||
                string.Equals(pactId, Haste, System.StringComparison.Ordinal) ||
                string.Equals(pactId, Silence, System.StringComparison.Ordinal);
        }

        internal static PactModifierTotals GetModifiers(string pactId)
        {
            switch (pactId)
            {
                case Avarice:
                    return new PactModifierTotals(35, 0, 15, 0, 0, 0, 0, 0);
                case Fervor:
                    return new PactModifierTotals(0, 20, 0, 25, 0, 0, 0, 0);
                case Haste:
                    return new PactModifierTotals(0, 0, 0, 0, 15, 2, 0, 0);
                case Silence:
                    return new PactModifierTotals(0, 0, 0, 0, 0, 0, 20, 25);
                default:
                    throw new System.ArgumentException("Unknown pact ID: " + pactId + ".", nameof(pactId));
            }
        }
    }
}
