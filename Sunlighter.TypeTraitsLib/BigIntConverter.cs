using System;
using System.Numerics;
using Sunlighter.OptionLib;

namespace Sunlighter.TypeTraitsLib
{
    public static class BigIntConverter
    {
        public static byte ConvertToByte(BigInteger b, OverflowBehavior ob)
        {
            switch(ob)
            {
                case OverflowBehavior.Wraparound:
                    return (byte)(b & byte.MaxValue);
                case OverflowBehavior.Saturate:
                    if (b < byte.MinValue) return byte.MinValue;
                    else if (b > byte.MaxValue) return byte.MaxValue;
                    else return (byte)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < byte.MinValue || b > byte.MaxValue) throw new OverflowException("Value out of range for Byte");
                    return (byte)b;
            }
        }

        public static Option<byte> TryConvertToByte(BigInteger b)
        {
            if (b < byte.MinValue || b > byte.MaxValue) return Option<byte>.None;
            return Option<byte>.Some((byte)b);
        }

        public static ushort ConvertToUInt16(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    return (ushort)(b & ushort.MaxValue);
                case OverflowBehavior.Saturate:
                    if (b < ushort.MinValue) return ushort.MinValue;
                    else if (b > ushort.MaxValue) return ushort.MaxValue;
                    else return (ushort)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < ushort.MinValue || b > ushort.MaxValue) throw new OverflowException("Value out of range for UInt16");
                    return (ushort)b;
            }
        }

        public static Option<ushort> TryConvertToUInt16(BigInteger b)
        {
            if (b < ushort.MinValue || b > ushort.MaxValue) return Option<ushort>.None;
            return Option<ushort>.Some((ushort)b);
        }

        public static uint ConvertToUInt32(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    return (uint)(b & uint.MaxValue);
                case OverflowBehavior.Saturate:
                    if (b < uint.MinValue) return uint.MinValue;
                    else if (b > uint.MaxValue) return uint.MaxValue;
                    else return (uint)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < uint.MinValue || b > uint.MaxValue) throw new OverflowException("Value out of range for UInt32");
                    return (uint)b;
            }
        }

        public static Option<uint> TryConvertToUInt32(BigInteger b)
        {
            if (b < uint.MinValue || b > uint.MaxValue) return Option<uint>.None;
            return Option<uint>.Some((uint)b);
        }

        public static ulong ConvertToUInt64(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    return (ulong)(b & ulong.MaxValue);
                case OverflowBehavior.Saturate:
                    if (b < ulong.MinValue) return ulong.MinValue;
                    else if (b > ulong.MaxValue) return ulong.MaxValue;
                    else return (ulong)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < ulong.MinValue || b > ulong.MaxValue) throw new OverflowException("Value out of range for UInt64");
                    return (ulong)b;
            }
        }

        public static Option<ulong> TryConvertToUInt64(BigInteger b)
        {
            if (b < ulong.MinValue || b > ulong.MaxValue) return Option<ulong>.None;
            return Option<ulong>.Some((ulong)b);
        }

        public static sbyte ConvertToSByte(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    byte b0 = (byte)(b & byte.MaxValue);
                    return unchecked((sbyte)b0);
                case OverflowBehavior.Saturate:
                    if (b < sbyte.MinValue) return sbyte.MinValue;
                    else if (b > sbyte.MaxValue) return sbyte.MaxValue;
                    else return (sbyte)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < sbyte.MinValue || b > sbyte.MaxValue) throw new OverflowException("Value out of range for SByte");
                    return (sbyte)b;
            }
        }

        public static Option<sbyte> TryConvertToSByte(BigInteger b)
        {
            if (b < sbyte.MinValue || b > sbyte.MaxValue) return Option<sbyte>.None;
            return Option<sbyte>.Some((sbyte)b);
        }

        public static short ConvertToInt16(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    ushort u0 = (ushort)(b & ushort.MaxValue);
                    return unchecked((short)u0);
                case OverflowBehavior.Saturate:
                    if (b < short.MinValue) return short.MinValue;
                    else if (b > short.MaxValue) return short.MaxValue;
                    else return (short)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < short.MinValue || b > short.MaxValue) throw new OverflowException("Value out of range for Int16");
                    return (short)b;
            }
        }

        public static Option<short> TryConvertToInt16(BigInteger b)
        {
            if (b < short.MinValue || b > short.MaxValue) return Option<short>.None;
            return Option<short>.Some((short)b);
        }

        public static int ConvertToInt32(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    uint u0 = (uint)(b & uint.MaxValue);
                    return unchecked((int)u0);
                case OverflowBehavior.Saturate:
                    if (b < int.MinValue) return int.MinValue;
                    else if (b > int.MaxValue) return int.MaxValue;
                    else return (int)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < int.MinValue || b > int.MaxValue) throw new OverflowException("Value out of range for Int32");
                    return (int)b;
            }
        }

        public static Option<int> TryConvertToInt32(BigInteger b)
        {
            if (b < int.MinValue || b > int.MaxValue) return Option<int>.None;
            return Option<int>.Some((int)b);
        }

        public static long ConvertToInt64(BigInteger b, OverflowBehavior ob)
        {
            switch (ob)
            {
                case OverflowBehavior.Wraparound:
                    ulong u0 = (ulong)(b & ulong.MaxValue);
                    return unchecked((long)u0);
                case OverflowBehavior.Saturate:
                    if (b < long.MinValue) return long.MinValue;
                    else if (b > long.MaxValue) return long.MaxValue;
                    else return (long)b;
                case OverflowBehavior.ThrowException:
                default:
                    if (b < long.MinValue || b > long.MaxValue) throw new OverflowException("Value out of range for Int64");
                    return (long)b;
            }
        }

        public static Option<long> TryConvertToInt64(BigInteger b)
        {
            if (b < long.MinValue || b > long.MaxValue) return Option<long>.None;
            return Option<long>.Some((long)b);
        }
    }

    public enum OverflowBehavior
    {
        Wraparound,
        Saturate,
        ThrowException
    }
}
