using System;
using System.Collections.Generic;

namespace Parallel.Compression.Models
{
    internal struct ByteMask
    {
        public static readonly ByteMask Any = new ByteMask(_ => true);

        public static ByteMask Between(byte min, byte max)
        {
            if (min > max)
                throw new ArgumentException("Minimum value is greater than maximum", nameof(min));
            return new ByteMask(b => b >= min && b <= max);
        }

        public static ByteMask Values(byte[] posibleValues)
        {
            var hashSet = new HashSet<byte>(posibleValues);
            return new ByteMask(b => hashSet.Contains(b));
        }

        public static ByteMask Exact(byte value) => new ByteMask(b => b == value);

        private readonly Func<byte, bool> isMatched;

        public ByteMask(Func<byte, bool> isMatched)
        {
            this.isMatched = isMatched;
        }

        public bool IsMatched(byte value)
        {
            return isMatched != null && isMatched(value);
        }

        public static implicit operator ByteMask(int value)
        {
            return Exact((byte) value);
        }

        public static implicit operator ByteMask(byte value)
        {
            return Exact(value);
        }

        public static implicit operator ByteMask(byte[] values)
        {
            return Values(values);
        }

        public static implicit operator ByteMask(byte? value)
        {
            return value.HasValue ? Exact(value.Value) : Any;
        }
    }
}