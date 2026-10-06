using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Any enum type. Enums with an int-compatible underlying type (including <c>uint</c>) use one <c>GetInt</c> call;
    /// <c>long</c>/<c>ulong</c> enums are stored as invariant numeric strings. Defaults are stored by name, so reordering
    /// enum members never changes a default silently.
    /// </summary>
    internal sealed class EnumValueHandler : PrefsValueHandler
    {
        private const string FlagSeparator = ", ";
        private const string EmittedFlagSeparator = " | ";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public EnumValueHandler() : base(PrefsValueHandlers.EnumId, "Enum") { }

        public override bool UsesTypeName => true;

        public override Type GetValueType(Type resolvedType) => resolvedType != null && resolvedType.IsEnum ? resolvedType : null;

        public override PrefsStorageSlot GetSlot(Type valueType) =>
            IsWide(valueType) ? PrefsStorageSlot.String : PrefsStorageSlot.Int;

        public override object GetZeroValue(Type valueType) => valueType != null ? Enum.ToObject(valueType, 0) : (object)string.Empty;

        public override bool TryParseText(string text, Type valueType, out object value, out string error)
        {
            error = null;
            if (valueType == null)
            {
                value = text ?? string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                value = GetZeroValue(valueType);
                return true;
            }

            try
            {
                value = Enum.Parse(valueType, text.Trim(), false);
                return true;
            }
            catch (ArgumentException)
            {
                value = GetZeroValue(valueType);
                error = $"'{text}' is not a member of {valueType.Name}.";
                return false;
            }
            catch (OverflowException)
            {
                value = GetZeroValue(valueType);
                error = $"'{text}' is out of range for {valueType.Name}.";
                return false;
            }
        }

        public override string FormatText(object value, Type valueType) => value?.ToString() ?? string.Empty;

        public override object DrawField(GUIContent label, object value, Type valueType)
        {
            if (valueType == null || !(value is Enum enumValue))
            {
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            }

            return valueType.IsDefined(typeof(FlagsAttribute), false)
                ? EditorGUILayout.EnumFlagsField(label, enumValue)
                : EditorGUILayout.EnumPopup(label, enumValue);
        }

        public override string GetTypeExpression(Type valueType, string typeNameText) =>
            valueType != null ? CSharpTypeName.GetQualified(valueType) : typeNameText.Trim();

        public override PrefsDefaultDeclaration GetDefaultDeclaration(Type valueType) => PrefsDefaultDeclaration.Const;

        public override string EmitDefault(object value, Type valueType, string typeExpression, string defaultText)
        {
            if (valueType == null)
            {
                string trimmed = (defaultText ?? string.Empty).Trim();
                if (CSharpSyntax.IsValidIdentifier(trimmed)) return typeExpression + "." + trimmed;
                return long.TryParse(trimmed, NumberStyles.Integer, Invariant, out long number)
                    ? $"({typeExpression})({number.ToString(Invariant)})"
                    : $"default({typeExpression})";
            }

            string formatted = value.ToString();
            string[] parts = formatted.Split(new[] { FlagSeparator }, StringSplitOptions.None);
            bool allNamed = true;
            foreach (string part in parts)
            {
                if (!CSharpSyntax.IsValidIdentifier(part) && !CSharpSyntax.IsKeyword(part))
                {
                    allNamed = false;
                    break;
                }
            }

            if (allNamed)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    string memberName = CSharpSyntax.IsKeyword(parts[i]) ? "@" + parts[i] : parts[i];
                    parts[i] = typeExpression + "." + memberName;
                }

                return string.Join(EmittedFlagSeparator, parts);
            }

            string numeric = IsUnsigned(valueType)
                ? Convert.ToUInt64(value, Invariant).ToString(Invariant)
                : Convert.ToInt64(value, Invariant).ToString(Invariant);
            return $"({typeExpression})({numeric})";
        }

        public override string EmitGetter(PrefsEmitContext context, Type valueType, PrefsStorage storage)
        {
            Type underlying = valueType != null ? Enum.GetUnderlyingType(valueType) : typeof(int);
            if (underlying == typeof(long)) return context.Apply("({T}){C}.ToInt64({P}.GetString({K}, string.Empty), (long){D})");
            if (underlying == typeof(ulong)) return context.Apply("({T}){C}.ToUInt64({P}.GetString({K}, string.Empty), (ulong){D})");
            if (underlying == typeof(uint)) return context.Apply("({T})unchecked((uint){P}.GetInt({K}, unchecked((int){D})))");
            return context.Apply("({T}){P}.GetInt({K}, (int){D})");
        }

        public override string EmitSetter(PrefsEmitContext context, Type valueType, PrefsStorage storage)
        {
            Type underlying = valueType != null ? Enum.GetUnderlyingType(valueType) : typeof(int);
            if (underlying == typeof(long)) return context.Apply("{P}.SetString({K}, {C}.Format((long)value))");
            if (underlying == typeof(ulong)) return context.Apply("{P}.SetString({K}, {C}.Format((ulong)value))");
            if (underlying == typeof(uint)) return context.Apply("{P}.SetInt({K}, unchecked((int)value))");
            return context.Apply("{P}.SetInt({K}, (int)value)");
        }

        protected override object FromStoredInt(int stored, Type valueType)
        {
            if (valueType == null) return stored.ToString(Invariant);
            return Enum.GetUnderlyingType(valueType) == typeof(uint)
                ? Enum.ToObject(valueType, unchecked((uint)stored))
                : Enum.ToObject(valueType, stored);
        }

        protected override int ToStoredInt(object value, Type valueType)
        {
            if (!(value is Enum)) return 0;
            return IsUnsigned(valueType)
                ? unchecked((int)Convert.ToUInt64(value, Invariant))
                : unchecked((int)Convert.ToInt64(value, Invariant));
        }

        protected override bool TryFromStoredString(string stored, Type valueType, out object value)
        {
            value = null;
            if (valueType == null) return false;

            if (IsUnsigned(valueType))
            {
                if (!ulong.TryParse(stored, NumberStyles.Integer, Invariant, out ulong unsignedNumber)) return false;
                value = Enum.ToObject(valueType, unsignedNumber);
                return true;
            }

            if (!long.TryParse(stored, NumberStyles.Integer, Invariant, out long number)) return false;
            value = Enum.ToObject(valueType, number);
            return true;
        }

        protected override string ToStoredString(object value, Type valueType) =>
            IsUnsigned(valueType)
                ? Convert.ToUInt64(value, Invariant).ToString(Invariant)
                : Convert.ToInt64(value, Invariant).ToString(Invariant);

        private static bool IsWide(Type valueType)
        {
            if (valueType == null) return false;
            Type underlying = Enum.GetUnderlyingType(valueType);
            return underlying == typeof(long) || underlying == typeof(ulong);
        }

        private static bool IsUnsigned(Type valueType)
        {
            if (valueType == null) return false;
            Type underlying = Enum.GetUnderlyingType(valueType);
            return underlying == typeof(byte) || underlying == typeof(ushort) || underlying == typeof(uint) ||
                   underlying == typeof(ulong);
        }
    }
}
