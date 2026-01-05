using Sunlighter.OptionLib;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using LinqExpression = System.Linq.Expressions.Expression;
using LinqLambdaExpression = System.Linq.Expressions.LambdaExpression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace Sunlighter.TypeTraitsLib.Building
{
    public enum ArtifactType
    {
        TypeTraits,
        Adapter,
        Setter,
        SpecialTypeTraits,
    }

    public abstract class ArtifactKey
    {
        private sealed class TypeBasedArtifactKey : ArtifactKey
        {
            private readonly ArtifactType artifactType;
            private readonly Type type;

            public TypeBasedArtifactKey(ArtifactType artifactType, Type type)
            {
                this.artifactType = artifactType;
                this.type = type;
            }

            public override ArtifactType ArtifactType => artifactType;

            public override Type Type => type;

            public override string Field => throw new InvalidOperationException("Artifact type {artifactType} doesn't have a Field property");

            public override Type HostingType => throw new InvalidOperationException("Artifact type {artifactType} doesn't have a HostingType property");

            public override string ToString()
            {
                return $"{artifactType}({TypeTraitsUtility.GetTypeName(type)})";
            }
        }

        private sealed class TypeAndFieldBasedArtifactKey : ArtifactKey
        {
            private readonly ArtifactType artifactType;
            private readonly Type type;
            private readonly string theField;

            public TypeAndFieldBasedArtifactKey(ArtifactType artifactType, Type type, string field)
            {
                this.artifactType = artifactType;
                this.type = type;
                theField = field;
            }

            public override ArtifactType ArtifactType => artifactType;

            public override Type Type => type;

            public override string Field => theField;

            public override Type HostingType => throw new InvalidOperationException("Artifact type {artifactType} doesn't have a HostingType property");

            public override string ToString()
            {
                return $"{artifactType}({TypeTraitsUtility.GetTypeName(type)}, {theField})";
            }
        }

        private sealed class HostedArtifactKey : ArtifactKey
        {
            private readonly ArtifactType artifactType;
            private readonly Type type;
            private readonly Type hostingType;

            public HostedArtifactKey(ArtifactType artifactType, Type type, Type hostingType)
            {
                this.artifactType = artifactType;
                this.type = type;
                this.hostingType = hostingType;
            }

            public override ArtifactType ArtifactType => artifactType;
            public override Type Type => type;
            public override string Field => throw new InvalidOperationException("Artifact type {artifactType} doesn't have a Field property");
            public override Type HostingType => hostingType;

            public override string ToString()
            {
                return $"{artifactType}({TypeTraitsUtility.GetTypeName(type)}, hosted by {TypeTraitsUtility.GetTypeName(hostingType)})";
            }
        }

        public static ArtifactKey Create(ArtifactType artifactType, Type type)
        {
            if (artifactType == ArtifactType.Setter)
            {
                throw new InvalidOperationException("Setter requires a binding variable");
            }
            else if (artifactType == ArtifactType.SpecialTypeTraits)
            {
                throw new InvalidOperationException("Special type traits requires a hosting type");
            }
            else
            {
                return new TypeBasedArtifactKey(artifactType, type);
            }
        }

        public static ArtifactKey Create(ArtifactType artifactType, Type type, string field)
        {
            if (artifactType != ArtifactType.Setter)
            {
                throw new InvalidOperationException("Field can only be supplied for a setter");
            }
            else
            {
                return new TypeAndFieldBasedArtifactKey(artifactType, type, field);
            }
        }

        public static ArtifactKey Create(ArtifactType artifactType, Type type, Type hostingType)
        {
            if (artifactType != ArtifactType.SpecialTypeTraits)
            {
                throw new InvalidOperationException("Hosting type can only be supplied for a special type traits");
            }
            else
            {
                return new HostedArtifactKey(artifactType, type, hostingType);
            }
        }

        public abstract ArtifactType ArtifactType { get; }

        public abstract Type Type { get; }

        public abstract string Field { get; }

        public abstract Type HostingType { get; }

        private static readonly Lazy<ITypeTraits<ArtifactKey>> typeTraits = new Lazy<ITypeTraits<ArtifactKey>>(GetTypeTraits, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ITypeTraits<ArtifactKey> GetTypeTraits()
        {
            ITypeTraits<ArtifactType> artifactTypeTraits = new ConvertTypeTraits<ArtifactType, int>
            (
                at => (int)at,
                Int32TypeTraits.Value,
                i => (ArtifactType)i
            );

            return new UnionTypeTraits<string, ArtifactKey>
            (
                StringTypeTraits.Value,
                ImmutableList<IUnionCaseTypeTraits<string, ArtifactKey>>.Empty.Add
                (
                    new UnionCaseTypeTraits2<string, ArtifactKey, TypeBasedArtifactKey>
                    (
                        "type-based",
                        new ConvertTypeTraits<TypeBasedArtifactKey, ValueTuple<ArtifactType, Type>>
                        (
                            a => (a.ArtifactType, a.Type),
                            new ValueTupleTypeTraits<ArtifactType, Type>
                            (
                                artifactTypeTraits,
                                TypeTypeTraits.Value
                            ),
                            vt => new TypeBasedArtifactKey(vt.Item1, vt.Item2)
                        )
                    )
                )
                .Add
                (
                    new UnionCaseTypeTraits2<string, ArtifactKey, TypeAndFieldBasedArtifactKey>
                    (
                        "type-and-field-based",
                        new ConvertTypeTraits<TypeAndFieldBasedArtifactKey, ValueTuple<ArtifactType, Type, string>>
                        (
                            a => (a.ArtifactType, a.Type, a.Field),
                            new ValueTupleTypeTraits<ArtifactType, Type, string>
                            (
                                artifactTypeTraits,
                                TypeTypeTraits.Value,
                                StringTypeTraits.Value
                            ),
                            vt => new TypeAndFieldBasedArtifactKey(vt.Item1, vt.Item2, vt.Item3)
                        )
                    )
                )
                .Add
                (
                    new UnionCaseTypeTraits2<string, ArtifactKey, HostedArtifactKey>
                    (
                        "hosted",
                        new ConvertTypeTraits<HostedArtifactKey, ValueTuple<ArtifactType, Type, Type>>
                        (
                            a => (a.ArtifactType, a.Type, a.HostingType),
                            new ValueTupleTypeTraits<ArtifactType, Type, Type>
                            (
                                artifactTypeTraits,
                                TypeTypeTraits.Value,
                                TypeTypeTraits.Value
                            ),
                            vt => new HostedArtifactKey(vt.Item1, vt.Item2, vt.Item3)
                        )
                    )
                )
            );
        }

        public static ITypeTraits<ArtifactKey> TypeTraits => typeTraits.Value;

        private static readonly Lazy<Adapter<ArtifactKey>> adapter = new Lazy<Adapter<ArtifactKey>>(() => Adapter<ArtifactKey>.Create(typeTraits.Value), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Adapter<ArtifactKey> Adapter => adapter.Value;

#if NETSTANDARD2_0
        public override bool Equals(object obj)
#else
        public override bool Equals(object? obj)
#endif
        {
            if (obj is ArtifactKey ak)
            {
                return typeTraits.Value.Compare(this, ak) == 0;
            }
            else return false;
        }

        public override int GetHashCode()
        {
            return typeTraits.Value.GetBasicHashCode(this);
        }
    }

    public sealed partial class Builder
    {
        private static readonly Lazy<Builder> instance = new Lazy<Builder>(() => new Builder(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Builder Instance => instance.Value;

        private readonly object syncRoot;

        private Builder()
        {
            syncRoot = new object();
            artifacts = GetInitialArtifacts();
            failedArtifacts = ImmutableSortedDictionary<ArtifactKey, Exception>.Empty.WithComparers(ArtifactKey.Adapter);
        }

        private ImmutableSortedDictionary<ArtifactKey, object> artifacts;
        private ImmutableSortedDictionary<ArtifactKey, Exception> failedArtifacts;

        private static ImmutableSortedDictionary<ArtifactKey, object> GetInitialArtifacts()
        {
            ImmutableSortedDictionary<ArtifactKey, object> result = ImmutableSortedDictionary<ArtifactKey, object>.Empty.WithComparers(ArtifactKey.Adapter);

            #region Add Initial Compare Workers

            foreach (Type t in typeof(ITypeTraits<>).Assembly.GetTypes())
            {
                if (t == typeof(Builder)) continue;

                Option<Type> typeTraitsTypeOption = t.TryGetCompareWorkerType();

                if (typeTraitsTypeOption.HasValue)
                {
                    Type typeTraitsArg = typeTraitsTypeOption.Value;

                    Type typeTraitsType = typeof(ITypeTraits<>).MakeGenericType(typeTraitsArg);

                    ImmutableList<ValueTuple<PropertyInfo, MethodInfo>> properties = t
                        .GetProperties()
                        .Where(pi => pi.CanRead)
                        .Select<PropertyInfo, ValueTuple<PropertyInfo, MethodInfo>>(pi => (pi, pi.GetGetMethod().AssertNotNull()))
                        .Where(t2 => t2.Item2.IsPublic && t2.Item2.IsStatic && typeTraitsType.IsAssignableFrom(t2.Item2.ReturnType))
                        .ToImmutableList();

                    if (properties.Count == 1)
                    {
                        object typeTraits = properties[0].Item2.Invoke(null, null).AssertNotNull();
                        result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeTraitsArg), typeTraits);
                    }
                }
            }

            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(Assembly)), AssemblyTypeTraits.Value);
            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(Type)), TypeTypeTraits.Value);
            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(DateTime)), DateTimeTypeTraits.Value);
            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(Guid)), GuidTypeTraits.Value);
            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(float)), SingleTypeTraits.Value);
            result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(double)), DoubleTypeTraits.Value);

            #endregion

            return result;
        }

        private class ArtifactBuilderServices : IAbstractBuilderServices<ArtifactKey, object>
        {
            private static readonly Lazy<ArtifactBuilderServices> value = new Lazy<ArtifactBuilderServices>(() => new ArtifactBuilderServices(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ArtifactBuilderServices() { }

            public static ArtifactBuilderServices Value => value.Value;

            public object CreateFixup(ArtifactKey key)
            {
                if (key.ArtifactType == ArtifactType.TypeTraits)
                {
                    Type t = typeof(RecursiveTypeTraits<>).MakeGenericType(key.Type);
                    ConstructorInfo ci = t.GetRequiredConstructor(Type.EmptyTypes);
                    return ci.Invoke(null);
                }
                else
                {
                    throw new NotSupportedException($"Fixup not supported for ArtifactType {key.ArtifactType}");
                }
            }

            public Exception NewException_CannotBuild(ImmutableList<IBuildRule<ArtifactKey, object>> rules, ImmutableSortedDictionary<ArtifactKey, ImmutableSortedSet<int>> unbuildables)
            {
                return new BuilderException($"Cannot build: {string.Join(", ", unbuildables.Keys.Select(t => t.ToString()))}");
            }

            public Exception NewException_CannotBuild(ImmutableSortedSet<ArtifactKey> unbuildables)
            {
                return new BuilderException($"Cannot build: {string.Join(", ", unbuildables.Select(t => t.ToString()))}");
            }

            public void SetFixup(ArtifactKey key, object fixup, object value)
            {
                if (key.ArtifactType == ArtifactType.TypeTraits)
                {
                    Type t = typeof(RecursiveTypeTraits<>).MakeGenericType(key.Type);
                    Type i = typeof(ITypeTraits<>).MakeGenericType(key.Type);
                    MethodInfo mi = t.GetMethods()
                        .Where(mi2 => mi2.IsPublic && !mi2.IsStatic && mi2.Name == "Set")
                        .Select(mi3 => Tuple.Create(mi3, mi3.GetParameters()))
                        .Where(tu => tu.Item2.Length == 1 && tu.Item2[0].ParameterType == i)
                        .Single().Item1;

                    mi.Invoke(fixup, new object[] { value });
                }
                else
                {
                    throw new NotSupportedException($"Fixup not supported for ArtifactType {key.ArtifactType}");
                }
            }
        }

        /// <summary>
        /// Now supports generic classes.
        /// </summary>
        private class ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule> value =
                new Lazy<ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule>(() => new ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule() { }

            public static ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule Value => value.Value;

            private static Option<PropertyInfo> TryGetTypeTraitsProperty(Type t)
            {
                Type typeTraitsType = typeof(ITypeTraits<>).MakeGenericType(t);

                if (t.IsGenericTypeDefinition) return Option<PropertyInfo>.None;

                ImmutableList<PropertyInfo> piArray = t.GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Where(p => p.PropertyType == typeTraitsType).Take(2).ToImmutableList();

                if (piArray.Count == 1)
                {
                    return Option<PropertyInfo>.Some(piArray[0]);
                }
                else
                {
                    return Option<PropertyInfo>.None;
                }
            }

            private static Option<MethodInfo> TryGetTypeTraitsMethod(Type t)
            {
                if (t.IsGenericType)
                {
                    Type[] genericArgs = t.GetGenericArguments();
                    Type[] methodArgs = genericArgs.Select(g => typeof(ITypeTraits<>).MakeGenericType(g)).ToArray();

                    ImmutableList<MethodInfo> miArray = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(methodArgs))
                        .Take(2)
                        .ToImmutableList();

                    if (miArray.Count == 1)
                    {
                        return Option<MethodInfo>.Some(miArray[0]);
                    }
                    else
                    {
                        return Option<MethodInfo>.None;
                    }
                }
                else
                {
                    return Option<MethodInfo>.None;
                }
            }

            public bool CanBuild(ArtifactKey k)
            {
                if (k.ArtifactType != ArtifactType.TypeTraits) return false;
                if (k.Type.GetCustomAttribute<ProvidesOwnTypeTraitsAttribute>() == null) return false;

                if (k.Type.IsGenericType)
                {
                    return TryGetTypeTraitsMethod(k.Type).HasValue;
                }
                else
                {
                    return TryGetTypeTraitsProperty(k.Type).HasValue;
                }
            }

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                ImmutableSortedSet<ArtifactKey> results = ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);
                if (k.Type.IsGenericType)
                {
                    Type[] genericArgs = k.Type.GetGenericArguments();
                    foreach (Type ga in genericArgs)
                    {
                        results = results.Add(ArtifactKey.Create(ArtifactType.TypeTraits, ga));
                    }
                }
                return results;
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                if (k.Type.IsGenericType)
                {
                    Type[] genericArgs = k.Type.GetGenericArguments();
                    Option<MethodInfo> oCreateMethod = TryGetTypeTraitsMethod(k.Type);
                    MethodInfo createMethod = oCreateMethod.Value;

                    object prerequisiteValue(Type traitsType)
                    {
                        if (traitsType.IsGenericType && traitsType.GetGenericTypeDefinition() == typeof(ITypeTraits<>))
                        {
                            var x = traitsType.GetGenericArguments()[0];
                            ArtifactKey prereqKey = ArtifactKey.Create(ArtifactType.TypeTraits, x);
                            return prerequisites[prereqKey];
                        }
                        else throw new InvalidOperationException("Unexpected prerequisite type " + TypeTraitsUtility.GetTypeName(traitsType));
                    }

                    object[] methodArgs = createMethod.GetParameters().Select(p => prerequisiteValue(p.ParameterType)).ToArray();
                    return createMethod.Invoke(null, methodArgs).AssertNotNull();
                }
                else
                {
                    Option<PropertyInfo> oTypeTraitsProperty = TryGetTypeTraitsProperty(k.Type);
                    PropertyInfo typeTraitsProperty = oTypeTraitsProperty.Value;
                    return typeTraitsProperty.GetValue(null).AssertNotNull();
                }
            }
        }

        /// <summary>
        /// Currently does not support generic classes.
        /// </summary>
        private class ProvidesOwnAdapter_AdapterBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ProvidesOwnAdapter_AdapterBuilder_Rule> value =
                new Lazy<ProvidesOwnAdapter_AdapterBuilder_Rule>(() => new ProvidesOwnAdapter_AdapterBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ProvidesOwnAdapter_AdapterBuilder_Rule() { }

            public static ProvidesOwnAdapter_AdapterBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.GetCustomAttribute<ProvidesOwnAdapterAttribute>() != null;

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type desiredType = typeof(Adapter<>).MakeGenericType(k.Type);
#if NETSTANDARD2_0
                MethodInfo getMethod = k.Type.GetProperties(BindingFlags.Public | BindingFlags.Static)
#else
                MethodInfo? getMethod = k.Type.GetProperties(BindingFlags.Public | BindingFlags.Static)
#endif
                    .Where(pi => pi.Name == "Adapter" && pi.CanRead && pi.PropertyType == desiredType)
                    .Select(pi => pi.GetGetMethod().AssertNotNull())
                    .FirstOrDefault();

                if (getMethod == null) throw new BuilderException("Adapter property not found on type " + TypeTraitsUtility.GetTypeName(k.Type));
                else
                {
                    return getMethod.Invoke(null, null).AssertNotNull();
                }
            }
        }

        private class GensymInt32_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<GensymInt32_TypeTraitsBuilder_Rule> value =
                new Lazy<GensymInt32_TypeTraitsBuilder_Rule>(() => new GensymInt32_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private GensymInt32_TypeTraitsBuilder_Rule() { }

            public static GensymInt32_TypeTraitsBuilder_Rule Value => value.Value;

            private static Option<PropertyInfo> TryGetIdProperty(Type t)
            {
                ImmutableList<PropertyInfo> piArray = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name == "ID").Take(2).ToImmutableList();

                if (piArray.Count == 1)
                {
                    return Option<PropertyInfo>.Some(piArray[0]);
                }
                else
                {
                    return Option<PropertyInfo>.None;
                }
            }

            private static Option<ConstructorInfo> TryGetDefaultConstructor(Type t)
            {
#if !NETSTANDARD2_0
                ConstructorInfo? ci = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                if (ci is not null)
#else
                ConstructorInfo ci = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                if (ci != null)
#endif
                {
                    return Option<ConstructorInfo>.Some(ci);
                }
                else
                {
                    return Option<ConstructorInfo>.None;
                }
            }

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits
                && k.Type.GetCustomAttribute<GensymInt32Attribute>() != null
                && TryGetIdProperty(k.Type).HasValue
                && TryGetDefaultConstructor(k.Type).HasValue;

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                ImmutableSortedSet<ArtifactKey> prereqs = ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

                prereqs = prereqs.Add(ArtifactKey.Create(ArtifactType.TypeTraits, TryGetIdProperty(k.Type).Value.PropertyType));

                return prereqs;
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                PropertyInfo idProperty = TryGetIdProperty(k.Type).Value;
                Type idType = idProperty.PropertyType;
                Type typeTraitsType = typeof(ITypeTraits<>).MakeGenericType(k.Type);
                Type idTypeTraitsType = typeof(ITypeTraits<>).MakeGenericType(idType);
                object idTypeTraits = prerequisites[ArtifactKey.Create(ArtifactType.TypeTraits, idType)];
                Type resultType = typeof(GensymTypeTraits<,,>).MakeGenericType(new Type[] { k.Type, idType, typeof(int) });

                Type getIdFuncType = typeof(Func<,>).MakeGenericType(k.Type, idType);

                ParameterExpression pGetIdSym = LinqExpression.Parameter(k.Type, "sym");

                Type gensymFuncType = typeof(Func<>).MakeGenericType(k.Type);

                Func<int, int> incrementFunc = x => x + 1;

                object getIdFunc = Expression.Lambda
                (
                    getIdFuncType,
                    LinqExpression.Property
                    (
                        pGetIdSym,
                        idProperty
                    ),
                    true,
                    pGetIdSym
                )
                .Compile();

                ConstructorInfo cInfo = TryGetDefaultConstructor(k.Type).Value;

                object gensymFunc = Expression.Lambda
                (
                    gensymFuncType,
                    LinqExpression.New
                    (
                        cInfo
                    ),
                    true
                )
                .Compile();

                ConstructorInfo gensymTypeTraitsCi = resultType.GetRequiredConstructor
                (
                    new Type[]
                    {
                        getIdFuncType, gensymFuncType, idTypeTraitsType, typeof(ITypeTraits<int>), typeof(int), typeof(Func<int, int>)
                    }
                );

                return gensymTypeTraitsCi.Invoke(new object[] { getIdFunc, gensymFunc, idTypeTraits, Int32TypeTraits.Value, 0, incrementFunc });
            }
        }

        private class SpecialTypeTraits_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<SpecialTypeTraits_TypeTraitsBuilder_Rule> value =
                new Lazy<SpecialTypeTraits_TypeTraitsBuilder_Rule>(() => new SpecialTypeTraits_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private SpecialTypeTraits_TypeTraitsBuilder_Rule() { }

            public static SpecialTypeTraits_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.SpecialTypeTraits;

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type desiredType = typeof(ITypeTraits<>).MakeGenericType(k.Type);
#if NETSTANDARD2_0
                MethodInfo getMethod = k.HostingType.GetProperties(BindingFlags.Public | BindingFlags.Static)
#else
                MethodInfo? getMethod = k.HostingType.GetProperties(BindingFlags.Public | BindingFlags.Static)
#endif
                    .Where(pi => pi.CanRead && pi.PropertyType == desiredType)
                    .Select(pi => pi.GetGetMethod().AssertNotNull())
                    .SingleOrDefault();

                if (getMethod == null) throw new BuilderException($"TypeTraits property (for {TypeTraitsUtility.GetTypeName(k.Type)}) not found on type " + TypeTraitsUtility.GetTypeName(k.HostingType));
                else
                {
                    return getMethod.Invoke(null, null).AssertNotNull();
                }
            }
        }

        private class Enum_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Enum_TypeTraitsBuilder_Rule> value = new Lazy<Enum_TypeTraitsBuilder_Rule>(() => new Enum_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Enum_TypeTraitsBuilder_Rule() { }

            public static Enum_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsEnum;

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) =>
                ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter).Add(ArtifactKey.Create(ArtifactType.TypeTraits, k.Type.GetEnumUnderlyingType()));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type integralType = k.Type.GetEnumUnderlyingType();
                Type typeTraitsType = typeof(ConvertTypeTraits<,>).MakeGenericType(k.Type, integralType);
                Type convertFuncType = typeof(Func<,>).MakeGenericType(k.Type, integralType);
                ParameterExpression pEnum = LinqExpression.Parameter(k.Type, "v");
                LinqLambdaExpression convertFuncExpr = LinqExpression.Lambda
                (
                    convertFuncType,
                    LinqExpression.Convert
                    (
                        pEnum,
                        integralType
                    ),
                    true,
                    pEnum
                );
                Delegate convertFunc = convertFuncExpr.Compile();
                Type itemWorkerType = typeof(ITypeTraits<>).MakeGenericType(integralType);
                Type convertBackFuncType = typeof(Func<,>).MakeGenericType(integralType, k.Type);
                ParameterExpression pIntegral = LinqExpression.Parameter(integralType, "i");
                LinqLambdaExpression convertBackFuncExpr = LinqExpression.Lambda
                (
                    convertBackFuncType,
                    LinqExpression.Convert
                    (
                        pIntegral,
                        k.Type
                    ),
                    true,
                    pIntegral
                );
                Delegate convertBackFunc = convertBackFuncExpr.Compile();
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { convertFuncType, itemWorkerType, convertBackFuncType });
                return typeTraitsCi.Invoke(new object[] { convertFunc, prerequisites[ArtifactKey.Create(ArtifactType.TypeTraits, integralType)], convertBackFunc });
            }
        }

        private class Tuple2_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Tuple2_TypeTraitsBuilder_Rule> value =
                new Lazy<Tuple2_TypeTraitsBuilder_Rule>(() => new Tuple2_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Tuple2_TypeTraitsBuilder_Rule() { }

            public static Tuple2_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(Tuple<,>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Union(k.Type.GetGenericArguments().Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type keyType = k.Type;
                Type item1Type = keyType.GetGenericArguments()[0];
                Type item2Type = keyType.GetGenericArguments()[1];
                ArtifactKey item1Key = ArtifactKey.Create(ArtifactType.TypeTraits, item1Type);
                ArtifactKey item2Key = ArtifactKey.Create(ArtifactType.TypeTraits, item2Type);
                Type typeTraitsType = typeof(TupleTypeTraits<,>).MakeGenericType(item1Type, item2Type);
                Type item1InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item1Type);
                Type item2InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item2Type);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { item1InterfaceType, item2InterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[item1Key], prerequisites[item2Key] });
            }
        }

        private class ValueTuple2_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ValueTuple2_TypeTraitsBuilder_Rule> value =
                new Lazy<ValueTuple2_TypeTraitsBuilder_Rule>(() => new ValueTuple2_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ValueTuple2_TypeTraitsBuilder_Rule() { }

            public static ValueTuple2_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(ValueTuple<,>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Union(k.Type.GetGenericArguments().Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type keyType = k.Type;
                Type item1Type = keyType.GetGenericArguments()[0];
                Type item2Type = keyType.GetGenericArguments()[1];
                ArtifactKey item1Key = ArtifactKey.Create(ArtifactType.TypeTraits, item1Type);
                ArtifactKey item2Key = ArtifactKey.Create(ArtifactType.TypeTraits, item2Type);
                Type typeTraitsType = typeof(ValueTupleTypeTraits<,>).MakeGenericType(item1Type, item2Type);
                Type item1InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item1Type);
                Type item2InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item2Type);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { item1InterfaceType, item2InterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[item1Key], prerequisites[item2Key] });
            }
        }

        private class Tuple3_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Tuple3_TypeTraitsBuilder_Rule> value =
                new Lazy<Tuple3_TypeTraitsBuilder_Rule>(() => new Tuple3_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Tuple3_TypeTraitsBuilder_Rule() { }

            public static Tuple3_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(Tuple<,,>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Union(k.Type.GetGenericArguments().Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type keyType = k.Type;
                Type item1Type = keyType.GetGenericArguments()[0];
                Type item2Type = keyType.GetGenericArguments()[1];
                Type item3Type = keyType.GetGenericArguments()[2];
                ArtifactKey item1Key = ArtifactKey.Create(ArtifactType.TypeTraits, item1Type);
                ArtifactKey item2Key = ArtifactKey.Create(ArtifactType.TypeTraits, item2Type);
                ArtifactKey item3Key = ArtifactKey.Create(ArtifactType.TypeTraits, item3Type);
                Type typeTraitsType = typeof(TupleTypeTraits<,,>).MakeGenericType(item1Type, item2Type, item3Type);
                Type item1InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item1Type);
                Type item2InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item2Type);
                Type item3InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item3Type);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { item1InterfaceType, item2InterfaceType, item3InterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[item1Key], prerequisites[item2Key], prerequisites[item3Key] });
            }
        }

        private class ValueTuple3_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ValueTuple3_TypeTraitsBuilder_Rule> value =
                new Lazy<ValueTuple3_TypeTraitsBuilder_Rule>(() => new ValueTuple3_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ValueTuple3_TypeTraitsBuilder_Rule() { }

            public static ValueTuple3_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(ValueTuple<,,>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Union(k.Type.GetGenericArguments().Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type keyType = k.Type;
                Type item1Type = keyType.GetGenericArguments()[0];
                Type item2Type = keyType.GetGenericArguments()[1];
                Type item3Type = keyType.GetGenericArguments()[2];
                ArtifactKey item1Key = ArtifactKey.Create(ArtifactType.TypeTraits, item1Type);
                ArtifactKey item2Key = ArtifactKey.Create(ArtifactType.TypeTraits, item2Type);
                ArtifactKey item3Key = ArtifactKey.Create(ArtifactType.TypeTraits, item3Type);
                Type typeTraitsType = typeof(ValueTupleTypeTraits<,,>).MakeGenericType(item1Type, item2Type, item3Type);
                Type item1InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item1Type);
                Type item2InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item2Type);
                Type item3InterfaceType = typeof(ITypeTraits<>).MakeGenericType(item3Type);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { item1InterfaceType, item2InterfaceType, item3InterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[item1Key], prerequisites[item2Key], prerequisites[item3Key] });
            }
        }


        private class Option_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Option_TypeTraitsBuilder_Rule> value =
                new Lazy<Option_TypeTraitsBuilder_Rule>(() => new Option_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Option_TypeTraitsBuilder_Rule() { }

            public static Option_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(Option<>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Add(ArtifactKey.Create(ArtifactType.TypeTraits, k.Type.GetGenericArguments()[0]));
            
            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type keyType = k.Type;
                Type itemType = keyType.GetGenericArguments()[0];
                ArtifactKey itemKey = ArtifactKey.Create(ArtifactType.TypeTraits, itemType);
                Type typeTraitsType = typeof(OptionTypeTraits<>).MakeGenericType(itemType);
                Type itemInterfaceType = typeof(ITypeTraits<>).MakeGenericType(itemType);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { itemInterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[itemKey] });
            }
        }

        private class StrongBox_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<StrongBox_TypeTraitsBuilder_Rule> value =
                new Lazy<StrongBox_TypeTraitsBuilder_Rule>(() => new StrongBox_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private StrongBox_TypeTraitsBuilder_Rule() { }

            public static StrongBox_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(StrongBox<>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Add(ArtifactKey.Create(ArtifactType.TypeTraits, k.Type.GetGenericArguments()[0]));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                ImmutableList<MethodInfo> miList = typeof(TypeTraitsUtility).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(mi2 => mi2.Name == "GetStrongBoxTraits" && mi2.IsGenericMethod)
                    .ToImmutableList();

                if (miList.Count != 1) throw new BuilderException($"Expected one GetStrongBoxTraits function, found {miList.Count}");

                MethodInfo mi = miList[0];

                Type keyType = k.Type;
                Type itemType = keyType.GetGenericArguments()[0];
                ArtifactKey itemKey = ArtifactKey.Create(ArtifactType.TypeTraits, itemType);
                Type itemInterfaceType = typeof(ITypeTraits<>).MakeGenericType(itemType);
                MethodInfo miWithType = mi.MakeGenericMethod(k.Type.GetGenericArguments()[0]);
                return miWithType.Invoke(null, new object[] { prerequisites[itemKey] }).AssertNotNull();
            }
        }

        private class ImmutableList_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ImmutableList_TypeTraitsBuilder_Rule> value =
                new Lazy<ImmutableList_TypeTraitsBuilder_Rule>(() => new ImmutableList_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ImmutableList_TypeTraitsBuilder_Rule() { }

            public static ImmutableList_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(ImmutableList<>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                var result = ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);
                Type itemType = k.Type.GetGenericArguments()[0];
                result = result.Add(ArtifactKey.Create(ArtifactType.TypeTraits, itemType));

                return result;
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type itemType = k.Type.GetGenericArguments()[0];
                ArtifactKey itemKey = ArtifactKey.Create(ArtifactType.TypeTraits, itemType);
                Type typeTraitsType = typeof(ListTypeTraits<>).MakeGenericType(itemType);
                Type itemInterfaceType = typeof(ITypeTraits<>).MakeGenericType(itemType);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { itemInterfaceType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[itemKey] });
            }
        }

        private class ImmutableSortedSet_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ImmutableSortedSet_TypeTraitsBuilder_Rule> value =
                new Lazy<ImmutableSortedSet_TypeTraitsBuilder_Rule>(() => new ImmutableSortedSet_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ImmutableSortedSet_TypeTraitsBuilder_Rule() { }

            public static ImmutableSortedSet_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(ImmutableSortedSet<>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Add(ArtifactKey.Create(ArtifactType.TypeTraits, k.Type.GetGenericArguments()[0]));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type itemType = k.Type.GetGenericArguments()[0];
                ArtifactKey itemKey = ArtifactKey.Create(ArtifactType.TypeTraits, itemType);
                Type itemTypeTraitsType = typeof(ITypeTraits<>).MakeGenericType(itemType);
                Type typeTraitsType = typeof(SetTypeTraits<>).MakeGenericType(itemType);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { itemTypeTraitsType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[itemKey] });
            }
        }

        private class ImmutableSortedDictionary_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ImmutableSortedDictionary_TypeTraitsBuilder_Rule> value =
                new Lazy<ImmutableSortedDictionary_TypeTraitsBuilder_Rule>(() => new ImmutableSortedDictionary_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ImmutableSortedDictionary_TypeTraitsBuilder_Rule() { }

            public static ImmutableSortedDictionary_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.TypeTraits && k.Type.IsGenericType && k.Type.GetGenericTypeDefinition() == typeof(ImmutableSortedDictionary<,>);

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Union(k.Type.GetGenericArguments().Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type kType = k.Type;
                Type keyType = kType.GetGenericArguments()[0];
                ArtifactKey keyKey = ArtifactKey.Create(ArtifactType.TypeTraits, keyType);
                Type keyTypeTraitsType = typeof(ITypeTraits<>).MakeGenericType(keyType);
                Type valueType = kType.GetGenericArguments()[1];
                ArtifactKey valueKey = ArtifactKey.Create(ArtifactType.TypeTraits, valueType);
                Type valueTypeTraitsType = typeof(ITypeTraits<>).MakeGenericType(valueType);
                Type typeTraitsType = typeof(DictionaryTypeTraits<,>).MakeGenericType(keyType, valueType);
                ConstructorInfo typeTraitsCi = typeTraitsType.GetRequiredConstructor(new Type[] { keyTypeTraitsType, valueTypeTraitsType });
                return typeTraitsCi.Invoke(new object[] { prerequisites[keyKey], prerequisites[valueKey] });
            }
        }

        private class AdapterBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<AdapterBuilder_Rule> value =
                new Lazy<AdapterBuilder_Rule>(() => new AdapterBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private AdapterBuilder_Rule() { }

            public static AdapterBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k) => k.ArtifactType == ArtifactType.Adapter;

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty
                .WithComparer(ArtifactKey.Adapter)
                .Add(ArtifactKey.Create(ArtifactType.TypeTraits, k.Type));

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                ArtifactKey typeTraitsKey = ArtifactKey.Create(ArtifactType.TypeTraits, k.Type);
                Type typeTraitsType = typeof(ITypeTraits<>).MakeGenericType(k.Type);
                Type adapterType = typeof(Adapter<>).MakeGenericType(k.Type);
                MethodInfo adapterCreate = adapterType.GetRequiredMethod("Create", BindingFlags.Public | BindingFlags.Static, new Type[] { typeTraitsType });
                return adapterCreate.Invoke(null, new object[] { prerequisites[typeTraitsKey] }).AssertNotNull();
            }
        }

        private static readonly Lazy<ImmutableList<IBuildRule<ArtifactKey, object>>> artifactBuildRules =
            new Lazy<ImmutableList<IBuildRule<ArtifactKey, object>>>(GetArtifactBuildRules, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableList<IBuildRule<ArtifactKey, object>> GetArtifactBuildRules()
        {
            return ImmutableList<IBuildRule<ArtifactKey, object>>.Empty
                .Add(ProvidesOwnTypeTraits_TypeTraitsBuilder_Rule.Value)
                .Add(ProvidesOwnAdapter_AdapterBuilder_Rule.Value)
                .Add(GensymInt32_TypeTraitsBuilder_Rule.Value)
                .Add(SpecialTypeTraits_TypeTraitsBuilder_Rule.Value)
                .Add(Enum_TypeTraitsBuilder_Rule.Value)
                .Add(Tuple2_TypeTraitsBuilder_Rule.Value)
                .Add(ValueTuple2_TypeTraitsBuilder_Rule.Value)
                .Add(Tuple3_TypeTraitsBuilder_Rule.Value)
                .Add(ValueTuple3_TypeTraitsBuilder_Rule.Value)
                .Add(Option_TypeTraitsBuilder_Rule.Value)
                .Add(StrongBox_TypeTraitsBuilder_Rule.Value)
                .Add(ImmutableList_TypeTraitsBuilder_Rule.Value)
                .Add(ImmutableSortedSet_TypeTraitsBuilder_Rule.Value)
                .Add(ImmutableSortedDictionary_TypeTraitsBuilder_Rule.Value)
                .Add(AdapterBuilder_Rule.Value)
                .Add(Reflected_TypeTraitsBuilder_Rule.Value)
                .Add(ReflectedSingleton_TypeTraitsBuilder_Rule.Value)
                .Add(ReflectedUnion_TypeTraitsBuilder_Rule.Value)
                .Add(Reflected_SetterBuilder_Rule.Value)
                ;
        }

        public ImmutableSortedDictionary<ArtifactKey, object> Artifacts => artifacts;

        public object GetArtifact(ArtifactKey key)
        {
            lock (syncRoot)
            {
                if (artifacts.ContainsKey(key))
                {
                    return artifacts[key];
                }
                else if (failedArtifacts.ContainsKey(key))
                {
                    throw failedArtifacts[key];
                }
                else
                {
                    try
                    {
                        artifacts = AbstractBuilder.Build2
                        (
                            ArtifactBuilderServices.Value,
                            artifacts,
                            artifactBuildRules.Value,
                            ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter).Add(key)
                        );

                        return artifacts[key];
                    }
                    catch(Exception exc)
                    {
                        failedArtifacts = failedArtifacts.Add(key, exc);
                        throw;
                    }
                }
            }
        }

        public object GetTypeTraits(Type t) => GetArtifact(ArtifactKey.Create(ArtifactType.TypeTraits, t));

        public ITypeTraits<T> GetTypeTraits<T>() => (ITypeTraits<T>)GetArtifact(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(T)));

        public ITypeTraits<T> GetSpecialTypeTraits<T>(Type hostingType) => (ITypeTraits<T>)GetArtifact(ArtifactKey.Create(ArtifactType.SpecialTypeTraits, typeof(T), hostingType));

        /// <summary>
        /// Add a custom ITypeTraits&lt;T&gt; implementation, which will supersede anything that would otherwise be built.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="typeTraits"></param>
        /// <returns></returns>
        public ITypeTraits<T> AddTypeTraits<T>(ITypeTraits<T> typeTraits)
        {
            artifacts = artifacts.Add(ArtifactKey.Create(ArtifactType.TypeTraits, typeof(T)), typeTraits);
            return typeTraits;
        }

        public object GetAdapter(Type t) => GetArtifact(ArtifactKey.Create(ArtifactType.Adapter, t));

        public Adapter<T> GetAdapter<T>() => (Adapter<T>)GetArtifact(ArtifactKey.Create(ArtifactType.Adapter, typeof(T)));
    }

    public static partial class Extensions
    {
        public static string UpTo(this string str, Regex r)
        {
            Match m = r.Match(str);
            if (m.Success)
            {
                return str.Substring(0, m.Index);
            }
            else
            {
                return str;
            }
        }

        public static string UpTo(this string str, char ch)
        {
            if (str is null) throw new ArgumentNullException(nameof(str));

            int i = str.IndexOf(ch);
            if (i >= 0)
            {
                return str.Substring(0, i);
            }
            else return str;
        }

        public static Option<Type> TryGetCompareWorkerType(this Type t)
        {
            ImmutableList<Type> interfaces = t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypeTraits<>)).ToImmutableList();

            if (interfaces.Count == 1)
            {
                return Option<Type>.Some(interfaces[0].GetGenericArguments()[0]);
            }
            else
            {
                return Option<Type>.None;
            }
        }

        public static ImmutableSortedDictionary<K, V3> OuterJoin<K, V1, V2, V3> 
        (
            this ImmutableSortedDictionary<K, V1> left,
            ImmutableSortedDictionary<K, V2> right,
            Func<K, Option<V1>, Option<V2>, V3> func
        )
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            ImmutableSortedSet<K> keySet = ImmutableSortedSet<K>.Empty
                .WithComparer(left.KeyComparer)
                .Union(left.Keys)
                .Union(right.Keys);

            ImmutableSortedDictionary<K, V3>.Builder result = ImmutableSortedDictionary<K, V3>.Empty
                .WithComparers(left.KeyComparer)
                .ToBuilder();

            foreach(K key in keySet)
            {
                Option<V1> leftValue = left.ContainsKey(key) ? Option<V1>.Some(left[key]) : Option<V1>.None;
                Option<V2> rightValue = right.ContainsKey(key) ? Option<V2>.Some(right[key]) : Option<V2>.None;

                result.Add(key, func(key, leftValue, rightValue));
            }

            return result.ToImmutable();
        }
    }
}
