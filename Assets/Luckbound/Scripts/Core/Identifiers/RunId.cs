using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct RunId : IEquatable<RunId>
    {
        public RunId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A run identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static RunId New()
        {
            return new RunId(Guid.NewGuid());
        }

        public static RunId Parse(string value)
        {
            if (!TryParse(value, out RunId id))
            {
                throw new FormatException("RunId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out RunId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new RunId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(RunId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is RunId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(RunId left, RunId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RunId left, RunId right)
        {
            return !left.Equals(right);
        }
    }
}
