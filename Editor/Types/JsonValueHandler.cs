using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Any other type (classes, structs, collections, nullables, ...), serialized with Newtonsoft Json through
    /// <see cref="PrefsJson"/>. In editor code the value is the Json text itself, so unresolved types remain editable.
    /// </summary>
    internal sealed class JsonValueHandler : PrefsValueHandler
    {
        private const int PreviewLength = 80;
        private const string Ellipsis = "…";
        private const float TextAreaMinHeight = 60f;

        public JsonValueHandler() : base(PrefsValueHandlers.JsonId, "Custom (Json)") { }

        public override bool UsesTypeName => true;

        public override bool IsJson => true;

        public override Type GetValueType(Type resolvedType) => resolvedType;

        public override PrefsStorageSlot GetSlot(Type valueType) => PrefsStorageSlot.String;

        public override object GetZeroValue(Type valueType) => string.Empty;

        public override bool TryParseText(string text, Type valueType, out object value, out string error)
        {
            value = text ?? string.Empty;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            try
            {
                if (valueType != null) PrefsJson.DeserializeStrict(text, valueType);
                else JToken.Parse(text);
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                                              exception is InvalidCastException || exception is FormatException)
            {
                error = $"Invalid Json: {exception.Message}";
                return false;
            }
        }

        public override string FormatText(object value, Type valueType) => value as string ?? string.Empty;

        public override object DrawField(GUIContent label, object value, Type valueType)
        {
            EditorGUILayout.LabelField(label);
            return EditorGUILayout.TextArea(value as string ?? string.Empty, EditorStyles.textArea,
                GUILayout.MinHeight(TextAreaMinHeight), GUILayout.ExpandWidth(true));
        }

        public override string Preview(object value, Type valueType)
        {
            string text = (value as string ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            if (string.IsNullOrEmpty(text)) return "default";
            return text.Length > PreviewLength ? text.Substring(0, PreviewLength) + Ellipsis : text;
        }

        public override string GetTypeExpression(Type valueType, string typeNameText) =>
            valueType != null ? CSharpTypeName.GetQualified(valueType) : typeNameText.Trim();

        public override PrefsDefaultDeclaration GetDefaultDeclaration(Type valueType) => PrefsDefaultDeclaration.Json;

        public override string EmitDefault(object value, Type valueType, string typeExpression, string defaultText)
        {
            string json = defaultText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json)) return CSharpSyntax.StringLiteral(string.Empty);

            try
            {
                json = JToken.Parse(json).ToString(Formatting.None);
            }
            catch (JsonException)
            {
                json = json.Trim();
            }

            return CSharpSyntax.StringLiteral(json);
        }

        public override string EmitGetter(PrefsEmitContext context, Type valueType, PrefsStorage storage) =>
            context.Apply("{J}.Read<{T}>({P}.GetString({K}, string.Empty), {DJ}, {K})");

        public override string EmitSetter(PrefsEmitContext context, Type valueType, PrefsStorage storage) =>
            context.Apply("{P}.SetString({K}, {J}.Serialize<{T}>(value))");

        protected override bool TryFromStoredString(string stored, Type valueType, out object value)
        {
            value = stored ?? string.Empty;
            if (string.IsNullOrEmpty(stored)) return true;
            return TryParseText(stored, valueType, out value, out _);
        }

        protected override string ToStoredString(object value, Type valueType) => value as string ?? string.Empty;

        /// <summary>Pretty-prints Json text. Returns the input unchanged when it is not valid Json.</summary>
        public static string Indent(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            try
            {
                return JToken.Parse(json).ToString(Formatting.Indented).Replace("\r\n", "\n");
            }
            catch (JsonException)
            {
                return json;
            }
        }

        /// <summary>Creates Json for a new instance of <paramref name="type"/> (public fields/properties with their initial values).</summary>
        public static bool TryCreateTemplate(Type type, out string json, out string error)
        {
            json = null;
            error = null;
            if (type == null)
            {
                error = "Resolve the type first.";
                return false;
            }

            try
            {
                object instance = CreateInstance(type);
                json = Indent(PrefsJson.Serialize(instance, type, false));
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not create an instance of {CSharpTypeName.GetReadable(type)}: {exception.Message}";
                return false;
            }
        }

        private static object CreateInstance(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            if (type.IsArray) return Array.CreateInstance(type.GetElementType(), new int[type.GetArrayRank()]);
            if (Nullable.GetUnderlyingType(type) != null) return null;
            if (type.IsValueType) return Activator.CreateInstance(type);
            if (type.IsAbstract || type.IsInterface)
            {
                throw new InvalidOperationException("Abstract types and interfaces cannot be instantiated without type-name handling.");
            }

            return Activator.CreateInstance(type, true);
        }
    }
}
