using System.Diagnostics.CodeAnalysis;

namespace DownloadData.ValueObjects
{
    public readonly struct TickerKey(ReadOnlySpan<char> value) : IEquatable<TickerKey>
    {
        private readonly string _value = value.ToString();
        private readonly int _hashCode = ComputeHashCode(value);
        public readonly bool Equals(TickerKey other)
        {
            return _value.AsSpan().SequenceEqual(other._value);
        }
        public override readonly bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is TickerKey key && key._value.AsSpan().SequenceEqual(_value);
        }
        public override readonly int GetHashCode() => _hashCode;
        private static int ComputeHashCode(ReadOnlySpan<char> value)
        {
            const int prime = 31;
            int hash = 0;
            foreach(ref readonly var v in value)
            {
                hash = (hash * prime + v) % int.MaxValue;
            }
            return hash;
        }
        public static implicit operator TickerKey(string value) => new(value);
        public static implicit operator TickerKey(ReadOnlySpan<char> value) => new(value);
        public static implicit operator string(TickerKey key) => key._value;
        public TickerKey ToTickerKey()
        {
            return this;
        }
        public override string ToString()
        {
            return _value;
        }
        public static bool operator ==(TickerKey left, TickerKey right) => left.Equals(right);
        public static bool operator !=(TickerKey left, TickerKey right) => !(left == right);
    }
}