using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace TypeTraitsTest
{
    [TestClass]
    public sealed class BuilderTests
    {
        [TestMethod]
        public void TestBuilder()
        {
            ITypeTraits<Thingy> typeTraits = Builder.Instance.GetTypeTraits<Thingy>();
            Thingy t1 = new ListThingy
            (
                [
                    new StringThingy("abc"),
                    new BigIntThingy(100),
                    NullThingy.Value,
                    new ListThingy([new StringThingy("def")]),
                ]
            );
            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);
            Thingy t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));
            Assert.AreEqual(0, typeTraits.Compare(t1, t2));
        }

        [TestMethod]
        public void TestBuilderWithBox()
        {
            ITypeTraits<Thingy> typeTraits = Builder.Instance.GetTypeTraits<Thingy>();
            StrongBox<Thingy> box = new StrongBox<Thingy>(NullThingy.Value);
            Thingy t1 = new ListThingy
            (
                [
                    new StringThingy("abc"),
                    new BigIntThingy(100),
                    NullThingy.Value,
                    new ListThingy([new StringThingy("def")]),
                    new BoxThingy(box),
                    new Gensym()
                ]
            );
            box.Value = t1;

            Assert.IsTrue(typeTraits.CanSerialize(t1));

            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);

            // check measuring bytes
            Assert.AreEqual(serializedBytes.LongLength, typeTraits.MeasureAllBytes(t1));

            Thingy t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));

            // the deserialized box isn't the "same" box...
            Assert.AreNotEqual(0, typeTraits.Compare(t1, t2));

            // but structures should be analogous
            Assert.IsTrue(typeTraits.IsAnalogous(t1, t2));

            // now try cloning...
            Thingy t3 = typeTraits.Clone(t1);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t3));

            // cloned boxes aren't the "same"...
            Assert.AreNotEqual(0, typeTraits.Compare(t1, t3));

            // but structures should be analogous
            Assert.IsTrue(typeTraits.IsAnalogous(t1, t3));

            // also analogous with serialized / deserialized version
            Assert.IsTrue(typeTraits.IsAnalogous(t2, t3));
        }

        [TestMethod]
        public void TestBuilderWithBigValueTuple()
        {
            ITypeTraits<(string, int, bool, double, DateTime)> typeTraits = Builder.Instance.GetTypeTraits<(string, int, bool, double, DateTime)>();
            var t1 = ("abc", 100, true, 3.14, DateTime.Now);
            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);
            var t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));
            Assert.AreEqual(0, typeTraits.Compare(t1, t2));
        }

        [TestMethod]
        public void TestBuilderWithBigTuple()
        {
            ITypeTraits<Tuple<string, int, bool, double, DateTime>> typeTraits = Builder.Instance.GetTypeTraits<Tuple<string, int, bool, double, DateTime>>();
            var t1 = Tuple.Create("abc", 100, true, 3.14, DateTime.Now);
            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);
            var t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));
            Assert.AreEqual(0, typeTraits.Compare(t1, t2));
        }
    }

    [UnionOfDescendants]
    public abstract class Thingy
    {

    }

    [Record]
    [UnionCaseName("String")]
    public sealed class StringThingy : Thingy
    {
        private readonly string value;

        public StringThingy(string value)
        {
            this.value = value;
        }

        [Bind("value")]
        public string Value => value;
    }

    [Record]
    [UnionCaseName("BigInt")]
    public sealed class BigIntThingy : Thingy
    {
        private readonly BigInteger value;

        public BigIntThingy(BigInteger value)
        {
            this.value = value;
        }

        [Bind("value")]
        public BigInteger Value => value;
    }

    [Record]
    [UnionCaseName("List")]
    public sealed class ListThingy : Thingy
    {
        private readonly ImmutableList<Thingy> values;

        public ListThingy(ImmutableList<Thingy> values)
        {
            this.values = values;
        }

        [Bind("values")]
        public ImmutableList<Thingy> Values => values;
    }

    [Record]
    [UnionCaseName("Box")]
    public sealed class BoxThingy : Thingy
    {
        private readonly StrongBox<Thingy> box;

        public BoxThingy(StrongBox<Thingy> box)
        {
            this.box = box;
        }

        [Bind("box")]
        public StrongBox<Thingy> Box => box;
    }

    [Singleton(0xD7501E67u)]
    public sealed class NullThingy : Thingy
    {
        private static readonly NullThingy value = new NullThingy();

        private NullThingy() { }

        public static NullThingy Value => value;
    }

    [GensymInt32]
    public sealed class Gensym : Thingy
    {
        private static Lock staticSyncRoot = new Lock();
        private static int nextId;

        private readonly int id;

        // required default constructor
        public Gensym()
        {
            lock(staticSyncRoot)
            {
                id = nextId;
                ++nextId;
            }
        }

        // required property called ID
        public int ID => id;
    }
}
