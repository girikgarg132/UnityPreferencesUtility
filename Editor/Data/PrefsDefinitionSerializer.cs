using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Reads and writes <c>.prefsdef</c> Json with stable, VCS-friendly formatting (LF line endings, trailing newline).</summary>
    public static class PrefsDefinitionSerializer
    {
        private const string LineFeed = "\n";

        private static readonly JsonSerializerSettings s_Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture
        };

        /// <summary>UTF-8 without BOM, used for every file this package writes.</summary>
        public static readonly Encoding FileEncoding = new UTF8Encoding(false);

        /// <summary>Serializes a definition to the canonical file text.</summary>
        public static string ToJson(PrefsDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            JsonSerializer serializer = JsonSerializer.Create(s_Settings);
            StringBuilder builder = new StringBuilder();
            using (StringWriter writer = new StringWriter(builder, CultureInfo.InvariantCulture) { NewLine = LineFeed })
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                serializer.Serialize(jsonWriter, definition);
            }

            builder.Append(LineFeed);
            return builder.ToString();
        }

        /// <summary>Parses file text. Empty text yields a new, empty definition. Throws <see cref="JsonException"/> on invalid Json.</summary>
        public static PrefsDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new PrefsDefinition();

            PrefsDefinition definition = JsonConvert.DeserializeObject<PrefsDefinition>(json, s_Settings) ?? new PrefsDefinition();
            if (definition.version > PrefsDefinition.CurrentVersion)
            {
                throw new JsonSerializationException(
                    $"The file was created by a newer version of Preferences Utility (format {definition.version}). Update the package to edit it.");
            }

            definition.Normalize();
            definition.version = PrefsDefinition.CurrentVersion;
            return definition;
        }

        /// <summary>Reads and parses a definition from a project-relative or absolute path.</summary>
        public static PrefsDefinition Load(string path) => FromJson(File.ReadAllText(path, FileEncoding));

        /// <summary>Creates a deep copy through serialization.</summary>
        public static PrefsDefinition Clone(PrefsDefinition definition) => FromJson(ToJson(definition));
    }
}
