using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int ComputeHashCode(char* value, int length)
        {
            const int prime = 31;
            int hash = 0;
            for (int i = 0; i < length; i++)
            {
                hash = (hash * prime + value[i]) % int.MaxValue;
            }
            return hash;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int ComputeHashCode(ReadOnlySpan<char> value)
        {
            fixed(char* ptr = value)
            {
                return ComputeHashCode(ptr, value.Length);
            }
        }
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