using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>C# identifier validation and literal escaping helpers for code generation.</summary>
    public static class CSharpSyntax
    {
        private const string FallbackIdentifier = "Prefs";
        private const char Underscore = '_';
        private const char NamespaceSeparator = '.';
        private const string UnicodeEscapeFormat = "x4";

        private static readonly HashSet<string> s_Keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>Returns true if <paramref name="text"/> is a reserved C# keyword.</summary>
        public static bool IsKeyword(string text) => text != null && s_Keywords.Contains(text);

        /// <summary>Returns true if <paramref name="text"/> is a valid, non-keyword C# identifier.</summary>
        public static bool IsValidIdentifier(string text)
        {
            if (string.IsNullOrEmpty(text) || IsKeyword(text)) return false;
            if (!IsIdentifierStart(text[0])) return false;

            for (int i = 1; i < text.Length; i++)
            {
                if (!IsIdentifierPart(text[i])) return false;
            }

            return true;
        }

        /// <summary>Returns true if <paramref name="text"/> is empty or a dot-separated list of valid identifiers.</summary>
        public static bool IsValidNamespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            foreach (string part in text.Split(NamespaceSeparator))
            {
                if (!IsValidIdentifier(part)) return false;
            }

            return true;
        }

        /// <summary>
        /// Converts arbitrary text (for example a file name) into a PascalCase identifier:
        /// invalid characters are removed and the following letter is capitalized.
        /// </summary>
        public static string ToIdentifier(string text)
        {
            if (string.IsNullOrEmpty(text)) return FallbackIdentifier;

            StringBuilder builder = new StringBuilder(text.Length);
            bool capitalizeNext = true;
            foreach (char character in text)
            {
                if (!IsIdentifierPart(character))
                {
                    capitalizeNext = true;
                    continue;
                }

                builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
                capitalizeNext = false;
            }

            if (builder.Length == 0) return FallbackIdentifier;
            if (!IsIdentifierStart(builder[0])) builder.Insert(0, Underscore);

            string identifier = builder.ToString();
            return IsKeyword(identifier) ? Underscore + identifier : identifier;
        }

        /// <summary>Creates a regular (escaped) C# string literal, including the quotes.</summary>
        public static string StringLiteral(string value)
        {
            if (value == null) return "null";

            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                AppendEscaped(builder, character, '"');
            }

            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>Creates a C# character literal, including the quotes.</summary>
        public static string CharLiteral(char value)
        {
            StringBuilder builder = new StringBuilder(8);
            builder.Append('\'');
            AppendEscaped(builder, value, '\'');
            builder.Append('\'');
            return builder.ToString();
        }

        /// <summary>Escapes text for XML documentation comments.</summary>
        public static string XmlEscape(string text) =>
            (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static void AppendEscaped(StringBuilder builder, char character, char quote)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); return;
                case '\0': builder.Append("\\0"); return;
                case '\a': builder.Append("\\a"); return;
                case '\b': builder.Append("\\b"); return;
                case '\f': builder.Append("\\f"); return;
                case '\n': builder.Append("\\n"); return;
                case '\r': builder.Append("\\r"); return;
                case '\t': builder.Append("\\t"); return;
                case '\v': builder.Append("\\v"); return;
            }

            if (character == quote)
            {
                builder.Append('\\').Append(character);
                return;
            }

            UnicodeCategory category = char.GetUnicodeCategory(character);
            bool needsEscape = char.IsControl(character) || char.IsSurrogate(character) ||
                               category == UnicodeCategory.LineSeparator ||
                               category == UnicodeCategory.ParagraphSeparator ||
                               category == UnicodeCategory.Format;
            if (needsEscape)
            {
                builder.Append("\\u").Append(((int)character).ToString(UnicodeEscapeFormat, CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(character);
        }

        private static bool IsIdentifierStart(char character) =>
            character == Underscore || char.IsLetter(character);

        private static bool IsIdentifierPart(char character)
        {
            if (character == Underscore || char.IsLetterOrDigit(character)) return true;

            UnicodeCategory category = char.GetUnicodeCategory(character);
            return category == UnicodeCategory.NonSpacingMark ||
                   category == UnicodeCategory.SpacingCombiningMark ||
                   category == UnicodeCategory.ConnectorPunctuation;
        }
    }
}
