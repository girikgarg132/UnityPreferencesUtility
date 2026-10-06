using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Where the generated accessors store their values.</summary>
    public enum PrefsStorage
    {
        /// <summary><see cref="UnityEngine.PlayerPrefs"/>: per product, available in builds.</summary>
        PlayerPrefs = 0,

        /// <summary><see cref="UnityEditor.EditorPrefs"/>: per machine, Editor only (generated code is wrapped in <c>#if UNITY_EDITOR</c>).</summary>
        EditorPrefs = 1
    }

    /// <summary>Accessibility of the generated class.</summary>
    public enum PrefsAccessModifier
    {
        /// <summary>Generates a <c>public</c> class.</summary>
        Public = 0,

        /// <summary>Generates an <c>internal</c> class.</summary>
        Internal = 1
    }

    /// <summary>
    /// The content of a <c>.prefsdef</c> file. Serialized as indented Json so it diffs and merges cleanly in version control.
    /// Empty optional strings mean "use the default derived from the asset path".
    /// </summary>
    [Serializable]
    public sealed class PrefsDefinition
    {
        /// <summary>Current file-format version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>File-format version, used for future migrations.</summary>
        [JsonProperty("version", Order = 0)]
        public int version = CurrentVersion;

        /// <summary>PlayerPrefs or EditorPrefs.</summary>
        [JsonProperty("storage", Order = 1)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PrefsStorage storage = PrefsStorage.PlayerPrefs;

        /// <summary>Generated class name. Empty uses the asset file name.</summary>
        [JsonProperty("className", Order = 2)]
        public string className = string.Empty;

        /// <summary>Namespace of the generated class. Empty means the global namespace.</summary>
        [JsonProperty("namespace", Order = 3)]
        public string namespaceName = string.Empty;

        /// <summary>Project-relative path of the generated .cs file. Empty means "next to the asset, named after the class".</summary>
        [JsonProperty("outputPath", Order = 4)]
        public string outputPath = string.Empty;

        /// <summary>Text prepended to every key. Recommended for EditorPrefs, which are shared by all projects.</summary>
        [JsonProperty("keyPrefix", Order = 5)]
        public string keyPrefix = string.Empty;

        /// <summary>Accessibility of the generated class.</summary>
        [JsonProperty("accessModifier", Order = 6)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PrefsAccessModifier accessModifier = PrefsAccessModifier.Public;

        /// <summary>Preference entries, in declaration order.</summary>
        [JsonProperty("entries", Order = 7)]
        public List<PrefsEntry> entries = new List<PrefsEntry>();

        /// <summary>Replaces <c>null</c> members (for example from hand-edited files) with safe empty values.</summary>
        public void Normalize()
        {
            className ??= string.Empty;
            namespaceName ??= string.Empty;
            outputPath ??= string.Empty;
            keyPrefix ??= string.Empty;
            entries ??= new List<PrefsEntry>();
            entries.RemoveAll(entry => entry == null);
            foreach (PrefsEntry entry in entries)
            {
                entry.Normalize();
            }
        }

        /// <summary>Returns the storage key for <paramref name="entry"/> (prefix + key, or prefix + name when the key is empty).</summary>
        public string GetEffectiveKey(PrefsEntry entry)
        {
            string localKey = string.IsNullOrEmpty(entry.key) ? entry.name : entry.key;
            return keyPrefix + localKey;
        }
    }

    /// <summary>One preference: a generated static property backed by a single key.</summary>
    [Serializable]
    public sealed class PrefsEntry
    {
        /// <summary>Generated property name.</summary>
        [JsonProperty("name", Order = 0)]
        public string name = string.Empty;

        /// <summary>Storage key without the definition prefix. Empty uses <see cref="name"/>.</summary>
        [JsonProperty("key", Order = 1)]
        public string key = string.Empty;

        /// <summary>Value type id (see <see cref="PrefsValueHandlers"/>), for example <c>bool</c>, <c>Vector3</c>, <c>Enum</c> or <c>Json</c>.</summary>
        [JsonProperty("type", Order = 2)]
        public string type = PrefsValueHandlers.DefaultId;

        /// <summary>C# type expression for <c>Enum</c> and <c>Json</c> types, for example <c>MyGame.Difficulty</c> or <c>List&lt;int&gt;</c>.</summary>
        [JsonProperty("typeName", Order = 3)]
        public string typeName = string.Empty;

        /// <summary>Default value as culture-invariant text (Json for <c>Json</c> types).</summary>
        [JsonProperty("defaultValue", Order = 4)]
        public string defaultValue = string.Empty;

        /// <summary>Optional XML documentation for the generated property.</summary>
        [JsonProperty("summary", Order = 5)]
        public string summary = string.Empty;

        /// <summary>Replaces <c>null</c> members with empty values.</summary>
        public void Normalize()
        {
            name ??= string.Empty;
            key ??= string.Empty;
            type = string.IsNullOrEmpty(type) ? PrefsValueHandlers.DefaultId : type;
            typeName ??= string.Empty;
            defaultValue ??= string.Empty;
            summary ??= string.Empty;
        }

        /// <summary>Creates a deep copy.</summary>
        public PrefsEntry Clone() => (PrefsEntry)MemberwiseClone();
    }
}
