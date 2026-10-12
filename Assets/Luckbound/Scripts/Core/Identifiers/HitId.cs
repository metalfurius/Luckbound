using System;

namespace Luckbound.Core.Identifiers
{
    /// <summary>Identifies one transient damage window and is never persisted.</summary>
    public readonly struct HitId : IEquatable<HitId>
    {
        public HitId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A hit identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static HitId New()
        {
            return new HitId(Guid.NewGuid());
        }

        public bool Equals(HitId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is HitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("N");
        }

        public static bool operator ==(HitId left, HitId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HitId left, HitId right)
        {
            return !left.Equals(right);
        }
    }
}
