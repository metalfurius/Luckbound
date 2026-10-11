using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct ActorId : IEquatable<ActorId>
    {
        public ActorId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("An actor identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static ActorId New()
        {
            return new ActorId(Guid.NewGuid());
        }

        public static ActorId Parse(string value)
        {
            if (!TryParse(value, out ActorId id))
            {
                throw new FormatException("ActorId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out ActorId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new ActorId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(ActorId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ActorId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(ActorId left, ActorId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ActorId left, ActorId right)
        {
            return !left.Equals(right);
        }
    }
}
