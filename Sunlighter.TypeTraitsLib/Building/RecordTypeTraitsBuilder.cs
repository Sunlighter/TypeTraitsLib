using Sunlighter.OptionLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using LinqExpression = System.Linq.Expressions.Expression;
using LinqLambdaExpression = System.Linq.Expressions.LambdaExpression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace Sunlighter.TypeTraitsLib.Building
{
    public sealed partial class Builder
    {
        private static LinqExpression AndAlsoAll(ImmutableList<LinqExpression> conditions)
        {
            if (conditions.IsEmpty)
            {
                return LinqExpression.Constant(true);
            }
            else if (conditions.Count == 1)
            {
                return conditions[0];
            }
            else
            {
                LinqExpression result = conditions[conditions.Count - 1];
                conditions = conditions.RemoveAt(conditions.Count - 1);
                while(conditions.Count > 0)
                {
                    result = LinqExpression.AndAlso(conditions[conditions.Count - 1], result);
                    conditions = conditions.RemoveAt(conditions.Count - 1);
                }
                return result;
            }
        }

        private static LinqExpression PropertyOrField(LinqExpression bearer, MemberInfo mi)
        {
            if (mi is PropertyInfo pi)
            {
                return LinqExpression.Property(bearer, pi);
            }
            else if (mi is FieldInfo fi)
            {
                return LinqExpression.Field(bearer, fi);
            }
            else
            {
                throw new InvalidOperationException($"Member {mi.Name} is not a property or theField");
            }
        }

        private static object BuildCompareFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits) // returns Func<T, T, int>
        {
            Type compareFuncType = typeof(Func<,,>).MakeGenericType(t, t, typeof(int));

            ParameterExpression pA = LinqExpression.Parameter(t, "a");
            ParameterExpression pB = LinqExpression.Parameter(t, "b");

            ParameterExpression vTemp = LinqExpression.Variable(typeof(int), "temp");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getCompareMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "Compare",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { bindingType, bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> compareMethod =
                info.ConstructorBindings.Select(getCompareMethod).ToImmutableSortedDictionary();

            int i = info.ConstructorBindings.Count;

            Option<LinqExpression> comparisons = Option<LinqExpression>.None;

            while (i > 0)
            {
                --i;

                string s = info.ConstructorBindings[i];

                if (comparisons.HasValue)
                {
                    comparisons = Option<LinqExpression>.Some
                    (
                        LinqExpression.Block
                        (
                            LinqExpression.Assign
                            (
                                vTemp,
                                LinqExpression.Call
                                (
                                    LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                    compareMethod[s],
                                    PropertyOrField(pA, info.PropertyBindings[s]),
                                    PropertyOrField(pB, info.PropertyBindings[s])
                                )
                            ),
                            LinqExpression.IfThen
                            (
                                LinqExpression.Equal
                                (
                                    vTemp,
                                    LinqExpression.Constant(0)
                                ),
                                comparisons.Value
                            )
                        )
                    );
                }
                else
                {
                    comparisons = Option<LinqExpression>.Some
                    (
                        LinqExpression.Assign
                        (
                            vTemp,
                            LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                compareMethod[s],
                                PropertyOrField(pA, info.PropertyBindings[s]),
                                PropertyOrField(pB, info.PropertyBindings[s])
                            )
                        )
                    );
                }
            }

            LinqLambdaExpression compareFuncExpr = LinqExpression.Lambda
            (
                compareFuncType,
                LinqExpression.Block
                (
                    ImmutableList<ParameterExpression>.Empty
                    .Add(vTemp)
                    .ToArray(),
                    ImmutableList<LinqExpression>.Empty
                    .Add(LinqExpression.Assign(vTemp, LinqExpression.Constant(0)))
                    .AddIfAny(comparisons)
                    .Add(vTemp)
                ),
                true,
                new ParameterExpression[] { pA, pB }
            );

            return compareFuncExpr.Compile();
        }

        private static object BuildAddToHashFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits) // returns Action<HashBuilder, T>
        {
            Type addToHashFuncType = typeof(Action<,>).MakeGenericType(typeof(HashBuilder), t);

            ParameterExpression pHashBuilder = LinqExpression.Parameter(typeof(HashBuilder), "hb");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getAddToHashMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "AddToHash",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(HashBuilder), bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> addToHashMethod =
                info.ConstructorBindings.Select(getAddToHashMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression addToHashFuncExpr = LinqExpression.Lambda
            (
                addToHashFuncType,
                LinqExpression.Block
                (
                    ImmutableList<LinqExpression>.Empty
                    .AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                addToHashMethod[s],
                                pHashBuilder,
                                PropertyOrField(pA, info.PropertyBindings[s])
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pHashBuilder, pA }
            );

            return addToHashFuncExpr.Compile();
        }

        private static object BuildCheckAnalogousFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type checkAnalogousFuncType = typeof(Action<,,>).MakeGenericType(typeof(AnalogyTracker), t, t);

            ParameterExpression pTracker = LinqExpression.Parameter(typeof(AnalogyTracker), "tracker");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");
            ParameterExpression pB = LinqExpression.Parameter(t, "b");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getCheckAnalogousMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "CheckAnalogous",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(AnalogyTracker), bindingType, bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> checkAnalogousMethod =
                info.ConstructorBindings.Select(getCheckAnalogousMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression checkAnalogousFuncExpr = LinqExpression.Lambda
            (
                checkAnalogousFuncType,
                LinqExpression.IfThen
                (
                    LinqExpression.Property
                    (
                        pTracker,
                        nameof(AnalogyTracker.IsAnalogous)
                    ),
                    LinqExpression.Block
                    (
                        ImmutableList<LinqExpression>.Empty
                        .AddRange
                        (
                            info.ConstructorBindings.Select
                            (
                                s => LinqExpression.Call
                                (
                                    LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                    checkAnalogousMethod[s],
                                    pTracker,
                                    PropertyOrField(pA, info.PropertyBindings[s]),
                                    PropertyOrField(pB, info.PropertyBindings[s])
                                )
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pTracker, pA, pB }
            );

            return checkAnalogousFuncExpr.Compile();
        }

        private static object BuildCanSerializeFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type canSerializeFuncType = typeof(Func<,>).MakeGenericType(t, typeof(bool));

            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getCanSerializeMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "CanSerialize",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> canSerializeMethod =
                info.ConstructorBindings.Select(getCanSerializeMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression canSerializeFuncExpr = LinqExpression.Lambda
            (
                canSerializeFuncType,
                AndAlsoAll
                (
                    ImmutableList<LinqExpression>.Empty.AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                canSerializeMethod[s],
                                PropertyOrField(pA, info.PropertyBindings[s])
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pA }
            );

            return canSerializeFuncExpr.Compile();
        }

        private static object BuildSerializeFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type serializeFuncType = typeof(Action<,>).MakeGenericType(typeof(Serializer), t);

            ParameterExpression pSerializer = LinqExpression.Parameter(typeof(Serializer), "dest");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getSerializeMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "Serialize",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(Serializer), bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> serializeMethod =
                info.ConstructorBindings.Select(getSerializeMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression serializeFuncExpr = LinqExpression.Lambda
            (
                serializeFuncType,
                LinqExpression.Block
                (
                    ImmutableList<LinqExpression>.Empty
                    .AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                serializeMethod[s],
                                pSerializer,
                                PropertyOrField(pA, info.PropertyBindings[s])
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pSerializer, pA }
            );

            return serializeFuncExpr.Compile();
        }

        private static object BuildDeserializeFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type deserializeFuncType = typeof(Func<,>).MakeGenericType(typeof(Deserializer), t);

            ParameterExpression pDeserializer = LinqExpression.Parameter(typeof(Deserializer), "src");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getDeserializeMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "Deserialize",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(Deserializer) }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> deserializeMethod =
                info.ConstructorBindings.Select(getDeserializeMethod).ToImmutableSortedDictionary();

            ImmutableSortedDictionary<string, ParameterExpression> fieldVars =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, ParameterExpression>(s, LinqExpression.Variable(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            LinqLambdaExpression deserializeFuncExpr = LinqExpression.Lambda
            (
                deserializeFuncType,
                LinqExpression.Block
                (
                    ImmutableList<ParameterExpression>.Empty.AddRange(info.ConstructorBindings.Select(s => fieldVars[s])),
                    ImmutableList<LinqExpression>.Empty
                    .AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Assign
                            (
                                fieldVars[s],
                                LinqExpression.Call
                                (
                                    LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                    deserializeMethod[s],
                                    pDeserializer
                                )
                            )
                        )
                    )
                    .Add
                    (
                        LinqExpression.New
                        (
                            info.ConstructorInfo,
                            ImmutableList<LinqExpression>.Empty.AddRange(info.ConstructorBindings.Select(s => fieldVars[s]))
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pDeserializer }
            );

            return deserializeFuncExpr.Compile();
        }

        private static object BuildMeasureBytesFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type measureBytesFuncType = typeof(Action<,>).MakeGenericType(typeof(ByteMeasurer), t);

            ParameterExpression pByteMeasurer = LinqExpression.Parameter(typeof(ByteMeasurer), "measurer");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getMeasureBytesMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "MeasureBytes",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(ByteMeasurer), bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> measureBytesMethod =
                info.ConstructorBindings.Select(getMeasureBytesMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression measureBytesFuncExpr = LinqExpression.Lambda
            (
                measureBytesFuncType,
                LinqExpression.Block
                (
                    ImmutableList<LinqExpression>.Empty.AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                measureBytesMethod[s],
                                pByteMeasurer,
                                PropertyOrField(pA, info.PropertyBindings[s])
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pByteMeasurer, pA }
            );

            return measureBytesFuncExpr.Compile();
        }

        private static object BuildCloneFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type cloneFuncType = typeof(Func<,,>).MakeGenericType(typeof(CloneTracker), t, t);

            ParameterExpression pCloneTracker = LinqExpression.Parameter(typeof(CloneTracker), "tracker");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getCloneMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];
                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "Clone",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(CloneTracker), bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> cloneMethod =
                info.ConstructorBindings.Select(getCloneMethod).ToImmutableSortedDictionary();

            LinqLambdaExpression cloneFuncExpr = LinqExpression.Lambda
            (
                cloneFuncType,
                LinqExpression.New
                (
                    info.ConstructorInfo,
                    ImmutableList<LinqExpression>.Empty.AddRange
                    (
                        info.ConstructorBindings.Select
                        (
                            s => LinqExpression.Call
                            (
                                LinqExpression.Constant(typeTraits[s], typeTraitsType[s]),
                                cloneMethod[s],
                                pCloneTracker,
                                PropertyOrField(pA, info.PropertyBindings[s])
                            )
                        )
                    )
                ),
                true,
                new ParameterExpression[] { pCloneTracker, pA }
            );

            return cloneFuncExpr.Compile();
        }

        private static object BuildAppendDebugStringFunc(Type t, RecordInfo info, ImmutableSortedDictionary<string, object> typeTraits)
        {
            Type appendDebugStringFuncType = typeof(Action<,>).MakeGenericType(typeof(DebugStringBuilder), t);

            ParameterExpression pDebugStringBuilder = LinqExpression.Parameter(typeof(DebugStringBuilder), "sb");
            ParameterExpression pA = LinqExpression.Parameter(t, "a");

            ImmutableSortedDictionary<string, Type> typeTraitsType =
                info.ConstructorBindings.Select(s => new KeyValuePair<string, Type>(s, typeof(ITypeTraits<>).MakeGenericType(info.BindingTypes[s]))).ToImmutableSortedDictionary();

            Func<string, KeyValuePair<string, MethodInfo>> getAppendDebugStringMethod = delegate (string s)
            {
                Type bindingType = info.BindingTypes[s];

                return new KeyValuePair<string, MethodInfo>
                (
                    s,
                    typeTraitsType[s].GetRequiredMethod
                    (
                        "AppendDebugString",
                        BindingFlags.Public | BindingFlags.Instance,
                        new Type[] { typeof(DebugStringBuilder), bindingType }
                    )
                );
            };

            ImmutableSortedDictionary<string, MethodInfo> appendDebugStringMethod =
                info.ConstructorBindings.Select(getAppendDebugStringMethod).ToImmutableSortedDictionary();

            PropertyInfo pBuilder = typeof(DebugStringBuilder).GetRequiredProperty("Builder", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo mAppend = typeof(StringBuilder).GetRequiredMethod("Append", BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(string) });

            LinqExpression appendString(string str)
            {
                return LinqExpression.Call
                (
                    LinqExpression.Property
                    (
                        pDebugStringBuilder,
                        pBuilder
                    ),
                    mAppend,
                    LinqExpression.Constant(str)
                );
            }

            ImmutableList<LinqExpression>.Builder instructions = ImmutableList<LinqExpression>.Empty.ToBuilder();

            instructions.Add(appendString($"( {TypeTraitsUtility.GetTypeName(t)}, "));

            bool needDelim = false;
            foreach(string binding in info.ConstructorBindings)
            {
                instructions.Add(appendString((needDelim ? ", " : "") + binding + " = "));
                needDelim = true;
                instructions.Add
                (
                    LinqExpression.Call
                    (
                        LinqExpression.Constant(typeTraits[binding], typeTraitsType[binding]),
                        appendDebugStringMethod[binding],
                        pDebugStringBuilder,
                        PropertyOrField(pA, info.PropertyBindings[binding])
                    )
                );
            }

            instructions.Add(appendString(" )"));

            LinqLambdaExpression appendDebugStringFunc = LinqExpression.Lambda
            (
                appendDebugStringFuncType,
                LinqExpression.Block
                (
                    instructions.ToImmutable()
                ),
                true,
                new ParameterExpression[] { pDebugStringBuilder, pA }
            );

            return appendDebugStringFunc.Compile();
        }

        private class ReflectedSingleton_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ReflectedSingleton_TypeTraitsBuilder_Rule> value =
                new Lazy<ReflectedSingleton_TypeTraitsBuilder_Rule>(() => new ReflectedSingleton_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ReflectedSingleton_TypeTraitsBuilder_Rule() { }

            public static ReflectedSingleton_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k)
            {
                if (k.ArtifactType != ArtifactType.TypeTraits) return false;

                try
                {
                    Option<RecordOrUnionInfo> info = k.Type.TryGetRecordInfo();
                    return info.HasValue && info.Value is SingletonInfo;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                    return false;
                }
            }

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k) => ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type itemType = k.Type;
                SingletonInfo si = (SingletonInfo)itemType.TryGetRecordInfo().Value;

                Type typeTraitsType = typeof(UnitTypeTraits<>).MakeGenericType(itemType);
                object item = si.SingletonProperty.GetValue(null).AssertNotNull();
                ConstructorInfo ci = typeTraitsType.GetRequiredConstructor(new Type[] { typeof(uint), itemType });
                return ci.Invoke(new object[] { si.HashToken, item });
            }
        }

        private class Reflected_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Reflected_TypeTraitsBuilder_Rule> value =
                new Lazy<Reflected_TypeTraitsBuilder_Rule>(() => new Reflected_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Reflected_TypeTraitsBuilder_Rule() { }

            public static Reflected_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k)
            {
                if (k.ArtifactType != ArtifactType.TypeTraits) return false;

                try
                {
                    Option<RecordOrUnionInfo> info = k.Type.TryGetRecordInfo();
                    return info.HasValue && info.Value is RecordInfo;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                    return false;
                }
            }

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                ImmutableSortedSet<ArtifactKey> results = ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

                try
                {
                    Option<RecordOrUnionInfo> optInfo = k.Type.TryGetRecordInfo();
                    if (optInfo.HasValue && optInfo.Value is RecordInfo info)
                    {
                        foreach(KeyValuePair<string, Type> kvp in info.BindingTypes)
                        {
#if NETSTANDARD2_0
                            if (info.SpecialTypeTraits.TryGetValue(kvp.Key, out ArtifactKey akSpecial))
#else
                            if (info.SpecialTypeTraits.TryGetValue(kvp.Key, out ArtifactKey? akSpecial))
#endif
                            {
                                results = results.Add(akSpecial);
                            }
                            else
                            {
                                results = results.Add(ArtifactKey.Create(ArtifactType.TypeTraits, kvp.Value));
                            }
                        }
                    }
                    else
                    {
                        throw new BuilderException($"{nameof(Reflected_TypeTraitsBuilder_Rule)} could not get prerequisites for {TypeTraitsUtility.GetTypeName(k.Type)}");
                    }
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                }

                return results;
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type itemType = k.Type;

                RecordInfo info = (RecordInfo)itemType.TryGetRecordInfo().Value;
                ImmutableSortedDictionary<string, object> typeTraitsDict = ImmutableSortedDictionary<string, object>.Empty;

                foreach (KeyValuePair<string, Type> kvp in info.BindingTypes)
                {
#if NETSTANDARD2_0
                    if (info.SpecialTypeTraits.TryGetValue(kvp.Key, out ArtifactKey akSpecial))
#else
                    if (info.SpecialTypeTraits.TryGetValue(kvp.Key, out ArtifactKey? akSpecial))
#endif
                    {
                        typeTraitsDict = typeTraitsDict.Add(kvp.Key, prerequisites[akSpecial]);
                    }
                    else
                    {
                        typeTraitsDict = typeTraitsDict.Add(kvp.Key, prerequisites[ArtifactKey.Create(ArtifactType.TypeTraits, kvp.Value)]);
                    }
                }

                Type typeTraitsType = typeof(DelegateTypeTraits<>).MakeGenericType(itemType);

                Type compareFuncType = typeof(Func<,,>).MakeGenericType(itemType, itemType, typeof(int));
                Type addToHashFuncType = typeof(Action<,>).MakeGenericType(typeof(HashBuilder), itemType);
                Type checkAnalogousFuncType = typeof(Action<,,>).MakeGenericType(typeof(AnalogyTracker), itemType, itemType);
                Type canSerializeFuncType = typeof(Func<,>).MakeGenericType(itemType, typeof(bool));
                Type serializeFuncType = typeof(Action<,>).MakeGenericType(typeof(Serializer), itemType);
                Type deserializeFuncType = typeof(Func<,>).MakeGenericType(typeof(Deserializer), itemType);
                Type measureBytesFuncType = typeof(Action<,>).MakeGenericType(typeof(ByteMeasurer), itemType);
                Type cloneFuncType = typeof(Func<,,>).MakeGenericType(typeof(CloneTracker), itemType, itemType);
                Type appendDebugStringFuncType = typeof(Action<,>).MakeGenericType(typeof(DebugStringBuilder), itemType);

                object compareFunc = BuildCompareFunc(itemType, info, typeTraitsDict);
                object addToHashFunc = BuildAddToHashFunc(itemType, info, typeTraitsDict);
                object checkAnalogousFunc = BuildCheckAnalogousFunc(itemType, info, typeTraitsDict);
                object canSerializeFunc = BuildCanSerializeFunc(itemType, info, typeTraitsDict);
                object serializeFunc = BuildSerializeFunc(itemType, info, typeTraitsDict);
                object deserializeFunc = BuildDeserializeFunc(itemType, info, typeTraitsDict);
                object measureBytesFunc = BuildMeasureBytesFunc(itemType, info, typeTraitsDict);
                object cloneFunc = BuildCloneFunc(itemType, info, typeTraitsDict);
                object appendDebugStringFunc = BuildAppendDebugStringFunc(itemType, info, typeTraitsDict);

                ConstructorInfo ci = typeTraitsType.GetRequiredConstructor
                (
                    new Type[]
                    {
                        compareFuncType, addToHashFuncType, checkAnalogousFuncType, canSerializeFuncType,
                        serializeFuncType, deserializeFuncType, measureBytesFuncType, cloneFuncType,
                        appendDebugStringFuncType
                    }
                );

                return ci.Invoke
                (
                    new object[]
                    {
                        compareFunc, addToHashFunc, checkAnalogousFunc, canSerializeFunc,
                        serializeFunc, deserializeFunc, measureBytesFunc, cloneFunc,
                        appendDebugStringFunc
                    }
                );
            }
        }

        private class ReflectedUnion_TypeTraitsBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<ReflectedUnion_TypeTraitsBuilder_Rule> value =
                new Lazy<ReflectedUnion_TypeTraitsBuilder_Rule>(() => new ReflectedUnion_TypeTraitsBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private ReflectedUnion_TypeTraitsBuilder_Rule() { }

            public static ReflectedUnion_TypeTraitsBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k)
            {
                if (k.ArtifactType != ArtifactType.TypeTraits) return false;

                try
                {
                    Option<RecordOrUnionInfo> info = k.Type.TryGetRecordInfo();
                    return info.HasValue && info.Value is UnionOfDescendantsInfo;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                    return false;
                }
            }

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                ImmutableSortedSet<ArtifactKey> results = ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);

                try
                {
                    Option<RecordOrUnionInfo> optInfo = k.Type.TryGetRecordInfo();
                    if (optInfo.HasValue && optInfo.Value is UnionOfDescendantsInfo info)
                    {
                        results = results.Union(info.Descendants.Keys.Select(t => ArtifactKey.Create(ArtifactType.TypeTraits, t)));
                    }
                    else
                    {
                        throw new BuilderException($"{nameof(Reflected_TypeTraitsBuilder_Rule)} could not get prerequisites for {TypeTraitsUtility.GetTypeName(k.Type)}");
                    }
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                }

                return results;
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Type itemType = k.Type;

                UnionOfDescendantsInfo info = (UnionOfDescendantsInfo)itemType.TryGetRecordInfo().Value;

                Type unionCaseItemType = typeof(IUnionCaseTypeTraits<,>).MakeGenericType(typeof(string), itemType);
                Type unionCaseListType = typeof(ImmutableList<>).MakeGenericType(unionCaseItemType);
                object unionCases = unionCaseListType.GetRequiredField("Empty", BindingFlags.Public | BindingFlags.Static).GetValue(null).AssertNotNull();
                MethodInfo addUnionCase = unionCaseListType.GetRequiredMethod("Add", BindingFlags.Public | BindingFlags.Instance, new Type[] { unionCaseItemType });

                foreach(KeyValuePair<Type, (string, RecordOrUnionInfo)> kvp in info.Descendants)
                {
                    Type unionCaseType = typeof(UnionCaseTypeTraits2<,,>).MakeGenericType(typeof(string), itemType, kvp.Key);
                    Type subCompareWorkerType = typeof(ITypeTraits<>).MakeGenericType(kvp.Key);
                    ConstructorInfo ciUnionCase = unionCaseType.GetRequiredConstructor(new Type[] { typeof(string), subCompareWorkerType });
                    string name = kvp.Value.Item1;
                    object typeTraits = prerequisites[ArtifactKey.Create(ArtifactType.TypeTraits, kvp.Key)];
                    object unionCase = ciUnionCase.Invoke(new object[] { name, typeTraits });
                    unionCases = addUnionCase.Invoke(unionCases, new object[] { unionCase }).AssertNotNull();
                }

                Type unionCompareWorkerType = typeof(UnionTypeTraits<,>).MakeGenericType(typeof(string), itemType);
                ConstructorInfo ciUnionCompareWorker = unionCompareWorkerType.GetRequiredConstructor(new Type[] { typeof(ITypeTraits<string>), unionCaseListType });

                return ciUnionCompareWorker.Invoke(new object[] { StringTypeTraits.Value, unionCases });
            }
        }

        private class Reflected_SetterBuilder_Rule : IBuildRule<ArtifactKey, object>
        {
            private static readonly Lazy<Reflected_SetterBuilder_Rule> value =
                new Lazy<Reflected_SetterBuilder_Rule>(() => new Reflected_SetterBuilder_Rule(), LazyThreadSafetyMode.ExecutionAndPublication);

            private Reflected_SetterBuilder_Rule() { }

            public static Reflected_SetterBuilder_Rule Value => value.Value;

            public bool CanBuild(ArtifactKey k)
            {
                if (k.ArtifactType != ArtifactType.Setter) return false;

                try
                {
                    Option<RecordOrUnionInfo> info = k.Type.TryGetRecordInfo();
                    return info.HasValue && info.Value is RecordInfo;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine($"{exc.GetType().FullName} : {exc.Message}");
                    return false;
                }
            }

            public ImmutableSortedSet<ArtifactKey> GetPrerequisites(ArtifactKey k)
            {
                return ImmutableSortedSet<ArtifactKey>.Empty.WithComparer(ArtifactKey.Adapter);
            }

            public object Build(ArtifactKey k, ImmutableSortedDictionary<ArtifactKey, object> prerequisites)
            {
                Option<RecordOrUnionInfo> info = k.Type.TryGetRecordInfo();
                if (info.HasValue && info.Value is RecordInfo dinfo)
                {
                    if (dinfo.BindingTypes.ContainsKey(k.Field))
                    {
                        Type fieldType = dinfo.BindingTypes[k.Field];

                        Type funcType = typeof(Func<,,>).MakeGenericType(k.Type, fieldType, k.Type);

                        ParameterExpression pOldWhole = LinqExpression.Variable(k.Type);
                        ParameterExpression pNewField = LinqExpression.Variable(fieldType);

                        LinqExpression body = LinqExpression.New
                        (
                            dinfo.ConstructorInfo,
                            dinfo.ConstructorBindings.Select
                            (
                                s =>
                                {
                                    if (s == k.Field)
                                    {
                                        return (LinqExpression)pNewField;
                                    }
                                    else
                                    {
                                        return PropertyOrField
                                        (
                                            pOldWhole,
                                            dinfo.PropertyBindings[s]
                                        );
                                    }
                                }
                            )
                        );

                        LinqLambdaExpression funcExpr = LinqExpression.Lambda(funcType, body, true, pOldWhole, pNewField);

                        return funcExpr.Compile();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Binding variable {k.Field} not found on type {TypeTraitsUtility.GetTypeName(k.Type)}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Type {TypeTraitsUtility.GetTypeName(k.Type)} doesn't have direct pattern-bind attributes");
                }
            }
        }

        public Func<T, U, T> GetSetter<T, U>(string bindingVariable)
        {
            Option<RecordOrUnionInfo> info = typeof(T).TryGetRecordInfo();
            if (!info.HasValue)
            {
                throw new InvalidOperationException($"Type {TypeTraitsUtility.GetTypeName(typeof(T))} doesn't have pattern-bind attributes");
            }

            if (info.Value is RecordInfo dinfo)
            {
                if (dinfo.BindingTypes.ContainsKey(bindingVariable))
                {
                    if (TypeTypeTraits.Value.Compare(dinfo.BindingTypes[bindingVariable], typeof(U)) == 0)
                    {
                        return (Func<T, U, T>)GetArtifact(ArtifactKey.Create(ArtifactType.Setter, typeof(T), bindingVariable));
                    }
                    else
                    {
                        throw new InvalidOperationException
                        (
                            $"Type mismatch: Binding variable {bindingVariable}" +
                            $" on type {TypeTraitsUtility.GetTypeName(typeof(T))}" +
                            $" has type {TypeTraitsUtility.GetTypeName(dinfo.BindingTypes[bindingVariable])}," +
                            $" but {TypeTraitsUtility.GetTypeName(typeof(U))} was requested"
                        );
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Binding variable {bindingVariable} not found on type {TypeTraitsUtility.GetTypeName(typeof(T))}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Type {TypeTraitsUtility.GetTypeName(typeof(T))} doesn't have direct pattern-bind attributes");
            }
        }
    }

    public static partial class Extensions
    {
        public static ImmutableList<T> AddIfAny<T>(this ImmutableList<T> items, Option<T> optionalItem)
        {
            if (optionalItem.HasValue)
            {
                return items.Add(optionalItem.Value);
            }
            else
            {
                return items;
            }
        }
    }
}
