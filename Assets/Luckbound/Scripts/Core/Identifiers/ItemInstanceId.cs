using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        public ItemInstanceId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("An item instance identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static ItemInstanceId New()
        {
            return new ItemInstanceId(Guid.NewGuid());
        }

        public static ItemInstanceId Parse(string value)
        {
            if (!TryParse(value, out ItemInstanceId id))
            {
                throw new FormatException("ItemInstanceId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out ItemInstanceId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new ItemInstanceId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(ItemInstanceId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(ItemInstanceId left, ItemInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemInstanceId left, ItemInstanceId right)
        {
            return !left.Equals(right);
        }
    }
}
