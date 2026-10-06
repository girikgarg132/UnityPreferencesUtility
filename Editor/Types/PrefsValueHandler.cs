using System;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Native slot a value type is stored in.</summary>
    public enum PrefsStorageSlot
    {
        /// <summary><c>GetInt</c>/<c>SetInt</c>.</summary>
        Int,

        /// <summary><c>GetFloat</c>/<c>SetFloat</c>.</summary>
        Float,

        /// <summary><c>GetString</c>/<c>SetString</c>.</summary>
        String,

        /// <summary>PlayerPrefs integer (0/1) or native <c>EditorPrefs.GetBool</c>.</summary>
        Bool
    }

    /// <summary>How the generated <c>Defaults</c> member is declared.</summary>
    public enum PrefsDefaultDeclaration
    {
        /// <summary><c>public const T Name = literal;</c></summary>
        Const,

        /// <summary><c>public static readonly T Name = expression;</c></summary>
        StaticReadonly,

        /// <summary>A property that deserializes a fresh instance from a Json constant.</summary>
        Json
    }

    /// <summary>State of a live value read from PlayerPrefs/EditorPrefs.</summary>
    public enum PrefsLiveState
    {
        /// <summary>The key is not set; generated properties return the default value.</summary>
        Missing,

        /// <summary>The key is set and readable.</summary>
        Valid,

        /// <summary>The key is stored with a different native kind (for example a string instead of an int).</summary>
        KindMismatch,

        /// <summary>The key is a string that cannot be parsed as the expected type.</summary>
        Malformed
    }

    /// <summary>Snapshot of a stored value as seen by editor tooling.</summary>
    public readonly struct PrefsLiveValue
    {
        /// <summary>Read state.</summary>
        public readonly PrefsLiveState State;

        /// <summary>Typed value when <see cref="State"/> is <see cref="PrefsLiveState.Valid"/>.</summary>
        public readonly object Value;

        /// <summary>Raw stored text for string-slot values (useful for malformed data).</summary>
        public readonly string Raw;

        /// <summary>Detected native kind.</summary>
        public readonly PrefsValueKind StoredKind;

        /// <summary>Creates a snapshot.</summary>
        public PrefsLiveValue(PrefsLiveState state, object value, string raw, PrefsValueKind storedKind)
        {
            State = state;
            Value = value;
            Raw = raw;
            StoredKind = storedKind;
        }
    }

    /// <summary>Tokens substituted into getter/setter templates.</summary>
    public readonly struct PrefsEmitContext
    {
        /// <summary>Fully qualified PlayerPrefs/EditorPrefs type.</summary>
        public readonly string PrefsType;

        /// <summary>Key expression, for example <c>Keys.Volume</c>.</summary>
        public readonly string KeyExpression;

        /// <summary>Default expression, for example <c>Defaults.Volume</c>.</summary>
        public readonly string DefaultExpression;

        /// <summary>Default Json expression, for example <c>DefaultsJson.Volume</c>.</summary>
        public readonly string DefaultJsonExpression;

        /// <summary>C# type expression of the property.</summary>
        public readonly string TypeExpression;

        /// <summary>Creates a context.</summary>
        public PrefsEmitContext(string prefsType, string keyExpression, string defaultExpression,
            string defaultJsonExpression, string typeExpression)
        {
            PrefsType = prefsType;
            KeyExpression = keyExpression;
            DefaultExpression = defaultExpression;
            DefaultJsonExpression = defaultJsonExpression;
            TypeExpression = typeExpression;
        }

        /// <summary>Replaces <c>{P}</c>, <c>{K}</c>, <c>{D}</c>, <c>{DJ}</c>, <c>{T}</c>, <c>{C}</c> and <c>{J}</c> in <paramref name="template"/>.</summary>
        public string Apply(string template) => template
            .Replace("{DJ}", DefaultJsonExpression)
            .Replace("{P}", PrefsType)
            .Replace("{K}", KeyExpression)
            .Replace("{D}", DefaultExpression)
            .Replace("{T}", TypeExpression)
            .Replace("{C}", PrefsCodeConstants.ConverterType)
            .Replace("{J}", PrefsCodeConstants.JsonType);
    }

    /// <summary>
    /// Describes one selectable value type: how its default is parsed and edited, how live values are read and written,
    /// and which C# is generated for it. Values are passed around as boxed objects in editor code only;
    /// generated runtime code is fully typed.
    /// </summary>
    public abstract class PrefsValueHandler
    {
        private const char MenuSeparator = '/';

        /// <summary>Creates a handler.</summary>
        protected PrefsValueHandler(string id, string menuPath)
        {
            Id = id;
            MenuPath = menuPath;
            int separatorIndex = menuPath.LastIndexOf(MenuSeparator);
            DisplayName = separatorIndex < 0 ? menuPath : menuPath.Substring(separatorIndex + 1);
        }

        /// <summary>Stable id stored in definition files.</summary>
        public string Id { get; }

        /// <summary>Path in the type menu (slashes create submenus).</summary>
        public string MenuPath { get; }

        /// <summary>Short display name.</summary>
        public string DisplayName { get; }

        /// <summary>True when the entry needs a C# type expression (<c>Enum</c>, <c>Json</c>).</summary>
        public virtual bool UsesTypeName => false;

        /// <summary>True when the value is Json text.</summary>
        public virtual bool IsJson => false;

        /// <summary>The value type for an entry. For type-name handlers, <paramref name="resolvedType"/> is the resolved type name (may be null).</summary>
        public abstract Type GetValueType(Type resolvedType);

        /// <summary>The native slot used for storage.</summary>
        public abstract PrefsStorageSlot GetSlot(Type valueType);

        /// <summary>Value used for new entries and empty default text.</summary>
        public abstract object GetZeroValue(Type valueType);

        /// <summary>Parses default-value text.</summary>
        public abstract bool TryParseText(string text, Type valueType, out object value, out string error);

        /// <summary>Formats a value as default-value text.</summary>
        public abstract string FormatText(object value, Type valueType);

        /// <summary>Draws an editable field (layout) and returns the possibly changed value.</summary>
        public abstract object DrawField(GUIContent label, object value, Type valueType);

        /// <summary>Short single-line preview.</summary>
        public virtual string Preview(object value, Type valueType) => FormatText(value, valueType);

        /// <summary>C# type expression to emit.</summary>
        public abstract string GetTypeExpression(Type valueType, string typeNameText);

        /// <summary>How the default member is declared.</summary>
        public abstract PrefsDefaultDeclaration GetDefaultDeclaration(Type valueType);

        /// <summary>C# expression (or Json literal for <see cref="PrefsDefaultDeclaration.Json"/>) for the default value.</summary>
        public abstract string EmitDefault(object value, Type valueType, string typeExpression, string defaultText);

        /// <summary>Getter expression.</summary>
        public abstract string EmitGetter(PrefsEmitContext context, Type valueType, PrefsStorage storage);

        /// <summary>Setter statement (without trailing semicolon).</summary>
        public abstract string EmitSetter(PrefsEmitContext context, Type valueType, PrefsStorage storage);

        /// <summary>Converts an int-slot value from storage.</summary>
        protected virtual object FromStoredInt(int stored, Type valueType) => stored;

        /// <summary>Converts a value to its int-slot representation.</summary>
        protected virtual int ToStoredInt(object value, Type valueType) => (int)value;

        /// <summary>Parses a string-slot value from storage.</summary>
        protected virtual bool TryFromStoredString(string stored, Type valueType, out object value) =>
            TryParseText(stored, valueType, out value, out _);

        /// <summary>Formats a value for string-slot storage.</summary>
        protected virtual string ToStoredString(object value, Type valueType) => FormatText(value, valueType);

        /// <summary>Reads the current stored value of <paramref name="key"/>.</summary>
        public PrefsLiveValue Read(IPrefsStore store, string key, Type valueType)
        {
            PrefsValueKind kind = PrefsStores.DetectKind(store, key);
            if (kind == PrefsValueKind.Missing) return new PrefsLiveValue(PrefsLiveState.Missing, null, null, kind);

            PrefsStorageSlot slot = GetSlot(valueType);
            switch (slot)
            {
                case PrefsStorageSlot.Int:
                    return kind == PrefsValueKind.Int
                        ? new PrefsLiveValue(PrefsLiveState.Valid, FromStoredInt(store.GetInt(key, 0), valueType), null, kind)
                        : new PrefsLiveValue(PrefsLiveState.KindMismatch, null, null, kind);
                case PrefsStorageSlot.Bool:
                    return kind == PrefsValueKind.Int
                        ? new PrefsLiveValue(PrefsLiveState.Valid, store.GetBool(key, false), null, kind)
                        : new PrefsLiveValue(PrefsLiveState.KindMismatch, null, null, kind);
                case PrefsStorageSlot.Float:
                    return kind == PrefsValueKind.Float
                        ? new PrefsLiveValue(PrefsLiveState.Valid, store.GetFloat(key, 0f), null, kind)
                        : new PrefsLiveValue(PrefsLiveState.KindMismatch, null, null, kind);
                default:
                    if (kind != PrefsValueKind.String) return new PrefsLiveValue(PrefsLiveState.KindMismatch, null, null, kind);

                    string raw = store.GetString(key, string.Empty);
                    return TryFromStoredString(raw, valueType, out object parsed)
                        ? new PrefsLiveValue(PrefsLiveState.Valid, parsed, raw, kind)
                        : new PrefsLiveValue(PrefsLiveState.Malformed, null, raw, kind);
            }
        }

        /// <summary>Writes <paramref name="value"/> to <paramref name="key"/> exactly as generated code would.</summary>
        public void Write(IPrefsStore store, string key, object value, Type valueType)
        {
            switch (GetSlot(valueType))
            {
                case PrefsStorageSlot.Int:
                    store.SetInt(key, ToStoredInt(value, valueType));
                    break;
                case PrefsStorageSlot.Bool:
                    store.SetBool(key, (bool)value);
                    break;
                case PrefsStorageSlot.Float:
                    store.SetFloat(key, (float)value);
                    break;
                default:
                    store.SetString(key, ToStoredString(value, valueType));
                    break;
            }

            store.Save();
        }
    }

    /// <summary>Fully qualified names used by generated code.</summary>
    public static class PrefsCodeConstants
    {
        /// <summary>Runtime converter type.</summary>
        public const string ConverterType = "global::GirikGarg.PreferencesUtility.PrefsConverter";

        /// <summary>Runtime Json bridge type.</summary>
        public const string JsonType = "global::GirikGarg.PreferencesUtility.PrefsJson";

        /// <summary>PlayerPrefs type.</summary>
        public const string PlayerPrefsType = "global::UnityEngine.PlayerPrefs";

        /// <summary>EditorPrefs type.</summary>
        public const string EditorPrefsType = "global::UnityEditor.EditorPrefs";
    }
}
