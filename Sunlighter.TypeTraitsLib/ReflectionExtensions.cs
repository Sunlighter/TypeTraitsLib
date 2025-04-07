using System;
using System.Linq;
using System.Reflection;

namespace Sunlighter.TypeTraitsLib
{
    public static class ReflectionExtensions
    {
        public static MethodInfo GetRequiredMethod(this Type t, string name, BindingFlags flags, Type[] parameterTypes)
        {
#if NETSTANDARD2_0
            MethodInfo m = t.GetMethod(name, flags, Type.DefaultBinder, parameterTypes, null);
#else
            MethodInfo? m = t.GetMethod(name, flags, Type.DefaultBinder, parameterTypes, null);
#endif
            if (m is null) throw new Exception($"Method {TypeTraitsUtility.GetTypeName(t)}.{name}({string.Join(", ", parameterTypes.Select(t2 => TypeTraitsUtility.GetTypeName(t2)))}) not found");
            return m;
        }

        public static ConstructorInfo GetRequiredConstructor(this Type t, Type[] parameterTypes)
        {
#if NETSTANDARD2_0
            ConstructorInfo c = t.GetConstructor(parameterTypes);
#else
            ConstructorInfo? c = t.GetConstructor(parameterTypes);
#endif
            if (c is null) throw new Exception($"Constructor {TypeTraitsUtility.GetTypeName(t)}({string.Join(", ", parameterTypes.Select(t2 => TypeTraitsUtility.GetTypeName(t2)))}) not found");
            return c;
        }

        public static PropertyInfo GetRequiredProperty(this Type t, string name, BindingFlags flags)
        {
#if NETSTANDARD2_0
            PropertyInfo p = t.GetProperty(name, flags);
#else
            PropertyInfo? p = t.GetProperty(name, flags);
#endif
            if (p is null) throw new Exception($"Property {TypeTraitsUtility.GetTypeName(t)}.{name} not found");
            return p;
        }

        public static PropertyInfo GetRequiredProperty(this Type t, string name, BindingFlags flags, Type propertyType, Type[] parameterTypes)
        {
#if NETSTANDARD2_0
            PropertyInfo p = t.GetProperty(name, flags, Type.DefaultBinder, propertyType, parameterTypes, null);
#else
            PropertyInfo? p = t.GetProperty(name, flags, Type.DefaultBinder, propertyType, parameterTypes, null);
#endif
            if (p is null) throw new Exception($"Property {TypeTraitsUtility.GetTypeName(t)}.{name}, of type {TypeTraitsUtility.GetTypeName(propertyType)}, with arguments ({string.Join(", ", parameterTypes.Select(TypeTraitsUtility.GetTypeName))}) not found");
            return p;
        }

        public static FieldInfo GetRequiredField(this Type t, string name, BindingFlags flags)
        {
#if NETSTANDARD2_0
            FieldInfo f = t.GetField(name, flags);
#else
            FieldInfo? f = t.GetField(name, flags);
#endif
            if (f is null) throw new Exception($"Field {TypeTraitsUtility.GetTypeName(t)}.{name} not found");
            return f;
        }
    }
}
