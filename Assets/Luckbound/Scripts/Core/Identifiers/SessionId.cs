using System;

namespace Luckbound.Core.Identifiers
{
    public readonly struct SessionId : IEquatable<SessionId>
    {
        public SessionId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A session identifier cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public bool IsValid => Value != Guid.Empty;

        public static SessionId New()
        {
            return new SessionId(Guid.NewGuid());
        }

        public static SessionId Parse(string value)
        {
            if (!TryParse(value, out SessionId id))
            {
                throw new FormatException("SessionId must be a non-empty GUID in N format.");
            }

            return id;
        }

        public static bool TryParse(string value, out SessionId id)
        {
            if (IdParsing.TryParse(value, out Guid parsed))
            {
                id = new SessionId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        public bool Equals(SessionId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is SessionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IdParsing.ToString(Value);
        }

        public static bool operator ==(SessionId left, SessionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SessionId left, SessionId right)
        {
            return !left.Equals(right);
        }
    }
}
