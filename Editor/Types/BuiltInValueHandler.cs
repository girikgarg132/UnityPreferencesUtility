using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Handler for a fixed, built-in type, configured with delegates and code templates.</summary>
    internal sealed class BuiltInValueHandler<T> : PrefsValueHandler
    {
        private readonly PrefsStorageSlot m_Slot;
        private readonly T m_Zero;
        private readonly T m_Alternate;
        private readonly Func<string, T, T> m_Parse;
        private readonly Func<T, string> m_Format;
        private readonly Func<GUIContent, T, T> m_Draw;
        private readonly Func<T, string> m_Literal;
        private readonly PrefsDefaultDeclaration m_Declaration;
        private readonly string m_Getter;
        private readonly string m_Setter;

        /// <summary>Creates a handler.</summary>
        /// <param name="id">Stable id stored in files.</param>
        /// <param name="menuPath">Type menu path.</param>
        /// <param name="slot">Native storage slot.</param>
        /// <param name="zero">Value for new entries.</param>
        /// <param name="alternate">A value different from <paramref name="zero"/>, used to detect parse failures.</param>
        /// <param name="parse">Parser returning the fallback on failure (normally a <see cref="PrefsConverter"/> method).</param>
        /// <param name="format">Invariant formatter (normally <see cref="PrefsConverter.Format(int)"/> overloads).</param>
        /// <param name="draw">IMGUI layout field.</param>
        /// <param name="literal">C# literal emitter.</param>
        /// <param name="declaration">Const or static readonly.</param>
        /// <param name="getter">Getter template.</param>
        /// <param name="setter">Setter template.</param>
        public BuiltInValueHandler(string id, string menuPath, PrefsStorageSlot slot, T zero, T alternate,
            Func<string, T, T> parse, Func<T, string> format, Func<GUIContent, T, T> draw, Func<T, string> literal,
            PrefsDefaultDeclaration declaration, string getter, string setter) : base(id, menuPath)
        {
            m_Slot = slot;
            m_Zero = zero;
            m_Alternate = alternate;
            m_Parse = parse;
            m_Format = format;
            m_Draw = draw;
            m_Literal = literal;
            m_Declaration = declaration;
            m_Getter = getter;
            m_Setter = setter;
        }

        /// <summary>Getter template used for EditorPrefs (defaults to the PlayerPrefs template).</summary>
        public string EditorGetter { get; set; }

        /// <summary>Setter template used for EditorPrefs (defaults to the PlayerPrefs template).</summary>
        public string EditorSetter { get; set; }

        /// <summary>Converts a stored int to <typeparamref name="T"/> (int slot only).</summary>
        public Func<int, T> FromInt { get; set; }

        /// <summary>Converts <typeparamref name="T"/> to a stored int (int slot only).</summary>
        public Func<T, int> ToInt { get; set; }

        /// <summary>The CLR type.</summary>
        public Type ClrType => typeof(T);

        public override Type GetValueType(Type resolvedType) => typeof(T);

        public override PrefsStorageSlot GetSlot(Type valueType) => m_Slot;

        public override object GetZeroValue(Type valueType) => m_Zero;

        public override bool TryParseText(string text, Type valueType, out object value, out string error)
        {
            if (string.IsNullOrEmpty(text))
            {
                value = typeof(T) == typeof(string) ? string.Empty : (object)m_Zero;
                error = null;
                return true;
            }

            T first = m_Parse(text, m_Zero);
            T second = m_Parse(text, m_Alternate);
            if (EqualityComparer<T>.Default.Equals(first, second))
            {
                value = first;
                error = null;
                return true;
            }

            value = m_Zero;
            error = $"'{text}' is not a valid {DisplayName}.";
            return false;
        }

        public override string FormatText(object value, Type valueType) => m_Format(Cast(value));

        public override object DrawField(GUIContent label, object value, Type valueType) => m_Draw(label, Cast(value));

        public override string GetTypeExpression(Type valueType, string typeNameText) => CSharpTypeName.GetQualified(typeof(T));

        public override PrefsDefaultDeclaration GetDefaultDeclaration(Type valueType) => m_Declaration;

        public override string EmitDefault(object value, Type valueType, string typeExpression, string defaultText) =>
            m_Literal(Cast(value));

        public override string EmitGetter(PrefsEmitContext context, Type valueType, PrefsStorage storage) =>
            context.Apply(storage == PrefsStorage.EditorPrefs && EditorGetter != null ? EditorGetter : m_Getter);

        public override string EmitSetter(PrefsEmitContext context, Type valueType, PrefsStorage storage) =>
            context.Apply(storage == PrefsStorage.EditorPrefs && EditorSetter != null ? EditorSetter : m_Setter);

        protected override object FromStoredInt(int stored, Type valueType) =>
            FromInt != null ? FromInt(stored) : (object)stored;

        protected override int ToStoredInt(object value, Type valueType) =>
            ToInt != null ? ToInt(Cast(value)) : (int)value;

        private T Cast(object value) => value is T typed ? typed : m_Zero;
    }

    /// <summary>IMGUI helpers for types without a dedicated EditorGUILayout field.</summary>
    internal static class PrefsFields
    {
        /// <summary>Delayed text field that only commits text that parses successfully.</summary>
        public static T TextParsed<T>(GUIContent label, T value, Func<T, string> format, Func<string, T, T> parse,
            T alternate)
        {
            string current = format(value);
            string edited = EditorGUILayout.DelayedTextField(label, current);
            if (edited == current) return value;

            T first = parse(edited, value);
            T second = parse(edited, alternate);
            return EqualityComparer<T>.Default.Equals(first, second) ? first : value;
        }

        /// <summary>Int field clamped to a range.</summary>
        public static int ClampedInt(GUIContent label, int value, int min, int max) =>
            Mathf.Clamp(EditorGUILayout.IntField(label, value), min, max);

        /// <summary>Single-character field.</summary>
        public static char Char(GUIContent label, char value)
        {
            string edited = EditorGUILayout.TextField(label, value == '\0' ? string.Empty : value.ToString());
            return string.IsNullOrEmpty(edited) ? '\0' : edited[edited.Length - 1];
        }

        /// <summary>Quaternion edited as Euler angles.</summary>
        public static Quaternion Rotation(GUIContent label, Quaternion value)
        {
            Quaternion safe = IsValid(value) ? value : Quaternion.identity;
            Vector3 euler = safe.eulerAngles;
            EditorGUI.BeginChangeCheck();
            Vector3 edited = EditorGUILayout.Vector3Field(label, euler);
            return EditorGUI.EndChangeCheck() ? Quaternion.Euler(edited) : value;
        }

        private static bool IsValid(Quaternion value) =>
            !(Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f) &&
              Mathf.Approximately(value.z, 0f) && Mathf.Approximately(value.w, 0f));
    }
}
