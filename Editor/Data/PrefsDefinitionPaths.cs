using System.IO;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Resolves the effective class name and output path of a definition (explicit value or asset-derived default).</summary>
    public static class PrefsDefinitionPaths
    {
        /// <summary>File extension of definition assets, without the dot.</summary>
        public const string Extension = "prefsdef";

        /// <summary>File extension of definition assets, with the dot.</summary>
        public const string DottedExtension = "." + Extension;

        private const string CSharpExtension = ".cs";
        private const char PathSeparator = '/';

        /// <summary>Returns true if <paramref name="assetPath"/> points to a definition asset.</summary>
        public static bool IsDefinitionPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.EndsWith(DottedExtension, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Class name derived from the asset file name.</summary>
        public static string GetDefaultClassName(string assetPath) =>
            CSharpSyntax.ToIdentifier(Path.GetFileNameWithoutExtension(assetPath ?? string.Empty));

        /// <summary>Explicit class name, or the asset-derived default.</summary>
        public static string GetEffectiveClassName(PrefsDefinition definition, string assetPath) =>
            string.IsNullOrWhiteSpace(definition.className) ? GetDefaultClassName(assetPath) : definition.className.Trim();

        /// <summary>Default output path: next to the asset, named after the effective class.</summary>
        public static string GetDefaultOutputPath(PrefsDefinition definition, string assetPath)
        {
            string directory = NormalizeSeparators(Path.GetDirectoryName(assetPath ?? string.Empty));
            string fileName = GetEffectiveClassName(definition, assetPath) + CSharpExtension;
            return string.IsNullOrEmpty(directory) ? fileName : directory + PathSeparator + fileName;
        }

        /// <summary>Explicit output path, or the default.</summary>
        public static string GetEffectiveOutputPath(PrefsDefinition definition, string assetPath) =>
            string.IsNullOrWhiteSpace(definition.outputPath)
                ? GetDefaultOutputPath(definition, assetPath)
                : NormalizeSeparators(definition.outputPath.Trim());

        /// <summary>Converts back slashes to forward slashes.</summary>
        public static string NormalizeSeparators(string path) => (path ?? string.Empty).Replace('\\', PathSeparator);
    }
}
