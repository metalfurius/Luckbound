using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct TransactionId : IEquatable<TransactionId>
    {
        public TransactionId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A transaction identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static TransactionId New()
        {
            return new TransactionId(Guid.NewGuid());
        }

        public static TransactionId Parse(string value)
        {
            if (!TryParse(value, out TransactionId id))
            {
                throw new FormatException("TransactionId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out TransactionId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new TransactionId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(TransactionId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is TransactionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(TransactionId left, TransactionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransactionId left, TransactionId right)
        {
            return !left.Equals(right);
        }
    }
}
