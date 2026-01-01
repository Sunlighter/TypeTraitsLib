using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Sunlighter.TypeTraitsLib
{
    public static class AssemblyTypeTraits
    {
        private static readonly Lazy<(ITypeTraits<Assembly>, Func<Assembly, bool>)> typeTraits = new Lazy<(ITypeTraits<Assembly>, Func<Assembly, bool>)>(GetTypeTraits, LazyThreadSafetyMode.ExecutionAndPublication);

        private static (ImmutableSortedDictionary<string, Assembly>, ImmutableSortedDictionary<string, ImmutableList<Assembly>>) GetAssembliesByName()
        {
            Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            ImmutableSortedDictionary<string, ImmutableList<Assembly>> duplicates =
                ImmutableSortedDictionary<string, ImmutableList<Assembly>>.Empty;

            void addDuplicate(string name, Assembly a)
            {
                duplicates = duplicates.SetItem(name, duplicates.GetValueOrDefault(name, ImmutableList<Assembly>.Empty).Add(a));
            }

            ImmutableSortedDictionary<string, Assembly>.Builder results = ImmutableSortedDictionary<string, Assembly>.Empty.ToBuilder();

            foreach (Assembly a in allAssemblies)
            {
                string name = a.FullNameNotNull();
                if (duplicates.ContainsKey(name))
                {
                    addDuplicate(name, a);
                }
                else if (results.ContainsKey(name))
                {
                    Assembly old = results[name];
                    results.Remove(name);
                    addDuplicate(name, old);
                    addDuplicate(name, a);
                }
                else
                {
                    results.Add(name, a);
                }
            }

            if (!duplicates.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("Duplicate assemblies:");
                foreach (KeyValuePair<string, ImmutableList<Assembly>> kvp in duplicates)
                {
                    System.Diagnostics.Debug.WriteLine($"  {kvp.Key}:");
                    foreach (Assembly a in kvp.Value)
                    {
                        System.Diagnostics.Debug.WriteLine($"    {a.Location}");
                    }
                }
            }

            return (results.ToImmutable(), duplicates);
        }

        private static (ITypeTraits<Assembly>, Func<Assembly, bool>) GetTypeTraits()
        {
            object syncRoot = new object();
            (ImmutableSortedDictionary<string, Assembly> nameToAssembly, ImmutableSortedDictionary<string, ImmutableList<Assembly>> duplicates) = GetAssembliesByName();

            ITypeTraits<Assembly> assemblyTypeTraits = new ConvertTypeTraits<Assembly, string>
            (
                a => a.FullNameNotNull(),
                StringTypeTraits.Value,
                n =>
                {
                    lock (syncRoot)
                    {
                        if (nameToAssembly.ContainsKey(n))
                        {
                            return nameToAssembly[n];
                        }
                        else
                        {
                            (nameToAssembly, duplicates) = GetAssembliesByName();
                            return nameToAssembly[n]; // if it throws, we did what we could
                        }
                    }
                }
            );

            bool isDuplicate(Assembly a)
            {
                lock (syncRoot)
                {
                    string name = a.FullNameNotNull();
                    if (duplicates.ContainsKey(name)) return true;
                    if (nameToAssembly.ContainsKey(name)) return false;

                    // this is a new assembly -- but it might cause some other assembly to become a duplicate!

                    (nameToAssembly, duplicates) = GetAssembliesByName();
                    if (duplicates.ContainsKey(name)) return true;
                    return false;
                }
            };

            return (assemblyTypeTraits, isDuplicate);
        }

        public static ITypeTraits<Assembly> Value => typeTraits.Value.Item1;

        private static readonly Lazy<Adapter<Assembly>> adapter = new Lazy<Adapter<Assembly>>(() => Adapter<Assembly>.Create(typeTraits.Value.Item1), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Adapter<Assembly> Adapter => adapter.Value;

        public static bool IsDuplicate(Assembly a) => typeTraits.Value.Item2(a);
    }

    public static class TypeTypeTraits
    {
        private static readonly Lazy<ITypeTraits<Type>> typeTraits = new Lazy<ITypeTraits<Type>>(GetTypeTraits, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<Type> GetTypeTraits()
        {
            RecursiveTypeTraits<Type> recurse = new RecursiveTypeTraits<Type>();

            ITypeTraits<Type> t = new GuardedTypeTraits<Type>
            (
                t0 => !t0.IsByRef && !t0.IsPointer,
                new UnionTypeTraits<string, Type>
                (
                    StringTypeTraits.Value,
                    new IUnionCaseTypeTraits<string, Type>[]
                    {
                        new UnionCaseTypeTraits<string, Type, ValueTuple<string, Assembly>>
                        (
                            "plainType",
                            t1 => !t1.IsGenericType && !t1.IsArray && !t1.IsGenericParameter,
                            t2 => (t2.FullNameNotNull(), t2.Assembly),
                            new ValueTupleTypeTraits<string, Assembly>
                            (
                                StringTypeTraits.Value,
                                AssemblyTypeTraits.Value
                            ),
                            tu => tu.Item2.GetType(tu.Item1, true).AssertNotNull()
                        ),
                        new UnionCaseTypeTraits<string, Type, ValueTuple<string, Assembly, int>>
                        (
                            "openGeneric",
                            t3 => t3.IsGenericTypeDefinition,
                            t4 => (t4.FullNameNotNull(), t4.Assembly, t4.GetGenericArguments().Length),
                            new ValueTupleTypeTraits<string, Assembly, int>
                            (
                                StringTypeTraits.Value,
                                AssemblyTypeTraits.Value,
                                Int32TypeTraits.Value
                            ),
                            tu =>
                            {
                                try
                                {
                                    return tu.Item2.GetTypes().Where(t10 => t10.FullName == tu.Item1 && t10.IsGenericTypeDefinition && t10.GetGenericArguments().Length == tu.Item3).Single();
                                }
                                catch(InvalidOperationException)
                                {
                                    throw new TypeTraitsException($"Could not find open generic, assembly {tu.Item2.FullName}, name {tu.Item1}, number of args {tu.Item3}");
                                }
                            }
                        ),
                        new UnionCaseTypeTraits<string, Type, ValueTuple<string, Assembly, ImmutableList<Type>>>
                        (
                            "closedGeneric",
                            t5 => t5.IsGenericType && !t5.IsGenericTypeDefinition,
                            t6 => (t6.GetGenericTypeDefinition().FullNameNotNull(), t6.Assembly, t6.GetGenericArguments().ToImmutableList()),
                            new ValueTupleTypeTraits<string, Assembly, ImmutableList<Type>>
                            (
                                StringTypeTraits.Value,
                                AssemblyTypeTraits.Value,
                                new ListTypeTraits<Type>(recurse)
                            ),
                            tu =>
                            {
                                try
                                {
                                    Type openGeneric = tu.Item2.GetTypes().Where(t11 => t11.FullName == tu.Item1 && t11.IsGenericTypeDefinition && t11.GetGenericArguments().Length == tu.Item3.Count).Single();

                                    return openGeneric.MakeGenericType(tu.Item3.ToArray());
                                }
                                catch(InvalidOperationException)
                                {
                                    throw new TypeTraitsException($"Could not find open generic, assembly {tu.Item2.FullName}, name {tu.Item1}, number of args {tu.Item3.Count}");
                                }
                            }
                        ),
                        new UnionCaseTypeTraits<string, Type, ValueTuple<Type, int>>
                        (
                            "genericTypeParameter",
#if NETSTANDARD2_0
                            t7 => t7.IsGenericParameter && t7.DeclaringMethod == null,
#else
                            t7 => t7.IsGenericTypeParameter,
#endif
                            t8 => (t8.DeclaringType.AssertNotNull(), t8.GenericParameterPosition),
                            new ValueTupleTypeTraits<Type, int>
                            (
                                recurse,
                                Int32TypeTraits.Value
                            ),
                            tu =>
                            {
                                Type[] genericArgs = tu.Item1.GetGenericArguments();
                                if (tu.Item2 < 0 || tu.Item2 >= genericArgs.Length)
                                {
                                    throw new TypeTraitsException($"Invalid generic parameter index {tu.Item2} for type {tu.Item1.FullName}");
                                }
                                return genericArgs[tu.Item2];
                            }
                        ),
                        new UnionCaseTypeTraits<string, Type, Type>
                        (
                            "array",
                            t7 => t7.IsArray,
                            t8 => t8.GetElementType().AssertNotNull(),
                            recurse,
                            t9 => t9.MakeArrayType()
                        )
                    }
                    .ToImmutableList()
                )
            );

            recurse.Set(t);

            return t;
        }

        public static ITypeTraits<Type> Value => typeTraits.Value;

        private static readonly Lazy<Adapter<Type>> adapter = new Lazy<Adapter<Type>>(() => Adapter<Type>.Create(typeTraits.Value), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Adapter<Type> Adapter => adapter.Value;

        public static bool IsDuplicateAssembly(Type t)
        {
            return AssemblyTypeTraits.IsDuplicate(t.Assembly);
        }
    }
}
