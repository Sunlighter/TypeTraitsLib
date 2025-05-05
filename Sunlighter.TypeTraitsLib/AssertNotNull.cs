using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sunlighter.TypeTraitsLib
{
    public static partial class Extensions
    {
#if NETSTANDARD2_0
        public static T AssertNotNull<T>(this T item) where T : class
        {
            if (item is null)
            {
                throw new NullReferenceException($"Expression of type {typeof(T).FullName ?? "(null)"} was unexpectedly null");
            }
            else
            {
                return item;
            }
        }

        public static T ValueAssertNotNull<T>(this StrongBox<T> box) where T : class
        {
            T item = box.Value;
            if (item is null)
            {
                throw new NullReferenceException($"Value in StrongBox of type {typeof(T).FullName ?? "(null)"} was unexpectedly null");
            }
            else
            {
                return item;
            }
        }

        public static string FullNameNotNull(this Type t)
        {
            string fullName = t.FullName;
            if (fullName is null)
            {
                throw new TypeTraitsException($"Type does not have a FullName");
            }
            else
            {
                return fullName;
            }
        }

        public static string FullNameNotNull(this Assembly a)
        {
            string fullName = a.FullName;
            if (fullName is null)
            {
                throw new TypeTraitsException($"Assembly does not have a FullName");
            }
            else
            {
                return fullName;
            }
        }
#else
        public static T AssertNotNull<T>(this T? item, [CallerArgumentExpression(nameof(item))] string itemExpr = "[???]") where T : class
        {
            if (item is null)
            {
                throw new NullReferenceException($"{itemExpr} of type {typeof(T).FullName ?? "(null)"} was unexpectedly null");
            }
            else
            {
                return item;
            }
        }

        public static T ValueAssertNotNull<T>(this StrongBox<T> box, [CallerArgumentExpression(nameof(box))] string boxExpr = "[???]") where T : class
        {
            T? item = box.Value;
            if (item is null)
            {
                throw new NullReferenceException($"Value in StrongBox {boxExpr} of type {typeof(T).FullName ?? "(null)"} was unexpectedly null");
            }
            else
            {
                return item;
            }
        }

        public static string FullNameNotNull(this Type t, [CallerArgumentExpression(nameof(t))] string tExpr = "[???]")
        {
            string? fullName = t.FullName;
            if (fullName is null)
            {
                throw new TypeTraitsException($"Type {tExpr} does not have a FullName");
            }
            else
            {
                return fullName;
            }
        }

        public static string FullNameNotNull(this Assembly a, [CallerArgumentExpression(nameof(a))] string aExpr = "[???]")
        {
            string? fullName = a.FullName;
            if (fullName is null)
            {
                throw new TypeTraitsException($"Assembly {aExpr} does not have a FullName");
            }
            else
            {
                return fullName;
            }
        }
#endif

        public static T LastItem<T>(this ImmutableList<T> items)
        {
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Can't take last item of an empty list");
            }
            else
            {
                return items[items.Count - 1];
            }
        }
    }
}
