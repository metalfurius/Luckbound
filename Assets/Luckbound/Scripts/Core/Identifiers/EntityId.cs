using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public EntityId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("An entity identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static EntityId New()
        {
            return new EntityId(Guid.NewGuid());
        }

        public static EntityId Parse(string value)
        {
            if (!TryParse(value, out EntityId id))
            {
                throw new FormatException("EntityId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out EntityId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new EntityId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(EntityId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(EntityId left, EntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EntityId left, EntityId right)
        {
            return !left.Equals(right);
        }
    }
}
