using System;

namespace Sunlighter.TypeTraitsLib.Building
{
    public sealed class DelegateTypeTraits<T> : ITypeTraits<T>
    {
        private readonly Func<T, T, int> compareFunc;
        private readonly Action<HashBuilder, T> addToHashFunc;
        private readonly Action<AnalogyTracker, T, T> checkAnalogousFunc;
        private readonly Action<SerializabilityTracker, T> checkSerializabilityFunc;
        private readonly Action<Serializer, T> serializeFunc;
        private readonly Func<Deserializer, T> deserializeFunc;
        private readonly Action<ByteMeasurer, T> measureBytesFunc;
        private readonly Func<CloneTracker, T, T> cloneFunc;
        private readonly Action<DebugStringBuilder, T> appendDebugStringFunc;

        public DelegateTypeTraits
        (
            Func<T, T, int> compareFunc,
            Action<HashBuilder, T> addToHashFunc,
            Action<AnalogyTracker, T, T> checkAnalogousFunc,
            Action<SerializabilityTracker, T> checkSerializabilityFunc,
            Action<Serializer, T> serializeFunc,
            Func<Deserializer, T> deserializeFunc,
            Action<ByteMeasurer, T> measureBytesFunc,
            Func<CloneTracker, T, T> cloneFunc,
            Action<DebugStringBuilder, T> appendDebugStringFunc
        )
        {
            this.compareFunc = compareFunc;
            this.addToHashFunc = addToHashFunc;
            this.checkAnalogousFunc = checkAnalogousFunc;
            this.checkSerializabilityFunc = checkSerializabilityFunc;
            this.serializeFunc = serializeFunc;
            this.deserializeFunc = deserializeFunc;
            this.measureBytesFunc = measureBytesFunc;
            this.cloneFunc = cloneFunc;
            this.appendDebugStringFunc = appendDebugStringFunc;
        }

        public int Compare(T a, T b)
        {
            return compareFunc(a, b);
        }

        public void AddToHash(HashBuilder b, T a)
        {
            addToHashFunc(b, a);
        }

        public void CheckAnalogous(AnalogyTracker tracker, T a, T b)
        {
            checkAnalogousFunc(tracker, a, b);
        }

        public void CheckSerializability(SerializabilityTracker tracker, T a)
        {
            checkSerializabilityFunc(tracker, a);
        }

        public void Serialize(Serializer dest, T a)
        {
            serializeFunc(dest, a);
        }

        public T Deserialize(Deserializer src)
        {
            return deserializeFunc(src);
        }

        public void MeasureBytes(ByteMeasurer measurer, T a)
        {
            measureBytesFunc(measurer, a);
        }

        public T Clone(CloneTracker tracker, T a)
        {
            return cloneFunc(tracker, a);
        }

        public void AppendDebugString(DebugStringBuilder sb, T a)
        {
            appendDebugStringFunc(sb, a);
        }
    }
}
