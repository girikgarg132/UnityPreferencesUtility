using System;
using System.Collections.Generic;
using System.Text;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Produces C# source names for <see cref="Type"/> objects (generics, nesting, arrays, nullables and aliases).</summary>
    public static class CSharpTypeName
    {
        private const string GlobalPrefix = "global::";
        private const char GenericArityMarker = '`';

        private static readonly Dictionary<Type, string> s_Aliases = new Dictionary<Type, string>
        {
            { typeof(bool), "bool" }, { typeof(byte), "byte" }, { typeof(sbyte), "sbyte" }, { typeof(char), "char" },
            { typeof(short), "short" }, { typeof(ushort), "ushort" }, { typeof(int), "int" }, { typeof(uint), "uint" },
            { typeof(long), "long" }, { typeof(ulong), "ulong" }, { typeof(float), "float" }, { typeof(double), "double" },
            { typeof(decimal), "decimal" }, { typeof(string), "string" }, { typeof(object), "object" }
        };

        /// <summary>The alias table (for example <c>int</c> to <see cref="int"/>).</summary>
        public static IReadOnlyDictionary<Type, string> Aliases => s_Aliases;

        /// <summary>Fully qualified name with <c>global::</c> prefixes, safe to emit in any namespace.</summary>
        public static string GetQualified(Type type) => Build(type, true);

        /// <summary>Readable name without <c>global::</c> (still namespace-qualified), used in the UI and definition files.</summary>
        public static string GetReadable(Type type) => Build(type, false);

        private static string Build(Type type, bool qualified)
        {
            if (type == null) return string.Empty;
            if (s_Aliases.TryGetValue(type, out string alias)) return alias;
            if (type.IsGenericParameter) return type.Name;

            if (type.IsArray)
            {
                string elementName = Build(type.GetElementType(), qualified);
                return elementName + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            }

            Type nullableUnderlying = Nullable.GetUnderlyingType(type);
            if (nullableUnderlying != null) return Build(nullableUnderlying, qualified) + "?";

            Type[] genericArguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            List<Type> chain = new List<Type>();
            for (Type current = type; current != null; current = current.DeclaringType)
            {
                chain.Insert(0, current);
            }

            StringBuilder builder = new StringBuilder();
            if (qualified) builder.Append(GlobalPrefix);
            if (!string.IsNullOrEmpty(chain[0].Namespace)) builder.Append(chain[0].Namespace).Append('.');

            int consumedArguments = 0;
            for (int i = 0; i < chain.Count; i++)
            {
                Type segment = chain[i];
                if (i > 0) builder.Append('.');
                builder.Append(StripArity(segment.Name));

                int segmentArgumentCount = segment.IsGenericType ? segment.GetGenericArguments().Length : 0;
                int ownArgumentCount = segmentArgumentCount - consumedArguments;
                if (ownArgumentCount <= 0) continue;

                builder.Append('<');
                for (int argumentIndex = 0; argumentIndex < ownArgumentCount; argumentIndex++)
                {
                    if (argumentIndex > 0) builder.Append(", ");
                    int absoluteIndex = consumedArguments + argumentIndex;
                    builder.Append(absoluteIndex < genericArguments.Length
                        ? Build(genericArguments[absoluteIndex], qualified)
                        : string.Empty);
                }

                builder.Append('>');
                consumedArguments = segmentArgumentCount;
            }

            return builder.ToString();
        }

        private static string StripArity(string name)
        {
            int markerIndex = name.IndexOf(GenericArityMarker);
            return markerIndex < 0 ? name : name.Substring(0, markerIndex);
        }
    }
}
