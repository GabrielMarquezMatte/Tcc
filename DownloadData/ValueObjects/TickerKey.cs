using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DownloadData.ValueObjects
{
    public readonly struct TickerKey(ReadOnlySpan<char> value) : IEquatable<TickerKey>
    {
        private readonly string _value = value.ToString();
        private readonly int _hashCode = ComputeHashCode(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(TickerKey other)
        {
            return _value.Equals(other._value, StringComparison.Ordinal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is TickerKey key && key._value.Equals(_value, StringComparison.Ordinal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode()
        {
            return _hashCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeHashCode(ReadOnlySpan<char> value)
        {
            const int prime = 31;
            int hash = 0;
            foreach (ref readonly var v in value)
            {
                hash = (hash * prime + v) % int.MaxValue;
            }
            return hash;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TickerKey(ReadOnlySpan<char> value)
        {
            return new(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator string(TickerKey key)
        {
            return key._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TickerKey ToTickerKey()
        {
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return _value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TickerKey left, TickerKey right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TickerKey left, TickerKey right)
        {
            return !(left == right);
        }
    }
}