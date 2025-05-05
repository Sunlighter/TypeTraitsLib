using System;
using System.IO;
using System.Security.Cryptography;

namespace Sunlighter.TypeTraitsLib
{
    public static class HashToken
    {
        public const uint Char = 0x1EE444BD;
        public const uint String = 0x1BA6E283;
        public const uint Byte = 0x9120ED73;
        public const uint SByte = 0x498EEF07;
        public const uint ByteArray = 0x4FEE3128;
        public const uint Int16 = 0x2E4DA6AE;
        public const uint UInt16 = 0xE1510A30;
        public const uint Int32 = 0x1D9EBF18;
        public const uint UInt32 = 0xE1C2F425;
        public const uint Int64 = 0x4789610C;
        public const uint UInt64 = 0x7EF0D322;
        public const uint BigInt = 0x3C7E3B80;
        public const uint Boolean = 0xB2980985;
        public const uint Tuple2 = 0x92882C6C;
        public const uint Tuple3 = 0xCCA88465;
        public const uint Union = 0x79EFC1E9;
        public const uint None = 0x6B48B4B6;
        public const uint List = 0x28BD378B;
        public const uint Set = 0xFB769F51;
        public const uint Dictionary = 0xA1AF0337;

        public const uint JsonNull = 0x2980BCD9;
    }

    public abstract class HashBuilder
    {
        public abstract void Add(byte b);
        public abstract void Add(byte[] b);

        public virtual void Add(int i)
        {
            Add(BitConverter.GetBytes(i));
        }

        public virtual void Add(uint i)
        {
            Add(BitConverter.GetBytes(i));
        }
    }

#if false
    public class Crc32HashBuilder : HashBuilder
    {
        private readonly Crc32 crc32;

        public Crc32HashBuilder()
        {
            crc32 = new Crc32();
        }

        public override void Add(byte b)
        {
            crc32.Update(b);
        }

        public override void Add(byte[] b)
        {
            crc32.Update(b);
        }

        public int Result => unchecked((int)crc32.Value);
    }
#endif

    public class BasicHashBuilder : HashBuilder
    {
        private readonly Random random;
        private readonly int[] array;
        private int currentValue;

        public BasicHashBuilder()
        {
            random = new Random(0x48BC74DB);
            currentValue = random.NextInt32();
            array = new int[256];
            for(int i = 0; i < 256; ++i)
            {
                array[i] = random.NextInt32();
            }
        }

        private static int Mutate(int x) => (int)((((uint)x & 0x7FFFFFF8u) >> 3) | (((uint)x & 7u) << 29));

        public override void Add(byte b)
        {
            currentValue ^= array[b];
            currentValue = Mutate(currentValue);
            array[b] = random.NextInt32();
        }

        public override void Add(byte[] b)
        {
            foreach(byte b1 in b)
            {
                currentValue ^= array[b1];
                currentValue = Mutate(currentValue);
                array[b1] = random.NextInt32();
            }
        }

        public int Result => currentValue;
    }

    public static partial class Extensions
    {
        public static int NextInt32(this Random r)
        {
            byte[] b = new byte[4];
            r.NextBytes(b);
            return BitConverter.ToInt32(b, 0);
        }
    }

    public sealed class SHA256HashBuilder : HashBuilder, IDisposable
    {
        private readonly MemoryStream ms;

        public SHA256HashBuilder()
        {
            ms = new MemoryStream();
        }

        public override void Add(byte b)
        {
            ms.WriteByte(b);
        }

        public override void Add(byte[] b)
        {
            ms.Write(b, 0, b.Length);
        }

        public byte[] Result
        {
            get
            {
                using (SHA256 worker = SHA256.Create())
                {
                    return worker.ComputeHash(ms.ToArray());
                }
            }
        }

        public void Dispose()
        {
            ms.Dispose();
        }
    }
}
