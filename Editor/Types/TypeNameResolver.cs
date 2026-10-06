using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Resolves C# type expressions such as <c>MyGame.SaveData</c>, <c>List&lt;int&gt;</c>, <c>Dictionary&lt;string, Vector3&gt;</c>,
    /// <c>int[]</c>, <c>int?</c> or <c>Outer.Inner</c> to loaded <see cref="Type"/> objects.
    /// Short names are looked up in the definition namespace (and its parents), then in
    /// <c>System</c>, <c>System.Collections.Generic</c> and <c>UnityEngine</c>. Results are cached per domain.
    /// </summary>
    public static class TypeNameResolver
    {
        private const string GlobalQualifier = "global::";
        private const char GenericArityMarker = '`';
        private const char NestedTypeSeparator = '+';
        private const char NamespaceSeparator = '.';

        private static readonly string[] s_ImplicitNamespaces = { "System", "System.Collections.Generic", "UnityEngine" };

        private static readonly Dictionary<string, Type> s_AliasLookup =
            CSharpTypeName.Aliases.ToDictionary(pair => pair.Value, pair => pair.Key);

        private static readonly Dictionary<string, ResolveResult> s_ResultCache = new Dictionary<string, ResolveResult>();
        private static readonly Dictionary<string, Type> s_MetadataNameCache = new Dictionary<string, Type>();

        /// <summary>Outcome of a resolution.</summary>
        public readonly struct ResolveResult
        {
            /// <summary>The resolved type, or <c>null</c>.</summary>
            public readonly Type Type;

            /// <summary>Why resolution failed, or <c>null</c> on success.</summary>
            public readonly string Error;

            /// <summary>Creates a result.</summary>
            public ResolveResult(Type type, string error)
            {
                Type = type;
                Error = error;
            }

            /// <summary>True when <see cref="Type"/> is not <c>null</c>.</summary>
            public bool Success => Type != null;
        }

        /// <summary>Clears cached lookups. Called automatically on domain reload (static state is reset).</summary>
        public static void ClearCache()
        {
            s_ResultCache.Clear();
            s_MetadataNameCache.Clear();
        }

        /// <summary>Resolves <paramref name="expression"/>, using <paramref name="contextNamespace"/> for short names.</summary>
        public static ResolveResult Resolve(string expression, string contextNamespace)
        {
            if (string.IsNullOrWhiteSpace(expression)) return new ResolveResult(null, "Type name is empty.");

            string cacheKey = (contextNamespace ?? string.Empty) + "|" + expression;
            if (s_ResultCache.TryGetValue(cacheKey, out ResolveResult cached)) return cached;

            ResolveResult result;
            try
            {
                Parser parser = new Parser(expression, contextNamespace);
                Type type = parser.ParseComplete();
                result = new ResolveResult(type, null);
            }
            catch (TypeResolveException exception)
            {
                result = new ResolveResult(null, exception.Message);
            }

            s_ResultCache[cacheKey] = result;
            return result;
        }

        private static Type FindByMetadataName(string metadataName)
        {
            if (s_MetadataNameCache.TryGetValue(metadataName, out Type cached)) return cached;

            Type found = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidate;
                try
                {
                    candidate = assembly.GetType(metadataName, false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (candidate == null) continue;

                found = candidate;
                if (candidate.IsPublic || candidate.IsNestedPublic) break;
            }

            s_MetadataNameCache[metadataName] = found;
            return found;
        }

        private static IEnumerable<string> EnumerateNestedVariants(string dottedName)
        {
            yield return dottedName;

            char[] characters = dottedName.ToCharArray();
            for (int index = characters.Length - 1; index >= 0; index--)
            {
                if (characters[index] != NamespaceSeparator) continue;

                characters[index] = NestedTypeSeparator;
                yield return new string(characters);
            }
        }

        private sealed class TypeResolveException : Exception
        {
            public TypeResolveException(string message) : base(message) { }
        }

        private sealed class Parser
        {
            private readonly string m_Text;
            private readonly string m_ContextNamespace;
            private int m_Position;

            public Parser(string text, string contextNamespace)
            {
                m_Text = text;
                m_ContextNamespace = contextNamespace ?? string.Empty;
            }

            public Type ParseComplete()
            {
                Type type = ParseType();
                SkipWhitespace();
                if (m_Position < m_Text.Length)
                {
                    throw new TypeResolveException($"Unexpected '{m_Text[m_Position]}' at position {m_Position + 1}.");
                }

                return type;
            }

            private Type ParseType()
            {
                SkipWhitespace();
                if (string.CompareOrdinal(m_Text, m_Position, GlobalQualifier, 0, GlobalQualifier.Length) == 0)
                {
                    m_Position += GlobalQualifier.Length;
                }

                string name = ParseQualifiedName();
                List<Type> arguments = new List<Type>();
                SkipWhitespace();
                if (Peek() == '<')
                {
                    m_Position++;
                    do
                    {
                        arguments.Add(ParseType());
                        SkipWhitespace();
                    } while (TryConsume(','));

                    Expect('>');
                }

                Type type = LookupType(name, arguments);
                return ParseSuffixes(type);
            }

            private Type ParseSuffixes(Type type)
            {
                while (true)
                {
                    SkipWhitespace();
                    if (TryConsume('?'))
                    {
                        if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null)
                        {
                            throw new TypeResolveException($"'{CSharpTypeName.GetReadable(type)}?' is only valid for non-nullable value types.");
                        }

                        type = typeof(Nullable<>).MakeGenericType(type);
                        continue;
                    }

                    if (TryConsume('['))
                    {
                        int rank = 1;
                        SkipWhitespace();
                        while (TryConsume(','))
                        {
                            rank++;
                            SkipWhitespace();
                        }

                        Expect(']');
                        type = rank == 1 ? type.MakeArrayType() : type.MakeArrayType(rank);
                        continue;
                    }

                    return type;
                }
            }

            private string ParseQualifiedName()
            {
                StringBuilder builder = new StringBuilder();
                while (true)
                {
                    SkipWhitespace();
                    int start = m_Position;
                    while (m_Position < m_Text.Length && (char.IsLetterOrDigit(m_Text[m_Position]) || m_Text[m_Position] == '_'))
                    {
                        m_Position++;
                    }

                    if (start == m_Position) throw new TypeResolveException($"Expected a type name at position {start + 1}.");

                    builder.Append(m_Text, start, m_Position - start);
                    SkipWhitespace();
                    if (!TryConsume(NamespaceSeparator)) return builder.ToString();

                    builder.Append(NamespaceSeparator);
                }
            }

            private Type LookupType(string name, List<Type> arguments)
            {
                if (arguments.Count == 0 && s_AliasLookup.TryGetValue(name, out Type aliasType)) return aliasType;

                string aritySuffix = arguments.Count > 0
                    ? GenericArityMarker + arguments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty;
                Type definition = FindDefinition(name, aritySuffix);
                if (definition == null)
                {
                    string displayName = arguments.Count > 0 ? $"{name}<{new string(',', arguments.Count - 1)}>" : name;
                    throw new TypeResolveException(
                        $"Type '{displayName}' was not found. Use the namespace-qualified name or pick it from the list.");
                }

                if (arguments.Count == 0) return definition;

                try
                {
                    return definition.MakeGenericType(arguments.ToArray());
                }
                catch (ArgumentException exception)
                {
                    throw new TypeResolveException($"Invalid generic arguments for '{name}': {exception.Message}");
                }
            }

            private Type FindDefinition(string name, string aritySuffix)
            {
                Type exact = FindWithNestedVariants(name, aritySuffix);
                if (exact != null) return exact;

                string contextNamespace = m_ContextNamespace;
                while (!string.IsNullOrEmpty(contextNamespace))
                {
                    Type contextual = FindWithNestedVariants(contextNamespace + NamespaceSeparator + name, aritySuffix);
                    if (contextual != null) return contextual;

                    int lastSeparator = contextNamespace.LastIndexOf(NamespaceSeparator);
                    contextNamespace = lastSeparator < 0 ? string.Empty : contextNamespace.Substring(0, lastSeparator);
                }

                List<Type> implicitMatches = new List<Type>();
                foreach (string implicitNamespace in s_ImplicitNamespaces)
                {
                    Type candidate = FindWithNestedVariants(implicitNamespace + NamespaceSeparator + name, aritySuffix);
                    if (candidate != null && !implicitMatches.Contains(candidate)) implicitMatches.Add(candidate);
                }

                if (implicitMatches.Count > 1)
                {
                    string options = string.Join(", ", implicitMatches.Select(CSharpTypeName.GetReadable));
                    throw new TypeResolveException($"'{name}' is ambiguous between: {options}. Use the full name.");
                }

                return implicitMatches.Count == 1 ? implicitMatches[0] : null;
            }

            private static Type FindWithNestedVariants(string dottedName, string aritySuffix)
            {
                foreach (string variant in EnumerateNestedVariants(dottedName))
                {
                    Type type = FindByMetadataName(variant + aritySuffix);
                    if (type != null) return type;
                }

                return null;
            }

            private char Peek() => m_Position < m_Text.Length ? m_Text[m_Position] : '\0';

            private bool TryConsume(char expected)
            {
                if (Peek() != expected) return false;

                m_Position++;
                return true;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (!TryConsume(expected))
                {
                    throw new TypeResolveException($"Expected '{expected}' at position {m_Position + 1}.");
                }
            }

            private void SkipWhitespace()
            {
                while (m_Position < m_Text.Length && char.IsWhiteSpace(m_Text[m_Position]))
                {
                    m_Position++;
                }
            }
        }
    }
}
