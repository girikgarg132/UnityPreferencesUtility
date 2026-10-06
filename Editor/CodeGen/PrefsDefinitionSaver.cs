using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Outcome of saving or regenerating a definition.</summary>
    public sealed class PrefsSaveResult
    {
        /// <summary>Analysis used for generation.</summary>
        public PrefsDefinitionAnalysis Analysis;

        /// <summary>True when the definition file content changed.</summary>
        public bool DefinitionChanged;

        /// <summary>True when code was generated (no errors).</summary>
        public bool CodeGenerated;

        /// <summary>True when the generated file content changed.</summary>
        public bool CodeChanged;

        /// <summary>I/O failure message, if any.</summary>
        public string IoError;

        /// <summary>True when the definition was written and code generated without errors.</summary>
        public bool Success => IoError == null && CodeGenerated;
    }

    /// <summary>Writes definition files and their generated code.</summary>
    public static class PrefsDefinitionSaver
    {
        private const string LogPrefix = "[Preferences Utility] ";

        /// <summary>
        /// Writes <paramref name="definition"/> to <paramref name="assetPath"/> and regenerates its C# file.
        /// The definition is always written, even when validation fails, so no edits are lost; code is only
        /// generated when there are no errors.
        /// </summary>
        /// <param name="assetPath">Project-relative path of the definition.</param>
        /// <param name="definition">Definition to write.</param>
        /// <param name="previousOutputPath">
        /// Output path used by the last generation. When it differs from the new one, the previously generated file
        /// is moved (keeping its GUID) or deleted so no duplicate class is left behind.
        /// </param>
        public static PrefsSaveResult Save(string assetPath, PrefsDefinition definition, string previousOutputPath = null)
        {
            PrefsSaveResult result = new PrefsSaveResult();
            try
            {
                result.DefinitionChanged = PrefsCodeGenerator.WriteIfChanged(assetPath, PrefsDefinitionSerializer.ToJson(definition));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                result.IoError = $"Could not write '{assetPath}': {exception.Message}";
                result.Analysis = PrefsDefinitionAnalyzer.Analyze(definition, assetPath, false);
                return result;
            }

            if (result.DefinitionChanged) PrefsDefinitionRegistry.Invalidate();
            GenerateInto(result, assetPath, definition, previousOutputPath);
            ImportChanged(result, assetPath);
            return result;
        }

        /// <summary>
        /// Keeps generated code in sync after a definition asset is moved or renamed: the previously generated file
        /// follows the definition (when it uses the default output path) and is regenerated.
        /// </summary>
        public static void HandleMoved(string oldAssetPath, string newAssetPath)
        {
            PrefsDefinition definition = PrefsDefinitionRegistry.TryLoad(newAssetPath);
            if (definition == null) return;

            string previousOutputPath = PrefsDefinitionPaths.GetEffectiveOutputPath(definition, oldAssetPath);
            if (!PrefsCodeGenerator.IsGeneratedFile(previousOutputPath)) return;

            PrefsSaveResult result = new PrefsSaveResult();
            GenerateInto(result, newAssetPath, definition, previousOutputPath);
            ImportChanged(result, newAssetPath);
            Log(result, newAssetPath);
        }

        /// <summary>Regenerates code for the definition stored at <paramref name="assetPath"/> without modifying it.</summary>
        public static PrefsSaveResult Regenerate(string assetPath)
        {
            PrefsSaveResult result = new PrefsSaveResult();
            PrefsDefinition definition;
            try
            {
                definition = PrefsDefinitionSerializer.Load(Path.GetFullPath(assetPath));
            }
            catch (Exception exception)
            {
                result.IoError = $"Could not read '{assetPath}': {exception.Message}";
                result.Analysis = new PrefsDefinitionAnalysis();
                return result;
            }

            GenerateInto(result, assetPath, definition, null);
            ImportChanged(result, assetPath);
            return result;
        }

        /// <summary>Logs a one-line summary of <paramref name="result"/> to the Console.</summary>
        public static void Log(PrefsSaveResult result, string assetPath)
        {
            UnityEngine.Object context = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (result.IoError != null)
            {
                Debug.LogError(LogPrefix + result.IoError, context);
                return;
            }

            if (!result.CodeGenerated)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(LogPrefix).Append("Code was not generated for '").Append(assetPath).Append("':");
                foreach (PrefsIssue issue in result.Analysis.Issues.Where(issue => issue.Severity == PrefsIssueSeverity.Error))
                {
                    builder.Append("\n- ");
                    if (issue.EntryIndex >= 0) builder.Append("Entry #").Append(issue.EntryIndex + 1).Append(": ");
                    builder.Append(issue.Message);
                }

                Debug.LogError(builder.ToString(), context);
                return;
            }

            string state = result.CodeChanged ? "Generated" : "Up to date";
            Debug.Log($"{LogPrefix}{state}: {result.Analysis.OutputPath}", context);
        }

        private static void GenerateInto(PrefsSaveResult result, string assetPath, PrefsDefinition definition,
            string previousOutputPath)
        {
            result.Analysis = PrefsDefinitionAnalyzer.Analyze(definition, assetPath, true);
            if (result.Analysis.HasErrors) return;

            RelocatePreviousOutput(previousOutputPath, result.Analysis.OutputPath);

            try
            {
                string code = PrefsCodeGenerator.Generate(definition, result.Analysis, assetPath);
                result.CodeChanged = PrefsCodeGenerator.WriteIfChanged(result.Analysis.OutputPath, code);
                result.CodeGenerated = true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                result.IoError = $"Could not write '{result.Analysis.OutputPath}': {exception.Message}";
            }
        }

        private static void RelocatePreviousOutput(string previousOutputPath, string newOutputPath)
        {
            if (string.IsNullOrEmpty(previousOutputPath)) return;
            if (string.Equals(previousOutputPath, newOutputPath, StringComparison.OrdinalIgnoreCase)) return;
            if (!PrefsCodeGenerator.IsGeneratedFile(previousOutputPath)) return;

            if (!File.Exists(Path.GetFullPath(newOutputPath)))
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(newOutputPath));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                string moveError = AssetDatabase.MoveAsset(previousOutputPath, newOutputPath);
                if (string.IsNullOrEmpty(moveError))
                {
                    Debug.Log($"{LogPrefix}Moved generated file '{previousOutputPath}' to '{newOutputPath}'.");
                    return;
                }

                Debug.LogWarning($"{LogPrefix}Could not move '{previousOutputPath}': {moveError}");
            }

            if (AssetDatabase.DeleteAsset(previousOutputPath))
            {
                Debug.Log($"{LogPrefix}Deleted previously generated file '{previousOutputPath}'.");
            }
        }

        private static void ImportChanged(PrefsSaveResult result, string assetPath)
        {
            if (!result.DefinitionChanged && !result.CodeChanged) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                if (result.DefinitionChanged) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                if (result.CodeChanged) AssetDatabase.ImportAsset(result.Analysis.OutputPath, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (result.CodeChanged) RequestRecompile();
        }

        /// <summary>
        /// Requests a script compilation so the regenerated accessors are usable immediately, even when the
        /// Editor's Auto Refresh is disabled. Unity coalesces repeated requests into a single compilation.
        /// </summary>
        private static void RequestRecompile() => CompilationPipeline.RequestScriptCompilation();
    }
}
