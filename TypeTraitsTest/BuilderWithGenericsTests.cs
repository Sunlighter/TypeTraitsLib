using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;
using System.Collections.Immutable;
using System.Numerics;

namespace TypeTraitsTest
{
    [TestClass]
    public sealed class BuilderWithGenericsTests
    {
        [TestMethod]
        public void TestTryUnify()
        {
            Option<Type> resultOpt = Sunlighter.TypeTraitsLib.Building.Extensions.TryUnify(typeof(GenericThingy<bool>), typeof(GenericStringThingy<>));

            Assert.IsTrue(resultOpt.HasValue && resultOpt.Value == typeof(GenericStringThingy<bool>));
        }

        [TestMethod]
        public void TestBuilderWithGenerics()
        {
            ITypeTraits<GenericThingy<bool>> typeTraits = Builder.Instance.GetTypeTraits<GenericThingy<bool>>();

            GenericThingy<bool> t1 = new GenericListThingy<bool>
            (
                [
                    new GenericStringThingy<bool>("abc"),
                    new GenericBigIntThingy<bool>(100),
                    new GenericListThingy<bool>
                    (
                        [
                            new GenericStringThingy<bool>("def"),
                            new GenericUserThingy<bool>(false),
                            new HiddenDescendant<bool>([ true, true, false ]),
                            new Accessory("accessory"),
                        ]
                    ),
                ]
            );

            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);

            GenericThingy<bool> t2 = typeTraits.DeserializeFromBytes(serializedBytes);

            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));

            Assert.IsTrue(typeTraits.Compare(t1, t2) == 0);
        }

        [TestMethod]
        public void TestBuilderWithGenerics2()
        {
            ITypeTraits<GenericThingy<DateTime>> typeTraits = Builder.Instance.GetTypeTraits<GenericThingy<DateTime>>();

            GenericThingy<DateTime> t1 = new GenericListThingy<DateTime>
            (
                [
                    new GenericStringThingy<DateTime>("abc"),
                    new GenericBigIntThingy<DateTime>(100),
                    new GenericListThingy<DateTime>
                    (
                        [
                            new GenericStringThingy<DateTime>("def"),
                            new HiddenDescendant<DateTime>([ DateTime.Today, DateTime.UtcNow ]),
                            new GenericUserThingy<DateTime>(DateTime.Now),
                        ]
                    ),
                ]
            );

            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);
            GenericThingy<DateTime> t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));
            Assert.IsTrue(typeTraits.Compare(t1, t2) == 0);

            Assert.IsFalse(typeTraits.CanSerialize(new Troublemaker<DateTime, ImmutableList<string>>(DateTime.Now, [ "abc" ])));
        }

        [TestMethod]
        public void TestBuilderWithGenerics3()
        {
            ITypeTraits<TwoGenericArguments<string, double>> typeTraits = Builder.Instance.GetTypeTraits<TwoGenericArguments<string, double>>();
            TwoGenericArguments<string, double> t1 = new Example1<string, double>("abc", 100.0);
            byte[] serializedBytes = typeTraits.SerializeToBytes(t1);
            TwoGenericArguments<string, double> t2 = typeTraits.DeserializeFromBytes(serializedBytes);
            System.Diagnostics.Debug.WriteLine(typeTraits.ToDebugString(t2));
            Assert.IsTrue(typeTraits.Compare(t1, t2) == 0);

            ITypeTraits<TwoGenericArguments<double, string>> typeTraits2 = Builder.Instance.GetTypeTraits<TwoGenericArguments<double, string>>();
            TwoGenericArguments<double, string> t3 = new Example1<double, string>(100.0, "ghi");
            byte[] serializedBytes2 = typeTraits2.SerializeToBytes(t3);
            TwoGenericArguments<double, string> t4 = typeTraits2.DeserializeFromBytes(serializedBytes2);
            System.Diagnostics.Debug.WriteLine(typeTraits2.ToDebugString(t4));
            Assert.IsTrue(typeTraits2.Compare(t3, t4) == 0);

            ITypeTraits<TwoGenericArguments<float, int>> typeTraits3 = Builder.Instance.GetTypeTraits<TwoGenericArguments<float, int>>();
            TwoGenericArguments<float, int> t5 = new Example2<float>(100.0f);
            byte[] serializedBytes3 = typeTraits3.SerializeToBytes(t5);
            TwoGenericArguments<float, int> t6 = typeTraits3.DeserializeFromBytes(serializedBytes3);
            System.Diagnostics.Debug.WriteLine(typeTraits3.ToDebugString(t6));
            Assert.IsTrue(typeTraits3.Compare(t5, t6) == 0);
        }
    }

    [UnionOfDescendants]
    public abstract class GenericThingy<T>
    {

    }

    [Record]
    [UnionCaseName("String")]
    public sealed class GenericStringThingy<T> : GenericThingy<T>
    {
        private readonly string value;

        public GenericStringThingy(string value)
        {
            this.value = value;
        }

        [Bind("value")]
        public string Value => value;
    }

    [Record]
    [UnionCaseName("BigInt")]
    public sealed class GenericBigIntThingy<T> : GenericThingy<T>
    {
        private readonly BigInteger value;

        public GenericBigIntThingy(BigInteger value)
        {
            this.value = value;
        }

        [Bind("value")]
        public BigInteger Value => value;
    }

    [Record]
    [UnionCaseName("List")]
    public sealed class GenericListThingy<T> : GenericThingy<T>
    {
        private readonly ImmutableList<GenericThingy<T>> values;

        public GenericListThingy(ImmutableList<GenericThingy<T>> values)
        {
            this.values = values;
        }

        [Bind("values")]
        public ImmutableList<GenericThingy<T>> Values => values;
    }

    [Record]
    [UnionCaseName("User")]
    public sealed class GenericUserThingy<T> : GenericThingy<T>
    {
        private readonly T value;

        public GenericUserThingy(T value)
        {
            this.value = value;
        }

        [Bind("value")]
        public T Value => value;
    }

    [Record]
    [UnionCaseName("Troublemaker")]
    public sealed class Troublemaker<T, U> : GenericThingy<T>
    {
        private readonly T value1;
        private readonly U value2;

        public Troublemaker(T value1, U value2)
        {
            this.value1 = value1;
            this.value2 = value2;
        }

        [Bind("value1")]
        public T Value1 => value1;

        [Bind("value2")]
        public U Value2 => value2;
    }

    public abstract class WithDescendants<T> : GenericThingy<T>
    {

    }

    [Record]
    [UnionCaseName("HiddenDescendant")]
    public sealed class HiddenDescendant<T> : WithDescendants<T>
    {
        private readonly ImmutableList<T> values;

        public HiddenDescendant(ImmutableList<T> values)
        {
            this.values = values;
        }

        [Bind("values")]
        public ImmutableList<T> Values => values;
    }

    [Record]
    [UnionCaseName("Accessory")]
    public sealed class Accessory : GenericThingy<bool>
    {
        private readonly string name;

        public Accessory(string name)
        {
            this.name = name;
        }

        [Bind("name")]
        public string Name => name;
    }

    [UnionOfDescendants]
    public abstract class TwoGenericArguments<T, U>
    {

    }

    [Record]
    [UnionCaseName("Example1")]
    public sealed class Example1<T, U> : TwoGenericArguments<T, U>
    {
        private readonly T item1;
        private readonly U item2;

        public Example1(T item1, U item2)
        {
            this.item1 = item1;
            this.item2 = item2;
        }

        [Bind("item1")]
        public T Item1 => item1;

        [Bind("item2")]
        public U Item2 => item2;
    }

    [Record]
    [UnionCaseName("Example2")]
    public sealed class Example2<T> : TwoGenericArguments<T, int>
        where T : struct
    {
        private readonly T item;

        public Example2(T item)
        {
            this.item = item;
        }

        [Bind("item")]
        public T Item => item;
    }
}
