using Sunlighter.OptionLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sunlighter.TypeTraitsLib.Building
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UnionOfDescendantsAttribute : Attribute
    {
        public UnionOfDescendantsAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RecordAttribute : Attribute
    {
        public RecordAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SingletonAttribute : Attribute
    {
        private readonly uint hashToken;

        public SingletonAttribute(uint hashToken)
        {
            this.hashToken = hashToken;
        }

        public uint HashToken => hashToken;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UnionCaseNameAttribute : Attribute
    {
        private readonly string name;

        public UnionCaseNameAttribute(string name)
        {
            this.name = name;
        }

        public string Name => name;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class BindAttribute : Attribute
    {
        private readonly string name;

        public BindAttribute(string name)
        {
            this.name = name;
        }

        public string Name => name;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProvidesOwnTypeTraitsAttribute : Attribute
    {
        public ProvidesOwnTypeTraitsAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProvidesOwnAdapterAttribute : Attribute
    {
        public ProvidesOwnAdapterAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GensymInt32Attribute : Attribute
    {
        public GensymInt32Attribute() { }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class UseSpecificTraitsAttribute : Attribute
    {
        private readonly Type hostingType;

        public UseSpecificTraitsAttribute(Type hostingType)
        {
            this.hostingType = hostingType;
        }

        public Type HostingType => hostingType;
    }

    public abstract class RecordOrUnionInfo
    {

    }

    public sealed class RecordInfo : RecordOrUnionInfo
    {
        private readonly ConstructorInfo constructorInfo;
        private readonly ImmutableSortedDictionary<string, Type> bindingTypes;
        private readonly ImmutableList<string> constructorBindings;
        private readonly ImmutableSortedDictionary<string, MemberInfo> propertyBindings;
        private readonly ImmutableSortedDictionary<string, ArtifactKey> specialTypeTraits;

        public RecordInfo
        (
            ConstructorInfo constructorInfo,
            ImmutableSortedDictionary<string, Type> bindingTypes,
            ImmutableList<string> constructorBindings,
            ImmutableSortedDictionary<string, MemberInfo> propertyBindings,
            ImmutableSortedDictionary<string, ArtifactKey> specialTypeTraits
        )
        {
            this.constructorInfo = constructorInfo;
            this.bindingTypes = bindingTypes;
            this.constructorBindings = constructorBindings;
            this.propertyBindings = propertyBindings;
            this.specialTypeTraits = specialTypeTraits;
        }


        public ConstructorInfo ConstructorInfo => constructorInfo;

        public ImmutableSortedDictionary<string, Type> BindingTypes => bindingTypes;

        public ImmutableList<string> ConstructorBindings => constructorBindings;

        public ImmutableSortedDictionary<string, MemberInfo> PropertyBindings => propertyBindings;

        public ImmutableSortedDictionary<string, ArtifactKey> SpecialTypeTraits => specialTypeTraits;
    }

    public sealed class UnionOfDescendantsInfo : RecordOrUnionInfo
    {
        private readonly ImmutableSortedDictionary<Type, (string, RecordOrUnionInfo)> descendants;

        public UnionOfDescendantsInfo(ImmutableSortedDictionary<Type, (string, RecordOrUnionInfo)> descendants)
        {
            this.descendants = descendants;
        }

        public ImmutableSortedDictionary<Type, (string, RecordOrUnionInfo)> Descendants => descendants;
    }

    public sealed class SingletonInfo : RecordOrUnionInfo
    {
        private readonly uint hashToken;
        private readonly PropertyInfo singletonProperty;

        public SingletonInfo(uint hashToken, PropertyInfo singletonProperty)
        {
            this.hashToken = hashToken;
            this.singletonProperty = singletonProperty;
        }

        public uint HashToken => hashToken;

        public PropertyInfo SingletonProperty => singletonProperty;
    }

    public sealed class GensymInfo : RecordOrUnionInfo
    {
        public GensymInfo()
        {

        }
    }

    public static partial class Extensions
    {
        private static RecordInfo GetTupleRecordInfo(this Type t)
        {
            if (!t.IsTupleType()) throw new BuilderException("Tuple expected");

            ConstructorInfo ci = t.GetRequiredConstructor(t.GetGenericArguments());

            ParameterInfo[] constructorParameters = ci.GetParameters();

            ImmutableList<string> constructorBindings = constructorParameters.Select(par => par.Name.AssertNotNull().ToLowerInvariant()).ToImmutableList();

            ImmutableSortedDictionary<string, int> nameToIndex = Enumerable.Range(0, constructorParameters.Length)
                .Select(i => new KeyValuePair<string, int>(constructorBindings[i], i))
                .ToImmutableSortedDictionary();

            ImmutableSortedDictionary<string, Type> bindingTypes = Enumerable.Range(0, constructorParameters.Length)
                .Select(i => new KeyValuePair<string, Type>(constructorBindings[i], constructorParameters[i].ParameterType))
                .ToImmutableSortedDictionary();

            ImmutableSortedDictionary<string, MemberInfo> propertyBindings = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Cast<MemberInfo>()
                .Concat(t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(p => (p.Name.ToLowerInvariant(), p))
                .Where(pt => nameToIndex.ContainsKey(pt.Item1))
                .Select(pt => new KeyValuePair<string, MemberInfo>(pt.Item1, pt.Item2))
                .ToImmutableSortedDictionary();

            return new RecordInfo
            (
                ci,
                bindingTypes,
                constructorBindings,
                propertyBindings,
                ImmutableSortedDictionary<string, ArtifactKey>.Empty
            );
        }

        private static RecordInfo GetDirectRecordInfo(this Type t)
        {
#if NETSTANDARD2_0
            RecordAttribute ra = t.GetCustomAttribute<RecordAttribute>();
#else
            RecordAttribute? ra = t.GetCustomAttribute<RecordAttribute>();
#endif
            if (ra == null) throw new BuilderException($"Type {TypeTraitsUtility.GetTypeName(t)} does not have a RecordAttribute");

            ImmutableList<ConstructorInfo> ciList = t.GetConstructors().ToImmutableList();

            ConstructorInfo ci;
            if (ciList.Count == 1)
            {
                ci = ciList[0];
            }
            else if (ciList.Count > 1)
            {
                ciList = ciList.Where(ci2 => ci2.GetParameters().Any(cp => cp.IsDefined(typeof(BindAttribute)))).ToImmutableList();
                if (ciList.Count == 1)
                {
                    ci = ciList[0];
                }
                else
                {
                    throw new BuilderException($"Type {TypeTraitsUtility.GetTypeName(t)} has more than one constructor with bindings (or more than one with no bindings on any of them)");
                }
            }
            else
            {
                throw new BuilderException($"Type {TypeTraitsUtility.GetTypeName(t)} does not have a constructor");
            }
             
            ParameterInfo[] ciParams = ci.GetParameters();

            ImmutableList<string> constructorBindings = ImmutableList<string>.Empty;

            ImmutableSortedDictionary<string, int> constructorParameterIndex = ImmutableSortedDictionary<string, int>.Empty;
            ImmutableSortedDictionary<string, ArtifactKey> specialTypeTraits = ImmutableSortedDictionary<string, ArtifactKey>.Empty;

            foreach (int i in Enumerable.Range(0, ciParams.Length))
            {
                ParameterInfo ciParam = ciParams[i];

                string bindSymbol;

                if (ciParam.IsDefined(typeof(BindAttribute)))
                {
                    BindAttribute bindAttr = ciParam.GetCustomAttribute<BindAttribute>().AssertNotNull();
                    bindSymbol = bindAttr.Name;
                }
#if NETSTANDARD2_0
                else if (ciParam.Name != null)
#else
                else if (ciParam.Name is not null)
#endif
                {
                    bindSymbol = ciParam.Name;
                }
                else
                {
                    throw new BuilderException($"Constructor parameter {i} of type {TypeTraitsUtility.GetTypeName(t)} has neither a BindAttribute nor a name");
                }

                if (constructorBindings.Contains(bindSymbol))
                {
                    throw new BuilderException($"Constructor for type {TypeTraitsUtility.GetTypeName(t)} has duplicate binding symbol {bindSymbol}");
                }
                else
                {
                    if (ciParam.IsDefined(typeof(UseSpecificTraitsAttribute)))
                    {
                        UseSpecificTraitsAttribute useTraitsAttr = ciParam.GetCustomAttribute<UseSpecificTraitsAttribute>().AssertNotNull();
                        ArtifactKey ak = ArtifactKey.Create(ArtifactType.SpecialTypeTraits, ciParam.ParameterType, useTraitsAttr.HostingType);

                        if (specialTypeTraits.ContainsKey(bindSymbol))
                        {
                            throw new BuilderException($"Constructor for type {TypeTraitsUtility.GetTypeName(t)} has duplicate special type traits on binding symbol {bindSymbol}");
                        }
                        else
                        {
                            specialTypeTraits = specialTypeTraits.Add(bindSymbol, ak);
                        }
                    }
                    constructorParameterIndex = constructorParameterIndex.Add(bindSymbol, i);
                    constructorBindings = constructorBindings.Add(bindSymbol);
                }
            }
                    
            ImmutableList<PropertyInfo> properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.IsDefined(typeof(BindAttribute))).ToImmutableList();

            ImmutableSortedDictionary<string, int> propertyIndex = ImmutableSortedDictionary<string, int>.Empty;
            foreach (int i in Enumerable.Range(0, properties.Count))
            {
                BindAttribute bindAttr = properties[i].GetCustomAttribute<BindAttribute>().AssertNotNull();
                string bindSymbol = bindAttr.Name;
                if (propertyIndex.ContainsKey(bindSymbol))
                {
                    throw new BuilderException($"Properties for type {TypeTraitsUtility.GetTypeName(t)} have duplicate binding symbol {bindSymbol}");
                }
                else
                {
                    propertyIndex = propertyIndex.Add(bindSymbol, i);
                }

                if (properties[i].IsDefined(typeof(UseSpecificTraitsAttribute)))
                {
                    UseSpecificTraitsAttribute useTraitsAttr = properties[i].GetCustomAttribute<UseSpecificTraitsAttribute>().AssertNotNull();
                    ArtifactKey ak = ArtifactKey.Create(ArtifactType.SpecialTypeTraits, properties[i].PropertyType, useTraitsAttr.HostingType);
                    if (specialTypeTraits.ContainsKey(bindSymbol))
                    {
                        throw new BuilderException($"Properties for type {TypeTraitsUtility.GetTypeName(t)} have duplicate special type traits on binding symbol {bindSymbol}");
                    }
                    else
                    {
                        specialTypeTraits = specialTypeTraits.Add(bindSymbol, ak);
                    }
                }
            }

            ImmutableSortedDictionary<string, Type> bindingTypes = ImmutableSortedDictionary<string, Type>.Empty;

            ImmutableSortedSet<string> bindingSymbols = ImmutableSortedSet<string>.Empty.Union(constructorParameterIndex.Keys).Union(propertyIndex.Keys);

            foreach(string s in bindingSymbols)
            {
                Type cpType;

                if (constructorParameterIndex.TryGetValue(s, out int constructorParameterIndexValue))
                {
                    cpType = ciParams[constructorParameterIndexValue].ParameterType;
                }
                else
                {
                    throw new BuilderException($"Binding symbol {s} not found in constructor parameters for type {TypeTraitsUtility.GetTypeName(t)}");
                }

                Type propType;
                if (!propertyIndex.TryGetValue(s, out int propertyIndexValue))
                {
                    throw new BuilderException($"Binding symbol {s} not found in properties for type {TypeTraitsUtility.GetTypeName(t)}");
                }
                else
                {
                    propType = properties[propertyIndexValue].PropertyType;
                }

                if (TypeTypeTraits.Value.Compare(cpType, propType) != 0)
                {
                    throw new BuilderException($"Type {TypeTraitsUtility.GetTypeName(t)} has inconsistent type for binding {s} ({TypeTraitsUtility.GetTypeName(cpType)} vs {TypeTraitsUtility.GetTypeName(propType)}");
                }

                bindingTypes = bindingTypes.Add(s, cpType);
            }

            return new RecordInfo
            (
                ci,
                bindingTypes,
                constructorBindings,
                propertyIndex.Map((k, i) => (MemberInfo)properties[i]),
                specialTypeTraits
            );
        }

        private static SingletonInfo GetSingletonInfo(this Type t)
        {
            ImmutableList<PropertyInfo> propertyCandidates = t.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.CanRead && t.IsAssignableFrom(p.PropertyType))
                .ToImmutableList();

            if (propertyCandidates.IsEmpty)
            {
                throw new BuilderException("Cannot find property on Singleton class");
            }
            else if (propertyCandidates.Count > 1)
            {
                throw new BuilderException("More than one possible Singleton property on Singleton class");
            }
            else
            {
                SingletonAttribute attr = t.GetCustomAttribute<SingletonAttribute>().AssertNotNull();
                return new SingletonInfo(attr.HashToken, propertyCandidates[0]);
            }
        }

        private static GensymInfo GetGensymInfo(this Type t)
        {
            return new GensymInfo();
        }

        /// <summary>
        /// If closedGeneric is an instantiation of openGeneric, returns a dictionary from the parameters of openGeneric to the arguments of closedGeneric
        /// openGeneric may actually be closed but may have generic parameters from another type, e.g. Special&lt;T&gt; inheriting from Plain&lt;T, int&gt;.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if closedGeneric is an open generic type</exception>
        private static Option<ImmutableSortedDictionary<Type, Type>> CanUnifyExactType(Type closedGeneric, Type openGeneric)
        {
            if (closedGeneric.IsGenericType)
            {
                if (closedGeneric.IsGenericTypeDefinition)
                {
                    throw new InvalidOperationException($"{nameof(closedGeneric)} must not have any unbound type variables");
                }

                if (openGeneric.IsGenericType)
                {
                    Type closedGenericDefinition = closedGeneric.GetGenericTypeDefinition();
                    Type openGenericDefinition = openGeneric.GetGenericTypeDefinition();

                    if (openGenericDefinition == closedGenericDefinition)
                    {
                        ImmutableSortedDictionary<Type, Type> result =
                            ImmutableSortedDictionary<Type, Type>.Empty.WithComparers(TypeTypeTraits.Adapter);

                        Type[] closedGenericArguments = closedGeneric.GetGenericArguments();
                        Type[] openGenericArguments = openGeneric.GetGenericArguments();

                        int iEnd = closedGenericArguments.Length;
                        for (int i = 0; i < iEnd; ++i)
                        {
                            if (openGenericArguments[i].IsGenericParameter)
                            {
                                Type[] constraints = openGenericArguments[i].GetGenericParameterConstraints();
                                if (constraints.All(c => c.IsAssignableFrom(closedGenericArguments[i])))
                                {
                                    result = result.Add(openGenericArguments[i], closedGenericArguments[i]);
                                }
                            }
                            else if (openGenericArguments[i] != closedGenericArguments[i])
                            {
                                return Option<ImmutableSortedDictionary<Type, Type>>.None;
                            }
                            // else keep looping
                        }

                        return Option<ImmutableSortedDictionary<Type, Type>>.Some(result);
                    }
                    else
                    {
                        // different generic types
                        return Option<ImmutableSortedDictionary<Type, Type>>.None;
                    }
                }
                else
                {
                    // one generic, one not
                    return Option<ImmutableSortedDictionary<Type, Type>>.None;
                }
            }
            else
            {
                if (openGeneric.IsGenericType)
                {
                    // one generic, one not
                    return Option<ImmutableSortedDictionary<Type, Type>>.None;
                }
                else if (closedGeneric == openGeneric)
                {
                    return Option<ImmutableSortedDictionary<Type, Type>>.Some
                    (
                        ImmutableSortedDictionary<Type, Type>.Empty.WithComparers(TypeTypeTraits.Adapter)
                    );
                }
                else
                {
                    // different non-generic types
                    return Option<ImmutableSortedDictionary<Type, Type>>.None;
                }
            }
        }

        /// <summary>
        /// If candidateDescendant (an open generic type) can be constructed so that it inherits from closedGenericBase,
        /// returns a dictionary from the parameters of candidateDescendant to the values needed to close the type.
        /// </summary>
        public static Option<Type> TryUnify(Type closedGenericBase, Type candidateDescendant)
        {
            if (closedGenericBase.IsInterface)
            {
                ImmutableList<(Type, ImmutableSortedDictionary<Type, Type>)> interfaces =
                    candidateDescendant.GetInterfaces()
                    .Select(i => new ValueTuple<Type, Option<ImmutableSortedDictionary<Type, Type>>>(i, CanUnifyExactType(i, candidateDescendant)))
                    .Where(i => i.Item2.HasValue)
                    .Select(i => (i.Item1, i.Item2.Value))
                    .ToImmutableList();

                if (interfaces.IsEmpty)
                {
                    return Option<Type>.None;
                }
                else if (interfaces.Count == 1)
                {
                    ImmutableSortedDictionary<Type, Type> types = interfaces[0].Item2;
                    if (types.IsEmpty && !candidateDescendant.IsGenericTypeDefinition)
                    {
                        return Option<Type>.Some(candidateDescendant);
                    }
                    else if (candidateDescendant.IsGenericTypeDefinition)
                    {
                        Type[] closedGenericArguments = candidateDescendant.GetGenericArguments().Select(a => types.GetValueOrDefault(a, a)).ToArray();
                        if (closedGenericArguments.Any(a => a.IsGenericParameter))
                        {
                            // this would lead to an open-ended collection of union cases!
                            return Option<Type>.None;
                        }
                        else
                        {
                            return Option<Type>.Some(candidateDescendant.MakeGenericType(closedGenericArguments));
                        }
                    }
                    else
                    {
                        return Option<Type>.None;
                    }
                }
                else
                {
                    throw new BuilderException
                    (
                        $"Multiple matching interfaces for {TypeTraitsUtility.GetTypeName(closedGenericBase)} : " +
                        string.Join(", ", interfaces.Select(j => TypeTraitsUtility.GetTypeName(j.Item1)))
                    );
                }
            }
            else
            {
#if NETSTANDARD2_0
                Type baseType = candidateDescendant.BaseType;
                if (baseType != null)
#else
                Type? baseType = candidateDescendant.BaseType;
                if (baseType is not null)
#endif
                {
                    Option<ImmutableSortedDictionary<Type, Type>> unifyResult = CanUnifyExactType(closedGenericBase, baseType);
                    if (unifyResult.HasValue)
                    {
                        if (candidateDescendant.IsGenericTypeDefinition)
                        {
                            Type[] closedGenericArguments = candidateDescendant.GetGenericArguments().Select(a => unifyResult.Value.GetValueOrDefault(a, a)).ToArray();
                            if (closedGenericArguments.Any(a => a.IsGenericParameter))
                            {
                                // this would lead to an open-ended collection of union cases!
                                return Option<Type>.None;
                            }
                            else
                            {
                                return Option<Type>.Some(candidateDescendant.MakeGenericType(closedGenericArguments));
                            }
                        }
                        else
                        {
                            return Option<Type>.Some(candidateDescendant);
                        }
                    }
                    else
                    {
                        return Option<Type>.None;
                    }
                }
                else
                {
                    return Option<Type>.None;
                }
            }
        }

        private static ImmutableSortedSet<T> Closure<T>(ImmutableSortedSet<T> initialItems, Func<T, ImmutableList<T>> getSuccessors)
        {
            ImmutableSortedSet<T> result = initialItems;
            ImmutableList<T> todoQueue = ImmutableList<T>.Empty.AddRange(initialItems);
            while (!todoQueue.IsEmpty)
            {
                T item = todoQueue[0];
                todoQueue = todoQueue.RemoveAt(0);

                ImmutableList<T> successors = getSuccessors(item);

                foreach (T successor in successors)
                {
                    if (!result.Contains(successor))
                    {
                        result = result.Add(successor);
                        todoQueue = todoQueue.Add(successor);
                    }
                }
            }
            return result;
        }

        private static Option<RecordOrUnionInfo> GetRecordInfoInternal(this Type t)
        {
            if (t.IsTupleType())
            {
                return Option<RecordOrUnionInfo>.Some(GetTupleRecordInfo(t));
            }
            else if (t.IsDefined(typeof(SingletonAttribute)))
            {
                return Option<RecordOrUnionInfo>.Some(GetSingletonInfo(t));
            }
            else if (t.IsDefined(typeof(GensymInt32Attribute)))
            {
                return Option<RecordOrUnionInfo>.Some(GetGensymInfo(t));
            }
            else if (t.IsDefined(typeof(RecordAttribute)))
            {
                if (t.IsDefined(typeof(UnionOfDescendantsAttribute)))
                {
                    throw new BuilderException("UnionOfDescendantsAttribute and RecordAttribute cannot be applied to the same class");
                }

                return Option<RecordOrUnionInfo>.Some(GetDirectRecordInfo(t));
            }
            else if (t.IsDefined(typeof(UnionOfDescendantsAttribute)))
            {
                ImmutableSortedSet<Type> subTypes = ImmutableSortedSet<Type>.Empty.WithComparer(TypeTypeTraits.Adapter);

                ImmutableSortedSet<Type> ReachableFrom(Type t0)
                {
                    return Closure
                    (
                        ImmutableSortedSet<Type>.Empty.WithComparer(TypeTypeTraits.Adapter).Add(t0),
                        t1 =>
                        {
                            ImmutableList<Type> result = ImmutableList<Type>.Empty;
                            if (t1.IsAbstract || t1.IsInterface)
                            {
                                foreach (Type st in t.Assembly.GetTypes().Where(t2 => (t2.IsPublic || t2.IsNestedPublic)))
                                {
                                    Option<Type> unifyOpt = TryUnify(t1, st);
                                    if (unifyOpt.HasValue)
                                    {
                                        Type tResult = unifyOpt.Value;
                                        result = result.Add(tResult);
                                    }
                                }
                            }
                            return result;
                        }
                    )
                    .Where(t2 => !t2.IsAbstract)
                    .ToImmutableSortedSet(TypeTypeTraits.Adapter);
                }

                if (t.IsInterface)
                {
                    if (t.IsGenericType)
                    {
                        if (t.IsGenericTypeDefinition)
                        {
                            // can only gather descendants of closed generic types
                            return Option<RecordOrUnionInfo>.None;
                        }
                        else
                        {
                            subTypes = subTypes.Union(ReachableFrom(t));
                        }
                    }
                    else
                    {
                        foreach (Type st in t.Assembly.GetTypes().Where(t2 => (t2.IsPublic || t2.IsNestedPublic) && !t2.IsAbstract && t.IsAssignableFrom(t2)))
                        {
                            subTypes = subTypes.Add(st);
                        }
                    }
                }
                else if (t.IsClass && t.IsAbstract)
                {
                    if (t.IsGenericType)
                    {
                        if (t.IsGenericTypeDefinition)
                        {
                            // can only gather descendants of closed generic types
                            return Option<RecordOrUnionInfo>.None;
                        }
                        else
                        {
                            subTypes = subTypes.Union(ReachableFrom(t));
                        }
                    }
                    else
                    {
                        foreach (Type st in t.Assembly.GetTypes().Where(t2 => (t2.IsPublic || t2.IsNestedPublic) && t2.BaseType == t))
                        {
                            subTypes = subTypes.Add(st);
                        }
                    }
                }
                else
                {
                    throw new BuilderException("UnionOfDescendantsAttribute can only be applied to an abstract class or interface");
                }

                ImmutableSortedDictionary<Type, (string, RecordOrUnionInfo)> results =
                    ImmutableSortedDictionary<Type, (string, RecordOrUnionInfo)>.Empty.WithComparers(TypeTypeTraits.Adapter);

                foreach (Type st in subTypes)
                {
                    Option<RecordOrUnionInfo> subInfo = st.TryGetRecordInfo();
                    if (subInfo.HasValue)
                    {
                        string unionCaseName;

                        if (st.IsDefined(typeof(UnionCaseNameAttribute)))
                        {
                            UnionCaseNameAttribute unionCase = st.GetCustomAttribute<UnionCaseNameAttribute>().AssertNotNull();
                            unionCaseName = unionCase.Name;
                        }
                        else
                        {
                            unionCaseName = TypeTraitsUtility.GetTypeName(st);
                        }

                        results = results.Add(st, (unionCaseName, subInfo.Value));
                    }
                }

                if (results.Count > 0)
                {
                    return Option<RecordOrUnionInfo>.Some(new UnionOfDescendantsInfo(results));
                }
                else
                {
                    return Option<RecordOrUnionInfo>.None;
                }
            }
            else
            {
                return Option<RecordOrUnionInfo>.None;
            }
        }

        private static readonly ConditionalWeakTable<Type, Option<RecordOrUnionInfo>> recordInfoTable = new ConditionalWeakTable<Type, Option<RecordOrUnionInfo>>();

        public static Option<RecordOrUnionInfo> TryGetRecordInfo(this Type t)
        {
            return recordInfoTable.GetValue(t, GetRecordInfoInternal);
        }
    }
}