using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Sunlighter.TypeTraitsLib
{
    public readonly struct SerializerStateID : IEquatable<SerializerStateID>, IComparable<SerializerStateID>
    {
        private readonly ulong value;

        public SerializerStateID(ulong id) { this.value = id; }

#if NETSTANDARD2_0
        public ulong Value => value;
#else
        public readonly ulong Value => value;
#endif

        private static readonly object syncRoot = new object();
        private static ulong nextValue = 0UL;

        public static SerializerStateID Next()
        {
            lock (syncRoot)
            {
                ulong value = nextValue;
                ++nextValue;
                return new SerializerStateID(value);
            }
        }

#if NETSTANDARD2_0
        public override bool Equals(object obj)
#else
        public override readonly bool Equals([NotNullWhen(true)] object? obj)
#endif
        {
            return obj is SerializerStateID ss && ss.Value == value;
        }

#if NETSTANDARD2_0
        public override int GetHashCode()
#else
        public override readonly int GetHashCode()
#endif
        {
            return value.ToString().GetHashCode();
        }

#if NETSTANDARD2_0
        public override string ToString()
#else
        public override readonly string ToString()
#endif
        {
            return $"(ssid {value})";
        }

#if NETSTANDARD2_0
        public bool Equals(SerializerStateID other) => value == other.value;
#else
        public readonly bool Equals(SerializerStateID other) => value == other.value;
#endif

#if NETSTANDARD2_0
        public int CompareTo(SerializerStateID other)
#else
        public readonly int CompareTo(SerializerStateID other)
#endif
        {
            if (value < other.value) return -1;
            if (value > other.value) return 1;
            return 0;
        }

        public static bool operator ==(SerializerStateID a, SerializerStateID b) => a.Value == b.Value;
        public static bool operator !=(SerializerStateID a, SerializerStateID b) => a.Value != b.Value;
        public static bool operator <(SerializerStateID a, SerializerStateID b) => a.Value < b.Value;
        public static bool operator >(SerializerStateID a, SerializerStateID b) => a.Value > b.Value;
        public static bool operator <=(SerializerStateID a, SerializerStateID b) => a.Value <= b.Value;
        public static bool operator >=(SerializerStateID a, SerializerStateID b) => a.Value >= b.Value;
    }

    public abstract class SerializerStateManager
    {
        private ImmutableSortedDictionary<SerializerStateID, object> serializerStates;
        protected ImmutableList<Action> queue;

        protected SerializerStateManager()
        {
            serializerStates = ImmutableSortedDictionary<SerializerStateID, object>.Empty;
            queue = ImmutableList<Action>.Empty;
        }

        public T GetSerializerState<T>(SerializerStateID ssid, Func<T> create)
#if !NETSTANDARD2_0
            where T : notnull
#endif
        {

#if NETSTANDARD2_0
            if (serializerStates.TryGetValue(ssid, out object oVal))
#else
            if (serializerStates.TryGetValue(ssid, out object? oVal))
#endif
            {
                if (oVal is T tVal)
                {
                    return tVal;
                }
                else
                {
                    throw new InvalidOperationException($"For {ssid}, expected {TypeTraitsUtility.GetTypeName(typeof(T))}, got {TypeTraitsUtility.GetTypeName(oVal.GetType())}");
                }
            }
            else
            {
                T tVal2 = create();
                serializerStates = serializerStates.Add(ssid, tVal2);
                return tVal2;
            }
        }

        public void Enqueue(Action a)
        {
            queue = queue.Add(a);
        }

        public virtual void RunQueue()
        {
            while (!queue.IsEmpty)
            {
                Action a = queue[0];
                queue = queue.RemoveAt(0);
                a();
            }
        }
    }

    public sealed class Serializer : SerializerStateManager
    {
        public Serializer(BinaryWriter writer)
        {
            Writer = writer;
        }

        public BinaryWriter Writer { get; }
    }

    public sealed class Deserializer : SerializerStateManager
    {
        public Deserializer(BinaryReader reader)
        {
            Reader = reader;
        }

        public BinaryReader Reader { get; }
    }

    public sealed class ByteMeasurer : SerializerStateManager
    {
        private long count;

        public ByteMeasurer()
        {
            count = 0L;
        }

        public void AddBytes(long count)
        {
            this.count += count;
        }

        public long Count => count;
    }

    public sealed class DebugStringBuilder : SerializerStateManager
    {
        private readonly StringBuilder sb;

        public DebugStringBuilder()
        {
            sb = new StringBuilder();
        }

        public StringBuilder Builder => sb;
    }

    public sealed class AnalogyTracker : SerializerStateManager
    {
        private bool isAnalogous;

        public AnalogyTracker()
        {
            isAnalogous = true;
        }

        public bool IsAnalogous => isAnalogous;

        public void SetNotAnalogous()
        {
            isAnalogous = false;
        }

        public void RunCheckIfAppropriate(Func<bool> check)
        {
            if (isAnalogous)
            {
                isAnalogous = check();
            }
        }

        public override void RunQueue()
        {
            while (!queue.IsEmpty && isAnalogous)
            {
                Action a = queue[0];
                queue = queue.RemoveAt(0);
                a();
            }

            queue = queue.Clear();
        }
    }
}
