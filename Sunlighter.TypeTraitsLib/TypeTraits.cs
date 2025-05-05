using Sunlighter.OptionLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Sunlighter.TypeTraitsLib
{
    public interface ITypeTraits<T>
    {
        int Compare(T a, T b);
        void AddToHash(HashBuilder b, T a);
        
        bool CanSerialize(T a);
        void Serialize(Serializer dest, T a);
        T Deserialize(Deserializer src);
        void MeasureBytes(ByteMeasurer measurer, T a);

        void AppendDebugString(DebugStringBuilder sb, T a);
    }

    public static partial class Extensions
    {
        public static byte[] SerializeToBytes<T>(this ITypeTraits<T> traits, T a)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter sw = new BinaryWriter(ms, Encoding.UTF8))
                {
                    Serializer s = new Serializer(sw);
                    traits.Serialize(s, a);
                    s.RunQueue();
                    return ms.ToArray();
                }
            }
        }

        public static T DeserializeFromBytes<T>(this ITypeTraits<T> traits, byte[] b)
        {
            using (MemoryStream ms = new MemoryStream(b))
            {
                using (BinaryReader sr = new BinaryReader(ms, Encoding.UTF8))
                {
                    Deserializer d = new Deserializer(sr);
                    T value = traits.Deserialize(d);
                    d.RunQueue();
                    return value;
                }
            }
        }

        public static long MeasureAllBytes<T>(this ITypeTraits<T> traits, T item)
        {
            ByteMeasurer measurer = new ByteMeasurer();
            traits.MeasureBytes(measurer, item);
            measurer.RunQueue();
            return measurer.Count;
        }

        public static void SerializeToFile<T>(this ITypeTraits<T> traits, string filePath, T a)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath).AssertNotNull());
            }

            using (FileStream fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 18, FileOptions.None))
            {
                using (BinaryWriter sw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    Serializer s = new Serializer(sw);
                    traits.Serialize(s, a);
                }
            }
        }

        public static T DeserializeFromFile<T>(this ITypeTraits<T> traits, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 18, FileOptions.None))
            {
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                {
                    Deserializer d = new Deserializer(br);
                    return traits.Deserialize(d);
                }
            }
        }

        public static T LoadOrGenerate<T>(this ITypeTraits<T> traits, string filePath, Func<T> generate)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    return traits.DeserializeFromFile(filePath);
                }
                catch(Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not read {filePath}: {exc.GetType().FullName}: {exc.Message}");
                }
            }
            
            T result = generate();
            traits.SerializeToFile(filePath, result);
            return result;
        }

        public static int GetBasicHashCode<T>(this ITypeTraits<T> traits, T a)
        {
            BasicHashBuilder hb = new BasicHashBuilder();
            traits.AddToHash(hb, a);
            return hb.Result;
        }

        public static byte[] GetSHA256Hash<T>(this ITypeTraits<T> traits, T a)
        {
            using (SHA256HashBuilder hb = new SHA256HashBuilder())
            {
                traits.AddToHash(hb, a);
                return hb.Result;
            }
        }

        public static string ToDebugString<T>(this ITypeTraits<T> traits, T a)
        {
            DebugStringBuilder sb = new DebugStringBuilder();
            traits.AppendDebugString(sb, a);
            sb.RunQueue();
            return sb.Builder.ToString();
        }
    }

#if NETSTANDARD2_0
    public abstract class Adapter<T> : IEqualityComparer<T>, IComparer<T>
    {
        protected readonly ITypeTraits<T> itemTraits;

        protected Adapter(ITypeTraits<T> itemTraits)
        {
            this.itemTraits = itemTraits;
        }

        public ITypeTraits<T> TypeTraits => itemTraits;

        public abstract int Compare(T x, T y);

        public abstract bool Equals(T x, T y);

        public int GetHashCode(T obj)
        {
            return itemTraits.GetBasicHashCode(obj);
        }

        public static Adapter<T> Create(ITypeTraits<T> itemTraits)
        {
            if (typeof(T).IsClass)
            {
                Type ac = typeof(AdapterForClass<>).MakeGenericType(typeof(T));
                ConstructorInfo ci = ac.GetRequiredConstructor(new Type[] { typeof(ITypeTraits<>).MakeGenericType(typeof(T)) });
                return (Adapter<T>)ci.Invoke(new object[] { itemTraits });
            }
            else
            {
                Type ac = typeof(AdapterForStruct<>).MakeGenericType(typeof(T));
                ConstructorInfo ci = ac.GetRequiredConstructor(new Type[] { typeof(ITypeTraits<>).MakeGenericType(typeof(T)) });
                return (Adapter<T>)ci.Invoke(new object[] { itemTraits });
            }
        }
    }

    public sealed class AdapterForClass<T> : Adapter<T>
        where T : class
    {
        public AdapterForClass(ITypeTraits<T> itemTraits)
            : base(itemTraits)
        {

        }

        public override int Compare(T x, T y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return itemTraits.Compare(x, y);
        }

        public override bool Equals(T x, T y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;

            return itemTraits.Compare(x, y) == 0;
        }
    }

    public sealed class AdapterForStruct<T> : Adapter<T>
        where T : struct
    {
        public AdapterForStruct(ITypeTraits<T> itemTraits)
            : base(itemTraits)
        {

        }

        public override int Compare(T x, T y)
        {
            return itemTraits.Compare(x, y);
        }

        public override bool Equals(T x, T y)
        {
            return itemTraits.Compare(x, y) == 0;
        }
    }
#else
    public sealed class Adapter<T> : IEqualityComparer<T>, IComparer<T>
    {
        private readonly ITypeTraits<T> itemTraits;

        private Adapter(ITypeTraits<T> itemTraits)
        {
            this.itemTraits = itemTraits;
        }

        public ITypeTraits<T> TypeTraits => itemTraits;

        public int Compare(T? x, T? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return itemTraits.Compare(x, y);
        }

        public bool Equals(T? x, T? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return itemTraits.Compare(x, y) == 0;
        }

        public int GetHashCode(T obj)
        {
            return itemTraits.GetBasicHashCode(obj);
        }

        public static Adapter<T> Create(ITypeTraits<T> itemTraits)
        {
            return new Adapter<T>(itemTraits);
        }
    }
#endif

    public sealed class StringTypeTraits : ITypeTraits<string>
    {
        private StringTypeTraits() { }

        public static StringTypeTraits Value { get; } = new StringTypeTraits();

        public int Compare(string a, string b)
        {
            return string.Compare(a, b, StringComparison.Ordinal);
        }

        public void AddToHash(HashBuilder b, string a)
        {
            b.Add(HashToken.String);
            b.Add(Encoding.UTF8.GetBytes(a));
        }

        public bool CanSerialize(string a) => true;

        public void Serialize(Serializer dest, string a)
        {
            dest.Writer.Write(a);
        }

        public string Deserialize(Deserializer src)
        {
            return src.Reader.ReadString();
        }

        public void MeasureBytes(ByteMeasurer measurer, string a)
        {
            int len = Encoding.UTF8.GetByteCount(a);
            int lenlen =
                (len < (1 << 7)) ? 1 :
                (len < (1 << 14)) ? 2 :
                (len < (1 << 21)) ? 3 :
                (len < (1 << 28)) ? 4 : 5;

            measurer.AddBytes((long)lenlen + len);
        }

        public void AppendDebugString(DebugStringBuilder sb, string a)
        {
            sb.Builder.AppendQuoted(a);
        }
    }

    public sealed class CharTypeTraits : ITypeTraits<char>
    {
        private CharTypeTraits() { }

        public static CharTypeTraits Value { get; } = new CharTypeTraits();

        public int Compare(char a, char b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, char a)
        {
            b.Add(HashToken.Char);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(char a) => true;

        public void Serialize(Serializer dest, char a)
        {
            dest.Writer.Write(a);
        }

        public char Deserialize(Deserializer src)
        {
            return src.Reader.ReadChar();
        }

        public void MeasureBytes(ByteMeasurer measurer, char a)
        {
            measurer.AddBytes(2L);
        }

        public void AppendDebugString(DebugStringBuilder sb, char a)
        {
            sb.Builder.AppendCharName(a);
        }
    }

    public sealed class ByteTypeTraits : ITypeTraits<byte>
    {
        private ByteTypeTraits() { }

        public static ByteTypeTraits Value { get; } = new ByteTypeTraits();

        public int Compare(byte a, byte b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, byte a)
        {
            b.Add(HashToken.Byte);
            b.Add(a);
        }

        public bool CanSerialize(byte a) => true;

        public void Serialize(Serializer dest, byte a)
        {
            dest.Writer.Write(a);
        }

        public byte Deserialize(Deserializer src)
        {
            return src.Reader.ReadByte();
        }

        public void MeasureBytes(ByteMeasurer measurer, byte a)
        {
            measurer.AddBytes(1L);
        }

        public void AppendDebugString(DebugStringBuilder sb, byte a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class SByteTypeTraits : ITypeTraits<sbyte>
    {
        private SByteTypeTraits() { }

        public static SByteTypeTraits Value { get; } = new SByteTypeTraits();

        public int Compare(sbyte a, sbyte b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, sbyte a)
        {
            b.Add(HashToken.SByte);
            b.Add((byte)a);
        }

        public bool CanSerialize(sbyte a) => true;

        public void Serialize(Serializer dest, sbyte a)
        {
            dest.Writer.Write(a);
        }

        public sbyte Deserialize(Deserializer src)
        {
            return src.Reader.ReadSByte();
        }

        public void MeasureBytes(ByteMeasurer measurer, sbyte a)
        {
            measurer.AddBytes(1L);
        }

        public void AppendDebugString(DebugStringBuilder sb, sbyte a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class Int16TypeTraits : ITypeTraits<short>
    {
        private Int16TypeTraits() { }

        public static Int16TypeTraits Value { get; } = new Int16TypeTraits();

        public int Compare(short a, short b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, short a)
        {
            b.Add(HashToken.Int16);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(short a) => true;

        public void Serialize(Serializer dest, short a)
        {
            dest.Writer.Write(a);
        }

        public short Deserialize(Deserializer src)
        {
            return src.Reader.ReadInt16();
        }

        public void MeasureBytes(ByteMeasurer measurer, short a)
        {
            measurer.AddBytes(2L);
        }

        public void AppendDebugString(DebugStringBuilder sb, short a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class UInt16TypeTraits : ITypeTraits<ushort>
    {
        private UInt16TypeTraits() { }

        public static UInt16TypeTraits Value { get; } = new UInt16TypeTraits();

        public int Compare(ushort a, ushort b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, ushort a)
        {
            b.Add(HashToken.UInt16);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(ushort a) => true;

        public void Serialize(Serializer dest, ushort a)
        {
            dest.Writer.Write(a);
        }

        public ushort Deserialize(Deserializer src)
        {
            return src.Reader.ReadUInt16();
        }

        public void MeasureBytes(ByteMeasurer measurer, ushort a)
        {
            measurer.AddBytes(2L);
        }

        public void AppendDebugString(DebugStringBuilder sb, ushort a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class Int32TypeTraits : ITypeTraits<int>
    {
        private Int32TypeTraits() { }

        public static Int32TypeTraits Value { get; } = new Int32TypeTraits();

        public int Compare(int a, int b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, int a)
        {
            b.Add(HashToken.Int32);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(int a) => true;

        public void Serialize(Serializer dest, int a)
        {
            dest.Writer.Write(a);
        }

        public int Deserialize(Deserializer src)
        {
            return src.Reader.ReadInt32();
        }

        public void MeasureBytes(ByteMeasurer measurer, int a)
        {
            measurer.AddBytes(4L);
        }

        public void AppendDebugString(DebugStringBuilder sb, int a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class UInt32TypeTraits : ITypeTraits<uint>
    {
        private UInt32TypeTraits() { }

        public static UInt32TypeTraits Value { get; } = new UInt32TypeTraits();

        public int Compare(uint a, uint b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, uint a)
        {
            b.Add(HashToken.UInt32);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(uint a) => true;

        public void Serialize(Serializer dest, uint a)
        {
            dest.Writer.Write(a);
        }

        public uint Deserialize(Deserializer src)
        {
            return src.Reader.ReadUInt32();
        }

        public void MeasureBytes(ByteMeasurer measurer, uint a)
        {
             measurer.AddBytes(4L);
        }

        public void AppendDebugString(DebugStringBuilder sb, uint a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class Int64TypeTraits : ITypeTraits<long>
    {
        private Int64TypeTraits() { }

        public static Int64TypeTraits Value { get; } = new Int64TypeTraits();

        public int Compare(long a, long b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, long a)
        {
            b.Add(HashToken.Int64);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(long a) => true;

        public void Serialize(Serializer dest, long a)
        {
            dest.Writer.Write(a);
        }

        public long Deserialize(Deserializer src)
        {
            return src.Reader.ReadInt64();
        }

        public void MeasureBytes(ByteMeasurer measurer, long a)
        {
            measurer.AddBytes(8L);
        }

        public void AppendDebugString(DebugStringBuilder sb, long a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class UInt64TypeTraits : ITypeTraits<ulong>
    {
        private UInt64TypeTraits() { }

        public static UInt64TypeTraits Value { get; } = new UInt64TypeTraits();

        public int Compare(ulong a, ulong b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void AddToHash(HashBuilder b, ulong a)
        {
            b.Add(HashToken.UInt64);
            b.Add(BitConverter.GetBytes(a));
        }

        public bool CanSerialize(ulong a) => true;

        public void Serialize(Serializer dest, ulong a)
        {
            dest.Writer.Write(a);
        }

        public ulong Deserialize(Deserializer src)
        {
            return src.Reader.ReadUInt64();
        }

        public void MeasureBytes(ByteMeasurer measurer, ulong a)
        {
            measurer.AddBytes(8L);
        }

        public void AppendDebugString(DebugStringBuilder sb, ulong a)
        {
            sb.Builder.Append(a);
        }
    }

    public static class SingleTypeTraits
    {
        private static readonly Lazy<ITypeTraits<float>> value = new Lazy<ITypeTraits<float>>(GetTypeTraits, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<float> GetTypeTraits()
        {
            return new ConvertTypeTraitsDebugOverride<float, int>
            (
#if NETSTANDARD2_0
                f => SingleToInt32Bits(f),
#else
                f => BitConverter.SingleToInt32Bits(f),
#endif
                Int32TypeTraits.Value,
#if NETSTANDARD2_0
                i => Int32BitsToSingle(i),
#else
                i => BitConverter.Int32BitsToSingle(i),
#endif
                (sb, f) => sb.Builder.Append(f)
            );
        }

        public static ITypeTraits<float> Value => value.Value;

#if NETSTANDARD2_0
        public static int SingleToInt32Bits(float f)
        {
            byte[] fBytes = BitConverter.GetBytes(f);
            return BitConverter.ToInt32(fBytes, 0);
        }

        public static float Int32BitsToSingle(int i)
        {
            byte[] iBytes = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(iBytes, 0);
        }
#endif
    }

    public static class DoubleTypeTraits
    {
        private static readonly Lazy<ITypeTraits<double>> value = new Lazy<ITypeTraits<double>>(GetTypeTraits, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<double> GetTypeTraits()
        {
            return new ConvertTypeTraitsDebugOverride<double, long>
            (
                d => BitConverter.DoubleToInt64Bits(d),
                Int64TypeTraits.Value,
                l => BitConverter.Int64BitsToDouble(l),
                (sb, d) => sb.Builder.Append(d)
            );
        }

        public static ITypeTraits<double> Value => value.Value;
    }

    public sealed class BigIntegerTypeTraits : ITypeTraits<BigInteger>
    {
        private BigIntegerTypeTraits() { }

        public static BigIntegerTypeTraits Value { get; } = new BigIntegerTypeTraits();

        public int Compare(BigInteger a, BigInteger b)
        {
            return a.CompareTo(b);
        }

        public void AddToHash(HashBuilder b, BigInteger a)
        {
            b.Add(HashToken.BigInt);
            byte[] aBytes = a.ToByteArray();
            b.Add(aBytes.Length);
            b.Add(aBytes);
        }

        public bool CanSerialize(BigInteger a) => true;

        public void Serialize(Serializer dest, BigInteger a)
        {
            byte[] aBytes = a.ToByteArray();
            dest.Writer.Write(aBytes.Length);
            dest.Writer.Write(aBytes);
        }

        public BigInteger Deserialize(Deserializer src)
        {
            int len = src.Reader.ReadInt32();
            byte[] aBytes = src.Reader.ReadBytes(len);
            return new BigInteger(aBytes);
        }

        public void MeasureBytes(ByteMeasurer measurer, BigInteger a)
        {
            measurer.AddBytes(4L + a.GetByteCount());
        }

        public void AppendDebugString(DebugStringBuilder sb, BigInteger a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class ByteArrayTypeTraits : ITypeTraits<byte[]>
    {
        private ByteArrayTypeTraits() { }

        public static ByteArrayTypeTraits Value { get; } = new ByteArrayTypeTraits();

        public int Compare(byte[] a, byte[] b)
        {
            int i = 0;
            while(true)
            {
                if (i >= a.Length && i >= b.Length) return 0;
                if (i >= a.Length) return -1;
                if (i >= b.Length) return 1;
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
                ++i;
            }
        }

        public void AddToHash(HashBuilder b, byte[] a)
        {
            b.Add(HashToken.ByteArray);
            b.Add(a.Length);
            b.Add(a);
        }

        public bool CanSerialize(byte[] a) => true;

        public void Serialize(Serializer dest, byte[] a)
        {
            dest.Writer.Write(a.Length);
            dest.Writer.Write(a);
        }

        public byte[] Deserialize(Deserializer src)
        {
            int length = src.Reader.ReadInt32();
            return src.Reader.ReadBytes(length);
        }

        public void MeasureBytes(ByteMeasurer measurer, byte[] a)
        {
            measurer.AddBytes(4L + a.Length);
        }

        public void AppendDebugString(DebugStringBuilder sb, byte[] a)
        {
            sb.Builder.Append("(bytes ");
            sb.Builder.AppendHex(a);
            sb.Builder.Append(')');
        }
    }

    public sealed class FixedLengthByteArrayTypeTraits : ITypeTraits<byte[]>
    {
        private readonly int length;

        public FixedLengthByteArrayTypeTraits(int length)
        {
            this.length = length;
        }

        public int Compare(byte[] a, byte[] b)
        {
            if (a.Length != length) throw new ArgumentException($"{nameof(a)} has incorrect length, expected {length}, got {a.Length}");
            if (b.Length != length) throw new ArgumentException($"{nameof(b)} has incorrect length, expected {length}, got {b.Length}");

            int i = 0;
            while (true)
            {
                if (i >= a.Length) return 0;
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
                ++i;
            }
        }

        public void AddToHash(HashBuilder b, byte[] a)
        {
            if (a.Length != length) throw new ArgumentException($"{nameof(a)} has incorrect length, expected {length}, got {a.Length}");

            b.Add(HashToken.ByteArray);
            b.Add(length);
            b.Add(a);
        }

        public bool CanSerialize(byte[] a) => a.Length == length;

        public void Serialize(Serializer dest, byte[] a)
        {
            if (a.Length != length) throw new ArgumentException($"{nameof(a)} has incorrect length, expected {length}, got {a.Length}");

            dest.Writer.Write(a);
        }

        public byte[] Deserialize(Deserializer src)
        {
            return src.Reader.ReadBytes(length);
        }

        public void MeasureBytes(ByteMeasurer measurer, byte[] a)
        {
            if (a.Length != length) throw new ArgumentException($"{nameof(a)} has incorrect length, expected {length}, got {a.Length}");

            measurer.AddBytes(length);
        }

        public void AppendDebugString(DebugStringBuilder sb, byte[] a)
        {
            if (a.Length != length) throw new ArgumentException($"{nameof(a)} has incorrect length, expected {length}, got {a.Length}");

            sb.Builder.Append("(bytes ");
            sb.Builder.AppendHex(a);
            sb.Builder.Append(')');
        }
    }

    public sealed class ImmutableByteArrayTypeTraits : ITypeTraits<ImmutableArray<byte>>
    {
        private ImmutableByteArrayTypeTraits() { }

        public static ImmutableByteArrayTypeTraits Value { get; } = new ImmutableByteArrayTypeTraits();

        public int Compare(ImmutableArray<byte> a, ImmutableArray<byte> b)
        {
            int i = 0;
            while (true)
            {
                if (i >= a.Length && i >= b.Length) return 0;
                if (i >= a.Length) return -1;
                if (i >= b.Length) return 1;
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
                ++i;
            }
        }

        public void AddToHash(HashBuilder b, ImmutableArray<byte> a)
        {
            b.Add(HashToken.ByteArray);
            b.Add(a.Length);
            b.Add(a.AsSpan().ToArray());
        }

        public bool CanSerialize(ImmutableArray<byte> a) => true;

        public void Serialize(Serializer dest, ImmutableArray<byte> a)
        {
            dest.Writer.Write(a.Length);
#if NETSTANDARD2_0
            dest.Writer.Write(a.ToArray());
#else
            dest.Writer.Write(a.AsSpan());
#endif
        }

        public ImmutableArray<byte> Deserialize(Deserializer src)
        {
            int length = src.Reader.ReadInt32();
            return ImmutableArray.Create(src.Reader.ReadBytes(length));
        }

        public void MeasureBytes(ByteMeasurer measurer, ImmutableArray<byte> a)
        {
            measurer.AddBytes(4L + a.Length);
        }

        public void AppendDebugString(DebugStringBuilder sb, ImmutableArray<byte> a)
        {
            sb.Builder.Append("(bytes ");
            sb.Builder.AppendHex(a.ToArray());
            sb.Builder.Append(')');
        }
    }

    public sealed class BooleanTypeTraits : ITypeTraits<bool>
    {
        private BooleanTypeTraits() { }

        public static BooleanTypeTraits Value { get; } = new BooleanTypeTraits();

        public int Compare(bool a, bool b)
        {
            if (a == b) return 0;
            if (!a) return -1;
            return 1;
        }

        public void AddToHash(HashBuilder b, bool a)
        {
            b.Add(HashToken.Boolean);
            b.Add(a ? (byte)1 : (byte)0);
        }

        public bool CanSerialize(bool a) => true;

        public void Serialize(Serializer dest, bool item)
        {
            dest.Writer.Write(item);
        }

        public bool Deserialize(Deserializer src)
        {
            return src.Reader.ReadBoolean();
        }

        public void MeasureBytes(ByteMeasurer measurer, bool a)
        {
            measurer.AddBytes(1L);
        }

        public void AppendDebugString(DebugStringBuilder sb, bool a)
        {
            sb.Builder.Append(a);
        }
    }

    public sealed class DateTimeTypeTraits
    {
        private DateTimeTypeTraits() { }

        private static readonly Lazy<ITypeTraits<DateTime>> value = new Lazy<ITypeTraits<DateTime>>(GetValue, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<DateTime> GetValue()
        {
            return new ConvertTypeTraits<DateTime, long>
            (
                d => d.ToBinary(),
                Int64TypeTraits.Value,
                L => DateTime.FromBinary(L)
            );
        }

        public static ITypeTraits<DateTime> Value => value.Value;

        private static readonly Lazy<Adapter<DateTime>> adapter = new Lazy<Adapter<DateTime>>(() => Adapter<DateTime>.Create(value.Value), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Adapter<DateTime> Adapter => adapter.Value;
    }

    public sealed class GuidTypeTraits
    {
        private GuidTypeTraits() { }

        private static readonly Lazy<ITypeTraits<Guid>> value = new Lazy<ITypeTraits<Guid>>(GetValue, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<Guid> GetValue()
        {
            return new ConvertTypeTraits<Guid, byte[]>
            (
                g => g.ToByteArray(),
                new FixedLengthByteArrayTypeTraits(16),
                buf => new Guid(buf)
            );
        }

        public static ITypeTraits<Guid> Value => value.Value;

        private static readonly Lazy<Adapter<Guid>> adapter = new Lazy<Adapter<Guid>>(() => Adapter<Guid>.Create(value.Value), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Adapter<Guid> Adapter => adapter.Value;
    }

    public sealed class TupleTypeTraits<T, U> : ITypeTraits<Tuple<T, U>>
    {
        private readonly ITypeTraits<T> item1Traits;
        private readonly ITypeTraits<U> item2Traits;

        public TupleTypeTraits(ITypeTraits<T> item1Traits, ITypeTraits<U> item2Traits)
        {
            this.item1Traits = item1Traits;
            this.item2Traits = item2Traits;
        }

        public int Compare(Tuple<T, U> a, Tuple<T, U> b)
        {
            int r = item1Traits.Compare(a.Item1, b.Item1);
            if (r != 0) return r;
            return item2Traits.Compare(a.Item2, b.Item2);
        }

        public void AddToHash(HashBuilder b, Tuple<T, U> a)
        {
            b.Add(HashToken.Tuple2);
            item1Traits.AddToHash(b, a.Item1);
            item2Traits.AddToHash(b, a.Item2);
        }

        public bool CanSerialize(Tuple<T, U> a) => item1Traits.CanSerialize(a.Item1) && item2Traits.CanSerialize(a.Item2);

        public void Serialize(Serializer dest, Tuple<T, U> item)
        {
            item1Traits.Serialize(dest, item.Item1);
            item2Traits.Serialize(dest, item.Item2);
        }

        public Tuple<T, U> Deserialize(Deserializer src)
        {
            T item1 = item1Traits.Deserialize(src);
            U item2 = item2Traits.Deserialize(src);
            return new Tuple<T, U>(item1, item2);
        }

        public void MeasureBytes(ByteMeasurer measurer, Tuple<T, U> a)
        {
            item1Traits.MeasureBytes(measurer, a.Item1);
            item2Traits.MeasureBytes(measurer, a.Item2);
        }

        public void AppendDebugString(DebugStringBuilder sb, Tuple<T, U> a)
        {
            sb.Builder.Append("(tuple2 ");
            item1Traits.AppendDebugString(sb, a.Item1);
            sb.Builder.Append(", ");
            item2Traits.AppendDebugString(sb, a.Item2);
            sb.Builder.Append(')');
        }
    }

    public sealed class ValueTupleTypeTraits<T, U> : ITypeTraits<(T, U)>
    {
        private readonly ITypeTraits<T> item1Traits;
        private readonly ITypeTraits<U> item2Traits;

        public ValueTupleTypeTraits(ITypeTraits<T> item1Traits, ITypeTraits<U> item2Traits)
        {
            this.item1Traits = item1Traits;
            this.item2Traits = item2Traits;
        }

        public int Compare((T, U) a, (T, U) b)
        {
            int r = item1Traits.Compare(a.Item1, b.Item1);
            if (r != 0) return r;
            return item2Traits.Compare(a.Item2, b.Item2);
        }

        public void AddToHash(HashBuilder b, (T, U) a)
        {
            b.Add(HashToken.Tuple2);
            item1Traits.AddToHash(b, a.Item1);
            item2Traits.AddToHash(b, a.Item2);
        }

        public bool CanSerialize((T, U) a) => item1Traits.CanSerialize(a.Item1) && item2Traits.CanSerialize(a.Item2);

        public void Serialize(Serializer dest, (T, U) item)
        {
            item1Traits.Serialize(dest, item.Item1);
            item2Traits.Serialize(dest, item.Item2);
        }

        public (T, U) Deserialize(Deserializer src)
        {
            T item1 = item1Traits.Deserialize(src);
            U item2 = item2Traits.Deserialize(src);
            return (item1, item2);
        }

        public void MeasureBytes(ByteMeasurer measurer, (T, U) a)
        {
            item1Traits.MeasureBytes(measurer, a.Item1);
            item2Traits.MeasureBytes(measurer, a.Item2);
        }

        public void AppendDebugString(DebugStringBuilder sb, (T, U) a)
        {
            sb.Builder.Append("(vtuple2 ");
            item1Traits.AppendDebugString(sb, a.Item1);
            sb.Builder.Append(", ");
            item2Traits.AppendDebugString(sb, a.Item2);
            sb.Builder.Append(')');
        }
    }

    public sealed class TupleTypeTraits<T, U, V> : ITypeTraits<Tuple<T, U, V>>
    {
        private readonly ITypeTraits<T> item1Traits;
        private readonly ITypeTraits<U> item2Traits;
        private readonly ITypeTraits<V> item3Traits;

        public TupleTypeTraits(ITypeTraits<T> item1Traits, ITypeTraits<U> item2Traits, ITypeTraits<V> item3Traits)
        {
            this.item1Traits = item1Traits;
            this.item2Traits = item2Traits;
            this.item3Traits = item3Traits;
        }

        public int Compare(Tuple<T, U, V> a, Tuple<T, U, V> b)
        {
            int r = item1Traits.Compare(a.Item1, b.Item1);
            if (r != 0) return r;
            r = item2Traits.Compare(a.Item2, b.Item2);
            if (r != 0) return r;
            return item3Traits.Compare(a.Item3, b.Item3);
        }

        public void AddToHash(HashBuilder b, Tuple<T, U, V> a)
        {
            b.Add(HashToken.Tuple3);
            item1Traits.AddToHash(b, a.Item1);
            item2Traits.AddToHash(b, a.Item2);
            item3Traits.AddToHash(b, a.Item3);
        }

        public bool CanSerialize(Tuple<T, U, V> a) =>
            item1Traits.CanSerialize(a.Item1) &&
            item2Traits.CanSerialize(a.Item2) &&
            item3Traits.CanSerialize(a.Item3);

        public void Serialize(Serializer dest, Tuple<T, U, V> item)
        {
            item1Traits.Serialize(dest, item.Item1);
            item2Traits.Serialize(dest, item.Item2);
            item3Traits.Serialize(dest, item.Item3);
        }

        public Tuple<T, U, V> Deserialize(Deserializer src)
        {
            T item1 = item1Traits.Deserialize(src);
            U item2 = item2Traits.Deserialize(src);
            V item3 = item3Traits.Deserialize(src);
            return new Tuple<T, U, V>(item1, item2, item3);
        }

        public void MeasureBytes(ByteMeasurer measurer, Tuple<T, U, V> a)
        {
            item1Traits.MeasureBytes(measurer, a.Item1);
            item2Traits.MeasureBytes(measurer, a.Item2);
            item3Traits.MeasureBytes(measurer, a.Item3);
        }

        public void AppendDebugString(DebugStringBuilder sb, Tuple<T, U, V> a)
        {
            sb.Builder.Append("(tuple3 ");
            item1Traits.AppendDebugString(sb, a.Item1);
            sb.Builder.Append(", ");
            item2Traits.AppendDebugString(sb, a.Item2);
            sb.Builder.Append(", ");
            item3Traits.AppendDebugString(sb, a.Item3);
            sb.Builder.Append(')');
        }
    }

    public sealed class ValueTupleTypeTraits<T, U, V> : ITypeTraits<(T, U, V)>
    {
        private readonly ITypeTraits<T> item1Traits;
        private readonly ITypeTraits<U> item2Traits;
        private readonly ITypeTraits<V> item3Traits;

        public ValueTupleTypeTraits(ITypeTraits<T> item1Traits, ITypeTraits<U> item2Traits, ITypeTraits<V> item3Traits)
        {
            this.item1Traits = item1Traits;
            this.item2Traits = item2Traits;
            this.item3Traits = item3Traits;
        }

        public int Compare((T, U, V) a, (T, U, V) b)
        {
            int r = item1Traits.Compare(a.Item1, b.Item1);
            if (r != 0) return r;
            r = item2Traits.Compare(a.Item2, b.Item2);
            if (r != 0) return r;
            return item3Traits.Compare(a.Item3, b.Item3);
        }

        public void AddToHash(HashBuilder b, (T, U, V) a)
        {
            b.Add(HashToken.Tuple3);
            item1Traits.AddToHash(b, a.Item1);
            item2Traits.AddToHash(b, a.Item2);
            item3Traits.AddToHash(b, a.Item3);
        }

        public bool CanSerialize((T, U, V) a) =>
            item1Traits.CanSerialize(a.Item1) &&
            item2Traits.CanSerialize(a.Item2) &&
            item3Traits.CanSerialize(a.Item3);

        public void Serialize(Serializer dest, (T, U, V) item)
        {
            item1Traits.Serialize(dest, item.Item1);
            item2Traits.Serialize(dest, item.Item2);
            item3Traits.Serialize(dest, item.Item3);
        }

        public (T, U, V) Deserialize(Deserializer src)
        {
            T item1 = item1Traits.Deserialize(src);
            U item2 = item2Traits.Deserialize(src);
            V item3 = item3Traits.Deserialize(src);
            return (item1, item2, item3);
        }

        public void MeasureBytes(ByteMeasurer measurer, (T, U, V) a)
        {
            item1Traits.MeasureBytes(measurer, a.Item1);
            item2Traits.MeasureBytes(measurer, a.Item2);
            item3Traits.MeasureBytes(measurer, a.Item3);
        }

        public void AppendDebugString(DebugStringBuilder sb, (T, U, V) a)
        {
            sb.Builder.Append("(vtuple2 ");
            item1Traits.AppendDebugString(sb, a.Item1);
            sb.Builder.Append(", ");
            item2Traits.AppendDebugString(sb, a.Item2);
            sb.Builder.Append(", ");
            item3Traits.AppendDebugString(sb, a.Item3);
            sb.Builder.Append(')');
        }
    }

    public sealed class OptionTypeTraits<T> : ITypeTraits<Option<T>>
    {
        private readonly ITypeTraits<T> itemTraits;

        public OptionTypeTraits(ITypeTraits<T> itemTraits)
        {
            this.itemTraits = itemTraits;
        }

        public int Compare(Option<T> a, Option<T> b)
        {
            if (!a.HasValue && !b.HasValue) return 0;
            if (!a.HasValue) return -1;
            if (!b.HasValue) return 1;
            return itemTraits.Compare(a.Value, b.Value);
        }

        public void AddToHash(HashBuilder b, Option<T> a)
        {
            if (a.HasValue)
            {
                b.Add((byte)1);
                itemTraits.AddToHash(b, a.Value);
            }
            else
            {
                b.Add((byte)0);
            }
        }

        public bool CanSerialize(Option<T> a) => !a.HasValue || itemTraits.CanSerialize(a.Value);

        public void Serialize(Serializer dest, Option<T> a)
        {
            if (a.HasValue)
            {
                dest.Writer.Write(true);
                itemTraits.Serialize(dest, a.Value);
            }
            else
            {
                dest.Writer.Write(false);
            }
        }

        public Option<T> Deserialize(Deserializer src)
        {
            bool hasValue = src.Reader.ReadBoolean();
            if (hasValue)
            {
                T value = itemTraits.Deserialize(src);
                return Option<T>.Some(value);
            }
            else
            {
                return Option<T>.None;
            }
        }

        public void MeasureBytes(ByteMeasurer measurer, Option<T> a)
        {
            measurer.AddBytes(1L);
            if (a.HasValue)
            {
                itemTraits.MeasureBytes(measurer, a.Value);
            }
        }

        public void AppendDebugString(DebugStringBuilder sb, Option<T> a)
        {
            if (a.HasValue)
            {
                sb.Builder.Append("(some ");
                itemTraits.AppendDebugString(sb, a.Value);
                sb.Builder.Append(')');
            }
            else
            {
                sb.Builder.Append("(none)");
            }
        }
    }

    public sealed class ConvertTypeTraits<T, U> : ITypeTraits<T>
    {
        private readonly Func<T, U> convert;
        private readonly ITypeTraits<U> itemTraits;
        private readonly Func<U, T> convertBack;

        public ConvertTypeTraits(Func<T, U> convert, ITypeTraits<U> itemTraits, Func<U, T> convertBack)
        {
            this.convert = convert;
            this.itemTraits = itemTraits;
            this.convertBack = convertBack;
        }

        public int Compare(T a, T b)
        {
            return itemTraits.Compare(convert(a), convert(b));
        }

        public void AddToHash(HashBuilder b, T a)
        {
            itemTraits.AddToHash(b, convert(a));
        }

        public bool CanSerialize(T a) => itemTraits.CanSerialize(convert(a));

        public void Serialize(Serializer dest, T a)
        {
            itemTraits.Serialize(dest, convert(a));
        }

        public T Deserialize(Deserializer src)
        {
            return convertBack(itemTraits.Deserialize(src));
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            itemTraits.MeasureBytes(measurer, convert(a));
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            itemTraits.AppendDebugString(sb, convert(a));
        }
    }

    public sealed class ConvertTypeTraitsDebugOverride<T, U> : ITypeTraits<T>
    {
        private readonly Func<T, U> convert;
        private readonly ITypeTraits<U> itemTraits;
        private readonly Func<U, T> convertBack;
        private readonly Action<DebugStringBuilder, T> appendDebugString;

        public ConvertTypeTraitsDebugOverride
        (
            Func<T, U> convert, ITypeTraits<U> itemTraits, Func<U, T> convertBack,
            Action<DebugStringBuilder, T> appendDebugString
        )
        {
            this.convert = convert;
            this.itemTraits = itemTraits;
            this.convertBack = convertBack;
            this.appendDebugString = appendDebugString;
        }

        public int Compare(T a, T b)
        {
            return itemTraits.Compare(convert(a), convert(b));
        }

        public void AddToHash(HashBuilder b, T a)
        {
            itemTraits.AddToHash(b, convert(a));
        }

        public bool CanSerialize(T a) => itemTraits.CanSerialize(convert(a));

        public void Serialize(Serializer dest, T a)
        {
            itemTraits.Serialize(dest, convert(a));
        }

        public T Deserialize(Deserializer src)
        {
            return convertBack(itemTraits.Deserialize(src));
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            itemTraits.MeasureBytes(measurer, convert(a));
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            appendDebugString(sb, a);
        }
    }

    public sealed class GuardException : Exception
    {
        public GuardException(string message) : base(message) { }
    }

    public sealed class GuardedTypeTraits<T> : ITypeTraits<T>
    {
        private readonly Func<T, bool> isOk;
        private readonly ITypeTraits<T> itemTraits;

        public GuardedTypeTraits(Func<T, bool> isOk, ITypeTraits<T> itemTraits)
        {
            this.isOk = isOk;
            this.itemTraits = itemTraits;
        }

        public int Compare(T a, T b)
        {
            if (isOk(a) && isOk(b))
            {
                return itemTraits.Compare(a, b);
            }
            else
            {
                throw new GuardException($"Guard failed for {typeof(T).FullName}");
            }
        }

        public void AddToHash(HashBuilder b, T a)
        {
            if (isOk(a))
            {
                itemTraits.AddToHash(b, a);
            }
            else
            {
                throw new GuardException($"Guard failed for {typeof(T).FullName}");
            }
        }

        public bool CanSerialize(T a) => isOk(a) && itemTraits.CanSerialize(a);

        public void Serialize(Serializer dest, T a)
        {
            if (isOk(a))
            {
                itemTraits.Serialize(dest, a);
            }
            else
            {
                throw new GuardException($"Guard failed for {typeof(T).FullName}");
            }
        }

        public T Deserialize(Deserializer src)
        {
            return itemTraits.Deserialize(src);
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            if (isOk(a))
            {
                itemTraits.MeasureBytes(measurer, a);
            }
            else
            {
                throw new GuardException($"Guard failed for {typeof(T).FullName}");
            }
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            if (isOk(a))
            {
                itemTraits.AppendDebugString(sb, a);
            }
            else
            {
                throw new GuardException($"Guard failed for {typeof(T).FullName}");
            }
        }
    }

    public sealed class RecursiveTypeTraits<T> : ITypeTraits<T>
    {
#if NETSTANDARD2_0
        private ITypeTraits<T> itemTraits;
#else
        private ITypeTraits<T>? itemTraits;
#endif

        public RecursiveTypeTraits()
        {
            itemTraits = null;
        }

        public void Set(ITypeTraits<T> itemTraits)
        {
            if (this.itemTraits == null)
            {
                this.itemTraits = itemTraits;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} already set");
            }
        }

        public int Compare(T a, T b)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                return itemTraits.Compare(a, b);
            }
        }

        public void AddToHash(HashBuilder b, T a)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                itemTraits.AddToHash(b, a);
            }
        }

        public bool CanSerialize(T a)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                return itemTraits.CanSerialize(a);
            }
        }

        public void Serialize(Serializer dest, T a)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                itemTraits.Serialize(dest, a);
            }
        }

        public T Deserialize(Deserializer src)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                return itemTraits.Deserialize(src);
            }
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                itemTraits.MeasureBytes(measurer, a);
            }
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            if (itemTraits == null)
            {
                throw new InvalidOperationException($"{nameof(RecursiveTypeTraits<T>)} not set");
            }
            else
            {
                itemTraits.AppendDebugString(sb, a);
            }
        }
    }

    public sealed class UnitTypeTraits<T> : ITypeTraits<T>
    {
        private readonly uint hashToken;
        private readonly T value;

        public UnitTypeTraits(uint hashToken, T value)
        {
            this.hashToken = hashToken;
            this.value = value;
        }

        public int Compare(T a, T b)
        {
            return 0;
        }

        public void AddToHash(HashBuilder b, T a)
        {
            b.Add(hashToken);
        }

        public bool CanSerialize(T a) => true;

        public void Serialize(Serializer dest, T a)
        {
            // do nothing
        }

        public T Deserialize(Deserializer src)
        {
            return value;
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            // do nothing
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            sb.Builder.Append("--");
        }
    }

    public interface IUnionCaseTypeTraits<TTag, T>
#if !NETSTANDARD2_0
        where TTag: notnull
#endif
    {
        bool CanConvert(T a);
        ITypeTraits<T> Traits { get; }
        TTag Name { get; }
    }

    public sealed class UnionCaseTypeTraits<TTag, T, U> : IUnionCaseTypeTraits<TTag, T>
#if !NETSTANDARD2_0
        where TTag : notnull
#endif
    {
        private readonly Func<T, bool> canConvert;

        public UnionCaseTypeTraits(TTag name, Func<T, bool> canConvert, Func<T, U> convert, ITypeTraits<U> itemTraits, Func<U, T> convertBack)
        {
            this.canConvert = canConvert;
            Traits = new ConvertTypeTraits<T, U>(convert, itemTraits, convertBack);
            Name = name;
        }

        public bool CanConvert(T a)
        {
            return canConvert(a);
        }

        public ITypeTraits<T> Traits { get; }

        public TTag Name { get; }
    }

    public sealed class UnionCaseTypeTraits2<TTag, T, U> : IUnionCaseTypeTraits<TTag, T> where U : T
#if !NETSTANDARD2_0
        where TTag : notnull
#endif
    {
        public UnionCaseTypeTraits2(TTag name, ITypeTraits<U> itemTraits)
        {
            Traits = new ConvertTypeTraits<T, U>
            (
                t =>
                {
                    if (t is U u)
                    {
                        return u;
                    }
                    else
                    {
                        throw new InvalidCastException($"{typeof(T).FullName} => {typeof(U).FullName} failed");
                    }
                },
                itemTraits,
                u => u
            );
            Name = name;
        }

        public bool CanConvert(T a)
        {
            return (a is U);
        }

        public ITypeTraits<T> Traits { get; }

        public TTag Name { get; }
    }

    public sealed class UnionTypeTraits<TTag, T> : ITypeTraits<T>
#if !NETSTANDARD2_0
        where TTag : notnull
#endif
    {
        private readonly ITypeTraits<TTag> tagTraits;
        private readonly Adapter<TTag> tagAdapter;
        private readonly ImmutableList<IUnionCaseTypeTraits<TTag, T>> cases;
        private readonly ImmutableSortedDictionary<TTag, int> caseIndexFromName;

        public UnionTypeTraits(ITypeTraits<TTag> tagTraits, ImmutableList<IUnionCaseTypeTraits<TTag, T>> cases)
        {
            this.tagTraits = tagTraits;
            tagAdapter = Adapter<TTag>.Create(tagTraits);
            this.cases = cases;
            ImmutableSortedDictionary<TTag, int>.Builder index = ImmutableSortedDictionary<TTag, int>.Empty.WithComparers(tagAdapter).ToBuilder();
            foreach(int i in Enumerable.Range(0, cases.Count))
            {
                if (index.ContainsKey(cases[i].Name))
                {
                    throw new InvalidOperationException($"Duplicate name {cases[i].Name}");
                }
                else
                {
                    index.Add(cases[i].Name, i);
                }
            }
            caseIndexFromName = index.ToImmutable();
        }

        private int GetCase(T a)
        {
            int i = 0;
            int iEnd = cases.Count;
            while (i < iEnd)
            {
                if (cases[i].CanConvert(a)) return i;
                ++i;
            }
            return -1;
        }

        public int Compare(T a, T b)
        {
            int ca = GetCase(a);
            int cb = GetCase(b);

            if (ca < 0 || cb < 0) throw new InvalidOperationException("Unrecognized case");

            if (ca < cb) return -1;
            if (ca > cb) return 1;

            return cases[ca].Traits.Compare(a, b);
        }

        public void AddToHash(HashBuilder b, T a)
        {
            int ca = GetCase(a);

            if (ca < 0) throw new InvalidOperationException("Unrecognized case");

            b.Add(HashToken.Union);
            b.Add(ca);
            cases[ca].Traits.AddToHash(b, a);
        }

        public bool CanSerialize(T a)
        {
            int ca = GetCase(a);
            return ca >= 0 && cases[ca].Traits.CanSerialize(a);
        }

        public void Serialize(Serializer dest, T a)
        {
            int ca = GetCase(a);
            if (ca < 0) throw new InvalidOperationException("Unrecognized case");

            tagTraits.Serialize(dest, cases[ca].Name);
            cases[ca].Traits.Serialize(dest, a);
        }

        public T Deserialize(Deserializer src)
        {
            TTag caseName = tagTraits.Deserialize(src);
            if (caseIndexFromName.TryGetValue(caseName, out int ca))
            {
                return cases[ca].Traits.Deserialize(src);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized case {tagTraits.ToDebugString(caseName)} in file");
            }
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            int ca = GetCase(a);
            if (ca < 0) throw new InvalidOperationException("Unrecognized case");

            tagTraits.MeasureBytes(measurer, cases[ca].Name);
            cases[ca].Traits.MeasureBytes(measurer, a);
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            int ca = GetCase(a);
            if (ca < 0) throw new InvalidOperationException("Unrecognized case");

            sb.Builder.Append("(union-case ");
            tagTraits.AppendDebugString(sb, cases[ca].Name);
            sb.Builder.Append(", ");
            cases[ca].Traits.AppendDebugString(sb, a);
            sb.Builder.Append(')');
        }
    }

    public sealed class ListTypeTraits<T> : ITypeTraits<ImmutableList<T>>
    {
        private readonly ITypeTraits<T> itemTraits;

        public ListTypeTraits(ITypeTraits<T> itemTraits)
        {
            this.itemTraits = itemTraits;
        }

        public int Compare(ImmutableList<T> a, ImmutableList<T> b)
        {
            int i = 0;
            while (true)
            {
                if (i == a.Count && i == b.Count) return 0;
                if (i == a.Count) return -1;
                if (i == b.Count) return 1;

                int r = itemTraits.Compare(a[i], b[i]);
                if (r != 0) return r;

                ++i;
            }
        }

        public void AddToHash(HashBuilder b, ImmutableList<T> a)
        {
            b.Add(HashToken.List);
            b.Add(a.Count);
            foreach (T item in a)
            {
                itemTraits.AddToHash(b, item);
            }
        }

        public bool CanSerialize(ImmutableList<T> a) => a.All(i => itemTraits.CanSerialize(i));

        public void Serialize(Serializer dest, ImmutableList<T> a)
        {
            dest.Writer.Write(a.Count);
            foreach(T item in a)
            {
                itemTraits.Serialize(dest, item);
            }
        }

        public ImmutableList<T> Deserialize(Deserializer src)
        {
            int count = src.Reader.ReadInt32();
            ImmutableList<T>.Builder result = ImmutableList<T>.Empty.ToBuilder();
            for (int i = 0; i < count; ++i)
            {
                result.Add(itemTraits.Deserialize(src));
            }
            return result.ToImmutable();
        }

        public void MeasureBytes(ByteMeasurer measurer, ImmutableList<T> a)
        {
            measurer.AddBytes(4L);
            a.ForEach(i => itemTraits.MeasureBytes(measurer, i));
        }

        public void AppendDebugString(DebugStringBuilder sb, ImmutableList<T> a)
        {
            if (!a.IsEmpty)
            {
                sb.Builder.Append("(list ");
                bool needDelim = false;
                foreach (T item in a)
                {
                    if (needDelim) sb.Builder.Append(", ");
                    itemTraits.AppendDebugString(sb, item);
                    needDelim = true;
                }
                sb.Builder.Append(')');
            }
            else
            {
                sb.Builder.Append("(list)");
            }
        }
    }

    public sealed class SetTypeTraits<T> : ITypeTraits<ImmutableSortedSet<T>>
    {
        private readonly ImmutableSortedSet<T> empty;
        private readonly ITypeTraits<T> itemTraits;

        public SetTypeTraits(ImmutableSortedSet<T> empty, ITypeTraits<T> itemTraits)
        {
            this.empty = empty;
            this.itemTraits = itemTraits;
        }

        public SetTypeTraits(ITypeTraits<T> itemTraits)
            : this
            (
                ImmutableSortedSet<T>.Empty.WithComparer(Adapter<T>.Create(itemTraits)),
                itemTraits
            )
        {
            // empty
        }

        public int Compare(ImmutableSortedSet<T> a, ImmutableSortedSet<T> b)
        {
            int i = 0;
            while (true)
            {
                if (i == a.Count && i == b.Count) return 0;
                if (i == a.Count) return -1;
                if (i == b.Count) return 1;

                int r = itemTraits.Compare(a[i], b[i]);
                if (r != 0) return r;

                ++i;
            }
        }

        public void AddToHash(HashBuilder b, ImmutableSortedSet<T> a)
        {
            b.Add(HashToken.Set);
            b.Add(a.Count);
            foreach (T item in a)
            {
                itemTraits.AddToHash(b, item);
            }
        }

        public bool CanSerialize(ImmutableSortedSet<T> a) => a.All(i => itemTraits.CanSerialize(i));

        public void Serialize(Serializer dest, ImmutableSortedSet<T> a)
        {
            dest.Writer.Write(a.Count);
            foreach(T item in a)
            {
                itemTraits.Serialize(dest, item);
            }
        }

        public ImmutableSortedSet<T> Deserialize(Deserializer src)
        {
            int count = src.Reader.ReadInt32();
            ImmutableSortedSet<T>.Builder result = empty.ToBuilder();
            for (int i = 0; i < count; ++i)
            {
                result.Add(itemTraits.Deserialize(src));
            }
            return result.ToImmutable();
        }

        public void MeasureBytes(ByteMeasurer measurer, ImmutableSortedSet<T> a)
        {
            measurer.AddBytes(4L);
            a.ForEach(i => itemTraits.MeasureBytes(measurer, i));
        }

        public void AppendDebugString(DebugStringBuilder sb, ImmutableSortedSet<T> a)
        {
            if (!a.IsEmpty)
            {
                sb.Builder.Append("(set ");
                bool needDelim = false;
                foreach (T item in a)
                {
                    if (needDelim) sb.Builder.Append(", ");
                    itemTraits.AppendDebugString(sb, item);
                    needDelim = true;
                }
                sb.Builder.Append(')');
            }
            else
            {
                sb.Builder.Append("(set)");
            }
        }
    }

    public sealed class DictionaryTypeTraits<K, V> : ITypeTraits<ImmutableSortedDictionary<K, V>>
#if !NETSTANDARD2_0
        where K : notnull
#endif
    {
        private readonly ImmutableSortedSet<K> emptySet;
        private readonly ImmutableSortedDictionary<K, V> emptyDict;
        private readonly ITypeTraits<K> keyTraits;
        private readonly ITypeTraits<V> valueTraits;

        public DictionaryTypeTraits
        (
            ImmutableSortedSet<K> emptySet,
            ImmutableSortedDictionary<K, V> emptyDict,
            ITypeTraits<K> keyTraits,
            ITypeTraits<V> valueTraits
        )
        {
            this.emptySet = emptySet;
            this.emptyDict = emptyDict;
            this.keyTraits = keyTraits;
            this.valueTraits = valueTraits;
        }

        public DictionaryTypeTraits
        (
            ITypeTraits<K> keyTraits,
            ITypeTraits<V> valueTraits
        )
            : this
            (
                ImmutableSortedSet<K>.Empty.WithComparer(Adapter<K>.Create(keyTraits)),
                ImmutableSortedDictionary<K, V>.Empty.WithComparers(Adapter<K>.Create(keyTraits), Adapter<V>.Create(valueTraits)),
                keyTraits,
                valueTraits
            )
        {
            // empty
        }

        public int Compare(ImmutableSortedDictionary<K, V> a, ImmutableSortedDictionary<K, V> b)
        {
            ImmutableSortedSet<K> keys = emptySet
                .Union(a.Keys)
                .Union(b.Keys);

            foreach (K key in keys)
            {
                if (!a.ContainsKey(key)) return 1;
                if (!b.ContainsKey(key)) return -1;

                int r = valueTraits.Compare(a[key], b[key]);
                if (r != 0) return r;
            }

            return 0;
        }

        public void AddToHash(HashBuilder b, ImmutableSortedDictionary<K, V> a)
        {
            b.Add(HashToken.Dictionary);
            b.Add(a.Count);
            foreach (KeyValuePair<K, V> kvp in a)
            {
                keyTraits.AddToHash(b, kvp.Key);
                valueTraits.AddToHash(b, kvp.Value);
            }
        }

        public bool CanSerialize(ImmutableSortedDictionary<K, V> a) => a.All(kvp => keyTraits.CanSerialize(kvp.Key) && valueTraits.CanSerialize(kvp.Value));

        public void Serialize(Serializer dest, ImmutableSortedDictionary<K, V> a)
        {
            dest.Writer.Write(a.Count);
            foreach(KeyValuePair<K, V> kvp in a)
            {
                keyTraits.Serialize(dest, kvp.Key);
                valueTraits.Serialize(dest, kvp.Value);
            }
        }

        public ImmutableSortedDictionary<K, V> Deserialize(Deserializer src)
        {
            int count = src.Reader.ReadInt32();
            ImmutableSortedDictionary<K, V>.Builder result = emptyDict.ToBuilder();
            for (int i = 0; i < count; ++i)
            {
                K k = keyTraits.Deserialize(src);
                V v = valueTraits.Deserialize(src);
                result.Add(k, v);
            }
            return result.ToImmutable();
        }

        public void MeasureBytes(ByteMeasurer measurer, ImmutableSortedDictionary<K, V> a)
        {
            measurer.AddBytes(4L);
            a.ForEach((k, v) => { keyTraits.MeasureBytes(measurer, k); valueTraits.MeasureBytes(measurer, v); });
        }

        public void AppendDebugString(DebugStringBuilder sb, ImmutableSortedDictionary<K, V> a)
        {
            if (!a.IsEmpty)
            {
                sb.Builder.Append("(dictionary ");
                bool needDelim = false;
                foreach (KeyValuePair<K, V> item in a)
                {
                    if (needDelim) sb.Builder.Append(", ");
                    sb.Builder.Append('(');
                    keyTraits.AppendDebugString(sb, item.Key);
                    sb.Builder.Append(": ");
                    valueTraits.AppendDebugString(sb, item.Value);
                    sb.Builder.Append(')');
                    needDelim = true;
                }
                sb.Builder.Append(')');
            }
            else
            {
                sb.Builder.Append("(dictionary)");
            }
        }
    }

    public sealed class MutableBoxTypeTraits<T, K, V> : ITypeTraits<T>
#if !NETSTANDARD2_0
        where K : notnull
#endif
    {
        private readonly SerializerStateID ssid;
        private readonly Func<T, K> getBoxKey;
        private readonly Func<T, V> getBoxValue;
        private readonly Func<T> createBox;
        private readonly Action<T, V> setBoxValue;
        private readonly ITypeTraits<K> boxKeyTraits;
        private readonly ITypeTraits<V> boxValueTraits;

        public MutableBoxTypeTraits
        (
            Func<T, K> getBoxKey,
            Func<T, V> getBoxValue,
            Func<T> createBox,
            Action<T, V> setBoxValue,
            ITypeTraits<K> boxKeyTraits,
            ITypeTraits<V> boxValueTraits
        )
        {
            ssid = SerializerStateID.Next();
            this.getBoxKey = getBoxKey;
            this.getBoxValue = getBoxValue;
            this.createBox = createBox;
            this.setBoxValue = setBoxValue;
            this.boxKeyTraits = boxKeyTraits;
            this.boxValueTraits = boxValueTraits;
        }

        public int Compare(T a, T b)
        {
            // only the box key is used for comparison
            return boxKeyTraits.Compare(getBoxKey(a), getBoxKey(b));
        }

        public void AddToHash(HashBuilder b, T a)
        {
            // only the box key is used for hashing
            boxKeyTraits.AddToHash(b, getBoxKey(a));
        }

        public bool CanSerialize(T a)
        {
            System.Diagnostics.Debug.Assert(boxKeyTraits.CanSerialize(getBoxKey(a)));

            return boxValueTraits.CanSerialize(getBoxValue(a));
        }

        private sealed class SerializationState
        {
            public SerializationState()
            {
                ScheduledKeys = ImmutableSortedDictionary<K, int>.Empty;
                NextID = 0;
            }

            public ImmutableSortedDictionary<K, int> ScheduledKeys { get; set; }

            public int NextID { get; set; }
        }

        public void Serialize(Serializer dest, T a)
        {
            SerializationState ss = dest.GetSerializerState(ssid, () => new SerializationState());
            K boxKey = getBoxKey(a);
            int finalId;
            if (ss.ScheduledKeys.TryGetValue(boxKey, out int id))
            {
                finalId = id;
            }
            else
            {
                finalId = ss.NextID;
                ss.NextID++;
                ss.ScheduledKeys = ss.ScheduledKeys.Add(boxKey, finalId);
                V value = getBoxValue(a);
                dest.Enqueue(() => { boxValueTraits.Serialize(dest, value); });
            }
            dest.Writer.Write(finalId);
        }

        private sealed class DeserializationState
        {
            public DeserializationState()
            {
                EncounteredKeys = ImmutableSortedDictionary<int, T>.Empty;
            }

            public ImmutableSortedDictionary<int, T> EncounteredKeys { get; set; }
        }

        public T Deserialize(Deserializer src)
        {
            DeserializationState ss = src.GetSerializerState(ssid, () => new DeserializationState());
            int id = src.Reader.ReadInt32();
#if NETSTANDARD2_0
            if (ss.EncounteredKeys.TryGetValue(id, out T box))
#else
            if (ss.EncounteredKeys.TryGetValue(id, out T? box))
#endif
            {
                return box;
            }
            else
            {
                T newBox = createBox();
                ss.EncounteredKeys = ss.EncounteredKeys.Add(id, newBox);
                src.Enqueue
                (
                    () =>
                    {
                        V value = boxValueTraits.Deserialize(src);
                        setBoxValue(newBox, value);
                    }
                );
                return newBox;
            }
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            SerializationState ss = measurer.GetSerializerState(ssid, () => new SerializationState());
            K boxKey = getBoxKey(a);
            int finalId;
            if (ss.ScheduledKeys.TryGetValue(boxKey, out int id))
            {
                finalId = id;
            }
            else
            {
                finalId = ss.NextID;
                ss.NextID++;
                ss.ScheduledKeys = ss.ScheduledKeys.Add(boxKey, finalId);
                measurer.Enqueue(() => { boxValueTraits.MeasureBytes(measurer, getBoxValue(a)); });
            }
            measurer.AddBytes(4L);
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            SerializationState ss = sb.GetSerializerState(ssid, () => new SerializationState());
            K boxKey = getBoxKey(a);
            int finalId;
            if (ss.ScheduledKeys.TryGetValue(boxKey, out int id))
            {
                finalId = id;

            }
            else
            {
                finalId = ss.NextID;
                ss.NextID++;
                ss.ScheduledKeys = ss.ScheduledKeys.Add(boxKey, finalId);
                V value = getBoxValue(a);
                sb.Enqueue
                (
                    () =>
                    {
                        sb.Builder.Append(", (box #");
                        sb.Builder.Append(finalId);
                        sb.Builder.Append(" = ");
                        boxValueTraits.AppendDebugString(sb, value);
                        sb.Builder.Append(')');
                    }
                );
            }

            sb.Builder.Append("(box #");
            sb.Builder.Append(finalId);
            sb.Builder.Append(')');
        }

        [Obsolete("Moved to TypeTraitsUtility")]
        public static MutableBoxTypeTraits<StrongBox<T>, long, T> GetStrongBoxTraits(ITypeTraits<T> valueTraits)
        {
            return TypeTraitsUtility.GetStrongBoxTraits(valueTraits);
        }
    }

    public abstract class AbstractFieldTypeTraits<TRecord, TBuilder>
    {
        public abstract string Name { get; }
#if NETSTANDARD2_0
        public abstract object GetFieldInBuilder(TBuilder builder);
        public abstract void SetFieldInBuilder(TBuilder builder, object value);
        public abstract object GetFieldInRecord(TRecord record);
        public abstract ITypeTraits<object> TypeTraits { get; }
#else
        public abstract object? GetFieldInBuilder(TBuilder builder);
        public abstract void SetFieldInBuilder(TBuilder builder, object? value);
        public abstract object? GetFieldInRecord(TRecord record);
        public abstract ITypeTraits<object?> TypeTraits { get; }
#endif
    }

    public sealed class FieldTypeTraits<TRecord, TBuilder, T> : AbstractFieldTypeTraits<TRecord, TBuilder>
    {
        private readonly string name;
        private readonly Func<TBuilder, T> getFieldInBuilder;
        private readonly Action<TBuilder, T> setFieldInBuilder;
        private readonly Func<TRecord, T> getFieldInRecord;
        //private readonly ITypeTraits<T> fieldTypeTraits;

#if NETSTANDARD2_0
        private readonly ITypeTraits<object> fieldAsObjectTypeTraits;
#else
        private readonly ITypeTraits<object?> fieldAsObjectTypeTraits;
#endif

        public FieldTypeTraits
        (
            string name,
            Func<TBuilder, T> getFieldInBuilder,
            Action<TBuilder, T> setFieldInBuilder,
            Func<TRecord, T> getFieldInRecord,
            ITypeTraits<T> fieldTypeTraits
        )
        {
            this.name = name;
            this.getFieldInBuilder = getFieldInBuilder;
            this.setFieldInBuilder = setFieldInBuilder;
            this.getFieldInRecord = getFieldInRecord;
            //this.fieldTypeTraits = fieldTypeTraits;

#if NETSTANDARD2_0
            fieldAsObjectTypeTraits = new ConvertTypeTraits<object, T>
#else
            fieldAsObjectTypeTraits = new ConvertTypeTraits<object?, T>
#endif
            (
#pragma warning disable CS8600, CS8603
                v => (T)v,
#pragma warning restore CS8600, CS8603
                fieldTypeTraits,
                v => v
            );
        }

        public override string Name => name;

#if NETSTANDARD2_0
        public override ITypeTraits<object> TypeTraits => fieldAsObjectTypeTraits;
#else
        public override ITypeTraits<object?> TypeTraits => fieldAsObjectTypeTraits;
#endif

#if NETSTANDARD2_0
        public override object GetFieldInBuilder(TBuilder builder)
#else
        public override object? GetFieldInBuilder(TBuilder builder)
#endif
        {
            return getFieldInBuilder(builder);
        }

#if NETSTANDARD2_0
        public override void SetFieldInBuilder(TBuilder builder, object value)
#else
        public override void SetFieldInBuilder(TBuilder builder, object? value)
#endif
        {
#pragma warning disable CS8600, CS8604
            setFieldInBuilder(builder, (T)value);
#pragma warning restore CS8600, CS8604
        }

#if NETSTANDARD2_0
        public override object GetFieldInRecord(TRecord record)
#else
        public override object? GetFieldInRecord(TRecord record)
#endif
        {
            return getFieldInRecord(record);
        }
    }

    public sealed class RecordTypeTraits<TRecord, TBuilder> : ITypeTraits<TRecord>
    {
        private readonly ImmutableList<AbstractFieldTypeTraits<TRecord, TBuilder>> fields;
        private readonly Func<TBuilder> makeBuilder;
        private readonly Func<TBuilder, TRecord> makeRecordFromBuilder;

        public RecordTypeTraits
        (
            ImmutableList<AbstractFieldTypeTraits<TRecord, TBuilder>> fields,
            Func<TBuilder> makeBuilder,
            Func<TBuilder, TRecord> makeRecordFromBuilder
        )
        {
            this.fields = fields;
            this.makeBuilder = makeBuilder;
            this.makeRecordFromBuilder = makeRecordFromBuilder;
        }

        public int Compare(TRecord a, TRecord b)
        {
            foreach(AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                int i = field.TypeTraits.Compare(field.GetFieldInRecord(a), field.GetFieldInRecord(b));
                if (i != 0) return i;
            }

            return 0;
        }

        public void AddToHash(HashBuilder b, TRecord a)
        {
            foreach(AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                field.TypeTraits.AddToHash(b, field.GetFieldInRecord(a));
            }
        }

        public bool CanSerialize(TRecord a)
        {
            foreach (AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                if (!field.TypeTraits.CanSerialize(field.GetFieldInRecord(a))) return false;
            }

            return true;
        }

        public void Serialize(Serializer dest, TRecord a)
        {
            foreach (AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                field.TypeTraits.Serialize(dest, field.GetFieldInRecord(a));
            }
        }

        public TRecord Deserialize(Deserializer src)
        {
            TBuilder builder = makeBuilder();
            foreach (AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                field.SetFieldInBuilder(builder, field.TypeTraits.Deserialize(src));
            }
            return makeRecordFromBuilder(builder);
        }

        public void MeasureBytes(ByteMeasurer measurer, TRecord a)
        {
            foreach (AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                field.TypeTraits.MeasureBytes(measurer, field.GetFieldInRecord(a));
            }
        }

        public void AppendDebugString(DebugStringBuilder sb, TRecord a)
        {
            sb.Builder.Append("(rec");
            foreach (AbstractFieldTypeTraits<TRecord, TBuilder> field in fields)
            {
                sb.Builder.Append(", ");
                sb.Builder.Append(TypeTraitsUtility.SymbolToString(field.Name));
                sb.Builder.Append(" = ");
                field.TypeTraits.AppendDebugString(sb, field.GetFieldInRecord(a));
            }
            sb.Builder.Append(')');
        }
    }

    public static partial class Extensions
    {
#if NETSTANDARD2_0
        public static int GetByteCount(this BigInteger i)
        {
            BigInteger j = (i < 0) ? ~i : i;

            int valueToAdd = 1;
            while ((j >> (valueToAdd << 3)) > 0)
            {
                valueToAdd <<= 1;
            }

            int sum = 0;
            BigInteger j2 = j;
            while (valueToAdd > 0)
            {
                BigInteger j3 = (j2 >> (valueToAdd << 3));
                if (j3 > 0)
                {
                    sum += valueToAdd;
                    j2 = j3;
                }
                valueToAdd >>= 1;
            }

            if (j >> (sum << 3) > 127) ++sum;

            return sum + 1;
        }
#endif

        public static void ForEach<T>(this ImmutableSortedSet<T> collection, Action<T> action)
        {
            foreach (T item in collection) action(item);
        }

        public static void ForEach<K, V>(this ImmutableSortedDictionary<K, V> collection, Action<K, V> action)
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            foreach (KeyValuePair<K, V> kvp in collection) action(kvp.Key, kvp.Value);
        }

        public static void AppendHex(this StringBuilder sb, byte[] b)
        {
            int iEnd = b.Length;
            bool needDelim = false;
            for(int i = 0; i < iEnd; i += 8)
            {
                for(int j = 0; j < 8; ++j)
                {
                    if (i+j < iEnd)
                    {
                        if (needDelim) sb.Append('-');
                        sb.AppendFormat("{0:X2}", b[i + j]);
                        needDelim = true;
                    }
                }
            }
        }
    }
}
