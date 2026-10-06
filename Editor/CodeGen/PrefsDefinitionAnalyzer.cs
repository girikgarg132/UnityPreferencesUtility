using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Severity of an analysis issue.</summary>
    public enum PrefsIssueSeverity
    {
        /// <summary>Informational hint.</summary>
        Info,

        /// <summary>Code is generated, but something may be wrong.</summary>
        Warning,

        /// <summary>Code cannot be generated.</summary>
        Error
    }

    /// <summary>A validation message, optionally tied to an entry.</summary>
    public readonly struct PrefsIssue
    {
        /// <summary>Severity.</summary>
        public readonly PrefsIssueSeverity Severity;

        /// <summary>Entry index, or -1 for definition-level issues.</summary>
        public readonly int EntryIndex;

        /// <summary>Human-readable message.</summary>
        public readonly string Message;

        /// <summary>Creates an issue.</summary>
        public PrefsIssue(PrefsIssueSeverity severity, int entryIndex, string message)
        {
            Severity = severity;
            EntryIndex = entryIndex;
            Message = message;
        }
    }

    /// <summary>Resolved information about one entry.</summary>
    public sealed class PrefsEntryInfo
    {
        /// <summary>The entry.</summary>
        public PrefsEntry Entry;

        /// <summary>Its handler (null for unknown type ids).</summary>
        public PrefsValueHandler Handler;

        /// <summary>The value type (null when unresolved).</summary>
        public Type ValueType;

        /// <summary>Resolution error of the type name, if any.</summary>
        public string TypeError;

        /// <summary>Parsed default value.</summary>
        public object DefaultValue;

        /// <summary>Effective storage key.</summary>
        public string Key;

        /// <summary>C# type expression for generated code.</summary>
        public string TypeExpression;
    }

    /// <summary>Result of analyzing a definition.</summary>
    public sealed class PrefsDefinitionAnalysis
    {
        /// <summary>Per-entry information, index-aligned with the definition entries.</summary>
        public readonly List<PrefsEntryInfo> Entries = new List<PrefsEntryInfo>();

        /// <summary>All issues.</summary>
        public readonly List<PrefsIssue> Issues = new List<PrefsIssue>();

        /// <summary>Effective class name.</summary>
        public string ClassName;

        /// <summary>Effective output path.</summary>
        public string OutputPath;

        /// <summary>True when any issue is an error.</summary>
        public bool HasErrors => Issues.Any(issue => issue.Severity == PrefsIssueSeverity.Error);

        /// <summary>Number of issues with <paramref name="severity"/>.</summary>
        public int Count(PrefsIssueSeverity severity) => Issues.Count(issue => issue.Severity == severity);
    }

    /// <summary>Validates definitions and resolves entry types and defaults.</summary>
    public static class PrefsDefinitionAnalyzer
    {
        /// <summary>Member names the generated class declares itself.</summary>
        public static readonly IReadOnlyCollection<string> ReservedNames = new HashSet<string>
        {
            "Keys", "Defaults", "DefaultsJson", "AllKeys", "s_AllKeys", "HasKey", "DeleteKey", "DeleteAll", "Save",
            "Equals", "GetHashCode", "ToString", "GetType", "ReferenceEquals", "MemberwiseClone", "Finalize"
        };

        private const string AssetsRoot = "Assets/";
        private const string PackagesRoot = "Packages/";
        private const string CSharpExtension = ".cs";
        private const string EditorFolderSegment = "/Editor/";
        private const string UnityEditorNamespace = "UnityEditor";

        private static HashSet<string> s_EditorOnlyAssemblyNames;

        /// <summary>
        /// Analyzes <paramref name="definition"/>. When <paramref name="includeProjectChecks"/> is true, the output path is
        /// also checked against the file system and the other definitions in the project.
        /// </summary>
        public static PrefsDefinitionAnalysis Analyze(PrefsDefinition definition, string assetPath, bool includeProjectChecks)
        {
            PrefsDefinitionAnalysis analysis = new PrefsDefinitionAnalysis
            {
                ClassName = PrefsDefinitionPaths.GetEffectiveClassName(definition, assetPath),
                OutputPath = PrefsDefinitionPaths.GetEffectiveOutputPath(definition, assetPath)
            };

            ValidateDefinition(definition, assetPath, analysis, includeProjectChecks);
            ValidateEntries(definition, analysis);
            return analysis;
        }

        private static void ValidateDefinition(PrefsDefinition definition, string assetPath, PrefsDefinitionAnalysis analysis,
            bool includeProjectChecks)
        {
            if (!CSharpSyntax.IsValidIdentifier(analysis.ClassName))
            {
                analysis.Issues.Add(Error(-1, $"Class name '{analysis.ClassName}' is not a valid C# identifier."));
            }

            if (!CSharpSyntax.IsValidNamespace(definition.namespaceName.Trim()))
            {
                analysis.Issues.Add(Error(-1, $"Namespace '{definition.namespaceName}' is not valid."));
            }

            string outputPath = analysis.OutputPath;
            bool rootIsValid = outputPath.StartsWith(AssetsRoot, StringComparison.Ordinal) ||
                               outputPath.StartsWith(PackagesRoot, StringComparison.Ordinal);
            if (!rootIsValid)
            {
                analysis.Issues.Add(Error(-1, "Output path must be inside the project (start with 'Assets/' or 'Packages/')."));
            }
            else if (!outputPath.EndsWith(CSharpExtension, StringComparison.OrdinalIgnoreCase))
            {
                analysis.Issues.Add(Error(-1, "Output path must end with '.cs'."));
            }
            else if (outputPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                analysis.Issues.Add(Error(-1, "Output path contains invalid characters."));
            }
            else if (definition.storage == PrefsStorage.PlayerPrefs &&
                     ("/" + outputPath).IndexOf(EditorFolderSegment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                analysis.Issues.Add(Warning(-1,
                    "Output path is inside an 'Editor' folder, so runtime scripts and builds cannot use these PlayerPrefs accessors."));
            }

            if (definition.storage == PrefsStorage.EditorPrefs && string.IsNullOrEmpty(definition.keyPrefix))
            {
                analysis.Issues.Add(Info(-1,
                    "EditorPrefs are shared by every Unity project on this machine. Consider a key prefix such as 'MyTool.'."));
            }

            if (includeProjectChecks && rootIsValid) ValidateOutputFile(assetPath, analysis);
        }

        private static void ValidateOutputFile(string assetPath, PrefsDefinitionAnalysis analysis)
        {
            string outputPath = analysis.OutputPath;
            if (File.Exists(Path.GetFullPath(outputPath)) && !PrefsCodeGenerator.IsGeneratedFile(outputPath))
            {
                analysis.Issues.Add(Error(-1,
                    $"'{outputPath}' already exists and was not generated by Preferences Utility. Choose another output path."));
            }

            foreach (string otherPath in PrefsDefinitionRegistry.FindAllDefinitionPaths())
            {
                if (string.Equals(otherPath, assetPath, StringComparison.OrdinalIgnoreCase)) continue;

                PrefsDefinition other = PrefsDefinitionRegistry.TryLoad(otherPath);
                if (other == null) continue;

                string otherOutput = PrefsDefinitionPaths.GetEffectiveOutputPath(other, otherPath);
                if (string.Equals(otherOutput, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    analysis.Issues.Add(Error(-1, $"'{otherPath}' generates code to the same output path."));
                }
            }
        }

        private static void ValidateEntries(PrefsDefinition definition, PrefsDefinitionAnalysis analysis)
        {
            Dictionary<string, int> firstIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> firstIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            string contextNamespace = definition.namespaceName.Trim();

            for (int index = 0; index < definition.entries.Count; index++)
            {
                PrefsEntry entry = definition.entries[index];
                PrefsEntryInfo info = new PrefsEntryInfo
                {
                    Entry = entry,
                    Handler = PrefsValueHandlers.Get(entry.type),
                    Key = definition.GetEffectiveKey(entry)
                };
                analysis.Entries.Add(info);

                ValidateName(entry, index, analysis, firstIndexByName);
                ValidateKey(info, index, analysis, firstIndexByKey);

                if (info.Handler == null)
                {
                    analysis.Issues.Add(Error(index, $"Unknown type '{entry.type}'."));
                    continue;
                }

                ResolveType(info, definition.storage, contextNamespace, index, analysis);
                ResolveDefault(info, index, analysis);
            }
        }

        private static void ValidateName(PrefsEntry entry, int index, PrefsDefinitionAnalysis analysis,
            Dictionary<string, int> firstIndexByName)
        {
            string name = entry.name;
            if (string.IsNullOrEmpty(name))
            {
                analysis.Issues.Add(Error(index, "Name is empty."));
                return;
            }

            if (!CSharpSyntax.IsValidIdentifier(name))
            {
                analysis.Issues.Add(Error(index, $"'{name}' is not a valid C# property name."));
            }
            else if (ReservedNames.Contains(name))
            {
                analysis.Issues.Add(Error(index, $"'{name}' is reserved by the generated class."));
            }
            else if (name == analysis.ClassName)
            {
                analysis.Issues.Add(Error(index, "A property cannot have the same name as its class."));
            }

            if (firstIndexByName.TryGetValue(name, out int firstIndex))
            {
                analysis.Issues.Add(Error(index, $"Name '{name}' is already used by entry #{firstIndex + 1}."));
            }
            else
            {
                firstIndexByName.Add(name, index);
            }
        }

        private static void ValidateKey(PrefsEntryInfo info, int index, PrefsDefinitionAnalysis analysis,
            Dictionary<string, int> firstIndexByKey)
        {
            if (string.IsNullOrEmpty(info.Key))
            {
                analysis.Issues.Add(Error(index, "Key is empty."));
                return;
            }

            if (info.Key.Trim() != info.Key)
            {
                analysis.Issues.Add(Warning(index, "Key starts or ends with whitespace."));
            }

            if (firstIndexByKey.TryGetValue(info.Key, out int firstIndex))
            {
                analysis.Issues.Add(Error(index, $"Key '{info.Key}' is already used by entry #{firstIndex + 1}."));
            }
            else
            {
                firstIndexByKey.Add(info.Key, index);
            }
        }

        private static void ResolveType(PrefsEntryInfo info, PrefsStorage storage, string contextNamespace, int index,
            PrefsDefinitionAnalysis analysis)
        {
            PrefsValueHandler handler = info.Handler;
            string typeName = info.Entry.typeName.Trim();
            Type resolved = null;

            if (handler.UsesTypeName)
            {
                if (string.IsNullOrEmpty(typeName))
                {
                    analysis.Issues.Add(Error(index, $"{handler.DisplayName} entries need a type name."));
                    info.TypeExpression = string.Empty;
                    return;
                }

                TypeNameResolver.ResolveResult result = TypeNameResolver.Resolve(typeName, contextNamespace);
                resolved = result.Type;
                info.TypeError = result.Error;
                if (resolved == null)
                {
                    analysis.Issues.Add(Warning(index,
                        $"{result.Error} The name is emitted as written; it must compile in the generated file's assembly."));
                }
            }

            info.ValueType = handler.GetValueType(resolved);
            info.TypeExpression = handler.GetTypeExpression(info.ValueType, typeName);

            if (resolved == null) return;

            if (handler.Id == PrefsValueHandlers.EnumId && !resolved.IsEnum)
            {
                analysis.Issues.Add(Error(index, $"'{CSharpTypeName.GetReadable(resolved)}' is not an enum. Use 'Custom (Json)' instead."));
                return;
            }

            if (handler.IsJson) ValidateJsonType(resolved, index, analysis);

            if (storage == PrefsStorage.PlayerPrefs && IsEditorOnly(resolved))
            {
                analysis.Issues.Add(Warning(index,
                    $"'{CSharpTypeName.GetReadable(resolved)}' is Editor-only, so it is unavailable in player builds."));
            }
        }

        private static void ValidateJsonType(Type type, int index, PrefsDefinitionAnalysis analysis)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                analysis.Issues.Add(Error(index,
                    $"'{type.Name}' is a UnityEngine.Object. Assets and scene objects cannot be stored in preferences; store an id or path instead."));
                return;
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                analysis.Issues.Add(Error(index, "Delegates cannot be serialized."));
                return;
            }

            if (type.IsAbstract || type.IsInterface)
            {
                analysis.Issues.Add(Warning(index,
                    "Abstract types and interfaces cannot be deserialized (type-name handling is disabled for security). Use a concrete type."));
            }

            PrefsValueHandler builtIn = PrefsValueHandlers.FindBuiltIn(type);
            if (builtIn != null)
            {
                analysis.Issues.Add(Info(index,
                    $"'{CSharpTypeName.GetReadable(type)}' has native support. Select '{builtIn.DisplayName}' for faster, Json-free storage."));
            }
        }

        private static void ResolveDefault(PrefsEntryInfo info, int index, PrefsDefinitionAnalysis analysis)
        {
            if (info.Handler.TryParseText(info.Entry.defaultValue, info.ValueType, out object value, out string error))
            {
                info.DefaultValue = value;
                return;
            }

            info.DefaultValue = info.Handler.GetZeroValue(info.ValueType);
            analysis.Issues.Add(Error(index, $"Default value: {error}"));
        }

        private static bool IsEditorOnly(Type type)
        {
            foreach (Type part in Decompose(type))
            {
                if (part.Namespace != null && part.Namespace.StartsWith(UnityEditorNamespace, StringComparison.Ordinal)) return true;
                if (EditorOnlyAssemblyNames.Contains(part.Assembly.GetName().Name)) return true;
            }

            return false;
        }

        private static IEnumerable<Type> Decompose(Type type)
        {
            if (type == null) yield break;
            if (type.HasElementType)
            {
                foreach (Type element in Decompose(type.GetElementType())) yield return element;
                yield break;
            }

            yield return type;
            if (!type.IsGenericType) yield break;

            foreach (Type argument in type.GetGenericArguments())
            {
                foreach (Type part in Decompose(argument)) yield return part;
            }
        }

        private static HashSet<string> EditorOnlyAssemblyNames
        {
            get
            {
                if (s_EditorOnlyAssemblyNames != null) return s_EditorOnlyAssemblyNames;

                HashSet<string> playerAssemblies = new HashSet<string>(
                    CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(assembly => assembly.name));
                s_EditorOnlyAssemblyNames = new HashSet<string>(
                    CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                        .Select(assembly => assembly.name)
                        .Where(name => !playerAssemblies.Contains(name)));
                return s_EditorOnlyAssemblyNames;
            }
        }

        private static PrefsIssue Error(int index, string message) => new PrefsIssue(PrefsIssueSeverity.Error, index, message);

        private static PrefsIssue Warning(int index, string message) => new PrefsIssue(PrefsIssueSeverity.Warning, index, message);

        private static PrefsIssue Info(int index, string message) => new PrefsIssue(PrefsIssueSeverity.Info, index, message);
    }
}
