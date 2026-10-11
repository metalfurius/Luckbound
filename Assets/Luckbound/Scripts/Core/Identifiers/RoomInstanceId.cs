using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct RoomInstanceId : IEquatable<RoomInstanceId>
    {
        public RoomInstanceId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A room instance identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static RoomInstanceId New()
        {
            return new RoomInstanceId(Guid.NewGuid());
        }

        public static RoomInstanceId Parse(string value)
        {
            if (!TryParse(value, out RoomInstanceId id))
            {
                throw new FormatException("RoomInstanceId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out RoomInstanceId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new RoomInstanceId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(RoomInstanceId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is RoomInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(RoomInstanceId left, RoomInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoomInstanceId left, RoomInstanceId right)
        {
            return !left.Equals(right);
        }
    }
}
