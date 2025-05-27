using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Sunlighter.TypeTraitsLib.Building
{
    public static partial class Extensions
    {
        private static Lazy<ImmutableList<Type>> tupleTypes = new Lazy<ImmutableList<Type>>(GetTupleTypes, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableList<Type> GetTupleTypes()
        {
            return ImmutableList<Type>.Empty.AddRange
            (
                new Type[]
                {
                    typeof(Tuple<>),
                    typeof(Tuple<,>),
                    typeof(Tuple<,,>),
                    typeof(Tuple<,,,>),
                    typeof(Tuple<,,,,>),
                    typeof(Tuple<,,,,,>),
                    typeof(Tuple<,,,,,,>),
                    typeof(Tuple<,,,,,,,>),
                    typeof(ValueTuple<>),
                    typeof(ValueTuple<,>),
                    typeof(ValueTuple<,,>),
                    typeof(ValueTuple<,,,>),
                    typeof(ValueTuple<,,,,>),
                    typeof(ValueTuple<,,,,,>),
                    typeof(ValueTuple<,,,,,,>),
                    typeof(ValueTuple<,,,,,,,>)
                }
            );
        }

        private static ImmutableList<Type> TupleTypes => tupleTypes.Value;

        /// <summary>
        /// Returns true for both Tuple and ValueTuple types, but false otherwise.
        /// </summary>
        public static bool IsTupleType(this Type t)
        {
            if (!t.IsGenericType) return false;

            if (!t.IsGenericTypeDefinition) t = t.GetGenericTypeDefinition();

            return tupleTypes.Value.Any(u => t == u);
        }
    }
}
