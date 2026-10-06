using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace GirikGarg.PreferencesUtility
{
    /// <summary>
    /// Newtonsoft Json bridge used by generated accessors for types that have no native preference representation
    /// (classes, structs, collections, nullables, ...). A single <see cref="JsonSerializer"/> is cached and reused,
    /// and failures never throw: a malformed or incompatible stored value falls back to the default value.
    /// </summary>
    public static class PrefsJson
    {
        private const int StringBuilderCapacity = 256;
        private const string LogPrefix = "[Preferences Utility] ";

        private static JsonSerializerSettings s_Settings;
        private static JsonSerializer s_Serializer;

        /// <summary>
        /// Settings used for every preference serialization. Assign a new instance (for example in a
        /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]</c> method)
        /// to add converters or change behavior. Changing the settings may make previously stored data unreadable.
        /// </summary>
        public static JsonSerializerSettings Settings
        {
            get => s_Settings ??= CreateDefaultSettings();
            set
            {
                s_Settings = value ?? CreateDefaultSettings();
                s_Serializer = null;
            }
        }

        /// <summary>
        /// When true (default), deserialization failures are logged as warnings. Values still fall back to defaults.
        /// </summary>
        public static bool LogFailures { get; set; } = true;

        private static JsonSerializer Serializer => s_Serializer ??= JsonSerializer.Create(Settings);

        /// <summary>
        /// Creates the default settings: no type-name handling (safe against payload injection),
        /// <see cref="ObjectCreationHandling.Replace"/> (collections initialized in constructors are not duplicated),
        /// and converters for common Unity structs.
        /// </summary>
        public static JsonSerializerSettings CreateDefaultSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                Culture = CultureInfo.InvariantCulture,
                Formatting = Formatting.None
            };
            UnityJsonConverters.AddTo(settings.Converters);
            return settings;
        }

        /// <summary>Serializes <paramref name="value"/> to compact Json.</summary>
        public static string Serialize<T>(T value)
        {
            StringBuilder builder = new StringBuilder(StringBuilderCapacity);
            using (StringWriter stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = Formatting.None;
                Serializer.Serialize(jsonWriter, value, typeof(T));
            }

            return builder.ToString();
        }

        /// <summary>Serializes <paramref name="value"/> as <paramref name="type"/>, optionally indented. Used by editor tooling.</summary>
        public static string Serialize(object value, Type type, bool indented)
        {
            StringBuilder builder = new StringBuilder(StringBuilderCapacity);
            using (StringWriter stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = indented ? Formatting.Indented : Formatting.None;
                Serializer.Serialize(jsonWriter, value, type);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Deserializes <paramref name="json"/> as <paramref name="type"/>. Throws on invalid input; used by editor tooling for validation.
        /// </summary>
        public static object DeserializeStrict(string json, Type type)
        {
            using (StringReader stringReader = new StringReader(json))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                object value = Serializer.Deserialize(jsonReader, type);
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType != JsonToken.Comment)
                    {
                        throw new JsonReaderException("Additional text found after the end of the Json value.");
                    }
                }

                return value;
            }
        }

        /// <summary>
        /// Reads a stored Json value. Returns the value described by <paramref name="defaultJson"/> (or <c>default</c>)
        /// when <paramref name="storedJson"/> is empty or cannot be deserialized.
        /// A fresh instance is created on every call, so callers may mutate the result safely.
        /// </summary>
        /// <param name="storedJson">Raw stored text. Empty means "not set".</param>
        /// <param name="defaultJson">Json for the default value. Empty means <c>default(T)</c>.</param>
        /// <param name="key">Preference key, used only in warning messages.</param>
        public static T Read<T>(string storedJson, string defaultJson, string key)
        {
            if (!string.IsNullOrEmpty(storedJson) && TryDeserialize(storedJson, key, out T stored))
            {
                return stored;
            }

            return ReadDefault<T>(defaultJson, key);
        }

        /// <summary>Creates a fresh instance of a default value from its Json. Returns <c>default</c> for empty or invalid Json.</summary>
        public static T ReadDefault<T>(string defaultJson, string key)
        {
            if (string.IsNullOrEmpty(defaultJson)) return default;
            return TryDeserialize(defaultJson, key, out T value) ? value : default;
        }

        private static bool TryDeserialize<T>(string json, string key, out T value)
        {
            try
            {
                using (StringReader stringReader = new StringReader(json))
                using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
                {
                    value = Serializer.Deserialize<T>(jsonReader);
                }

                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is InvalidCastException || exception is ArgumentException)
            {
                if (LogFailures)
                {
                    Debug.LogWarning($"{LogPrefix}Could not read preference '{key}' as {typeof(T).Name}. " +
                                     $"Falling back to the default value. {exception.Message}");
                }

                value = default;
                return false;
            }
        }
    }
}
