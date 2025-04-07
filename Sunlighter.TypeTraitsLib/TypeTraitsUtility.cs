using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sunlighter.TypeTraitsLib
{
    public static partial class TypeTraitsUtility
    {
        private static readonly Lazy<ImmutableSortedDictionary<Type, string>> commonTypes =
            new Lazy<ImmutableSortedDictionary<Type, string>>(GetCommonTypes, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<Type, string> GetCommonTypes()
        {
            return ImmutableSortedDictionary<Type, string>.Empty
                .WithComparers(TypeTypeTraits.Adapter)
                .Add(typeof(bool), "bool")
                .Add(typeof(byte), "byte")
                .Add(typeof(short), "short")
                .Add(typeof(int), "int")
                .Add(typeof(long), "long")
                .Add(typeof(sbyte), "sbyte")
                .Add(typeof(ushort), "ushort")
                .Add(typeof(uint), "uint")
                .Add(typeof(ulong), "ulong")
                .Add(typeof(char), "char")
                .Add(typeof(string), "string")
                .Add(typeof(float), "float")
                .Add(typeof(double), "double")
                .Add(typeof(decimal), "decimal")
                .Add(typeof(BigInteger), "BigInteger")
                .Add(typeof(ImmutableList<>), "ImmutableList")
                .Add(typeof(ImmutableSortedSet<>), "ImmutableSortedSet")
                .Add(typeof(ImmutableSortedDictionary<,>), "ImmutableSortedDictionary");
        }

        private static readonly Lazy<Regex> backTick = new Lazy<Regex>(() => new Regex("`[1-9][0-9]*", RegexOptions.None), LazyThreadSafetyMode.ExecutionAndPublication);

        internal static Regex BackTickRegex => backTick.Value;

        private static readonly ConditionalWeakTable<Type, string> typeNameTable = new ConditionalWeakTable<Type, string>();

        public static string GetTypeName(Type t)
        {
            return typeNameTable.GetValue(t, t2 => GetTypeNameInternal(t2));
        }

        private static string GetTypeNameInternal(Type t)
        {
            if (t.IsGenericType)
            {
                if (t.IsGenericTypeDefinition)
                {
                    if (commonTypes.Value.ContainsKey(t))
                    {
                        return commonTypes.Value[t];
                    }
                    else
                    {
                        return t.FullNameNotNull().UpTo(backTick.Value);
                    }
                }
                else
                {
                    Type gtd = t.GetGenericTypeDefinition();
                    string canonicalName;
                    if (commonTypes.Value.ContainsKey(gtd))
                    {
                        canonicalName = commonTypes.Value[gtd];
                    }
                    else
                    {
                        canonicalName = gtd.FullNameNotNull().UpTo(backTick.Value);
                    }

                    return $"{canonicalName}<{string.Join(",", t.GetGenericArguments().Select(t2 => GetTypeName(t2)))}>";
                }
            }
            else if (t.IsArray)
            {
                return GetTypeName(t.GetElementType().AssertNotNull()) + "[]";
            }
            else
            {
                if (commonTypes.Value.ContainsKey(t))
                {
                    return commonTypes.Value[t];
                }
                else
                {
                    return t.FullNameNotNull();
                }
            }
        }

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

        private static readonly Lazy<ImmutableSortedDictionary<char, char>> stringCharsToEscape = new Lazy<ImmutableSortedDictionary<char, char>>(GetStringCharsToEscape, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<char, char> GetStringCharsToEscape()
        {
            return ImmutableSortedDictionary<char, char>.Empty
                .Add('"', '"')
                .Add('\\', '\\')
                .Add('\a', 'a')
                .Add('\b', 'b')
                .Add('\t', 't')
                .Add('\n', 'n')
                .Add('\v', 'v')
                .Add('\f', 'f')
                .Add('\r', 'r')
                .Add('\x1b', 'e');
        }

        public static ImmutableSortedDictionary<char, char> StringCharsToEscape => stringCharsToEscape.Value;

        public static void AppendQuoted(this StringBuilder sb, string str)
        {
            ImmutableSortedDictionary<char, char> symbolChars = stringCharsToEscape.Value;

            sb.Append('\"');

            foreach (char ch in str)
            {
                if (symbolChars.ContainsKey(ch))
                {
                    sb.Append("\\" + symbolChars[ch]);
                }
                else if (ch == ' ' || (!char.IsControl(ch) && !char.IsWhiteSpace(ch) && !char.IsSurrogate(ch)))
                {
                    sb.Append(ch);
                }
                else sb.Append("\\x" + ((int)ch).ToString("X") + ";");
            }

            sb.Append('\"');
        }

        public static string Quoted(this string str)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendQuoted(str);
            return sb.ToString();
        }

        private static readonly Lazy<ImmutableSortedDictionary<char, string>> charToName = new Lazy<ImmutableSortedDictionary<char, string>>(GetCharToName, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<char, string> GetCharToName()
        {
            return ImmutableSortedDictionary<char, string>.Empty
                .Add('\a', "alarm")
                .Add('\b', "backspace")
                .Add('\x7f', "delete")
                .Add('\x1b', "escape")
                .Add('\n', "newline")
                .Add('\x00', "null")
                .Add('\r', "return")
                .Add(' ', "space")
                .Add('\t', "tab")
                .Add('\v', "vtab")
                .Add('\f', "page");
        }

        public static ImmutableSortedDictionary<char, string> CharToName => charToName.Value;

        private static readonly Lazy<ImmutableSortedDictionary<string, char>> nameToChar = new Lazy<ImmutableSortedDictionary<string, char>>(GetNameToChar, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<string, char> GetNameToChar()
        {
            return ImmutableSortedDictionary<string, char>.Empty.AddRange(CharToName.Select(kvp => new KeyValuePair<string, char>(kvp.Value, kvp.Key)));
        }

        public static ImmutableSortedDictionary<string, char> NameToChar => nameToChar.Value;


        public static void AppendCharName(this StringBuilder sb, char ch)
        {
            if (!char.IsControl(ch) && !char.IsWhiteSpace(ch) && !char.IsSurrogate(ch))
            {
                sb.Append("#\\");
                sb.Append(ch);
            }
#if NETSTANDARD2_0
            else if (charToName.Value.TryGetValue(ch, out string name))
#else
            else if (charToName.Value.TryGetValue(ch, out string? name))
#endif
            {
                sb.Append("#\\");
                sb.Append(name);
            }
            else
            {
                sb.Append("#\\x");
                sb.AppendFormat("x6", (int)ch);
            }
        }

        public const string SymbolRegexPattern = "\\G(?!-?[0-9]+)(?:\\p{L}|\\p{N}|[\\!\\$\\%\\&\\*\\+\\-\\.\\/\\:\\<\\=\\>\\?\\@\\^_\\~])+\\z";

        private static readonly Lazy<Regex> symbolRegex = new Lazy<Regex>(() => new Regex(SymbolRegexPattern, RegexOptions.None), LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<ImmutableSortedDictionary<char, char>> symbolCharsToEscape =
            new Lazy<ImmutableSortedDictionary<char, char>>(GetSymbolCharsToEscape, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<char, char> GetSymbolCharsToEscape()
        {
            return ImmutableSortedDictionary<char, char>.Empty
                .Add('|', '|')
                .Add('\\', '\\')
                .Add('\a', 'a')
                .Add('\b', 'b')
                .Add('\t', 't')
                .Add('\n', 'n')
                .Add('\v', 'v')
                .Add('\f', 'f')
                .Add('\r', 'r')
                .Add('\x1b', 'e');
        }

        public static ImmutableSortedDictionary<char, char> SymbolCharsToEscape => symbolCharsToEscape.Value;

        public static string SymbolEscape(string name)
        {
            StringBuilder sb = new StringBuilder("|");
            ImmutableSortedDictionary<char, char> symbolChars = symbolCharsToEscape.Value;

            foreach (char ch in name)
            {
                if (symbolChars.ContainsKey(ch))
                {
                    sb.Append("\\" + symbolChars[ch]);
                }
                else if (ch == ' ' || (!char.IsControl(ch) && !char.IsWhiteSpace(ch) && !char.IsSurrogate(ch)))
                {
                    sb.Append(ch);
                }
                else sb.Append("\\x" + ((int)ch).ToString("X") + ";");
            }

            sb.Append('|');

            return sb.ToString();
        }

        public static string SymbolToString(string name)
        {
            if (name == "")
            {
                return "||";
            }
            else if (symbolRegex.Value.IsMatch(name))
            {
                return name;
            }
            else
            {
                return SymbolEscape(name);
            }
        }
    }
}
