using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Searchable dropdown listing enum types, or serializable types from the project's script assemblies.</summary>
    internal sealed class TypePickerDropdown : AdvancedDropdown
    {
        private const float MinimumWidth = 320f;
        private const float MinimumHeight = 400f;
        private const string GlobalNamespaceLabel = "(global namespace)";
        private const string CommonFolderLabel = "Common";
        private const string UnityEditorNamespace = "UnityEditor";

        private static readonly string[] s_CommonJsonTypes =
        {
            "List<string>", "List<int>", "List<float>", "List<bool>", "List<Vector3>",
            "string[]", "int[]", "float[]", "byte[]",
            "Dictionary<string, string>", "Dictionary<string, int>", "Dictionary<string, float>", "Dictionary<string, bool>",
            "HashSet<string>", "HashSet<int>", "int?", "float?", "bool?", "DateTime?"
        };

        private static readonly string[] s_ExcludedNamespacePrefixes =
        {
            "UnityEditorInternal", "UnityEngineInternal", "Internal", "Mono", "Microsoft", "JetBrains", "Bee", "NiceIO",
            "Unity.Burst.Editor", "TMPro.EditorUtilities", "System.Runtime", "System.Reflection", "System.Security"
        };

        private static List<Type> s_EnumTypes;
        private static List<Type> s_PlayerJsonTypes;
        private static List<Type> s_EditorJsonTypes;

        private readonly bool m_EnumsOnly;
        private readonly bool m_IncludeEditorTypes;
        private readonly Action<string> m_OnSelected;

        private sealed class TypeItem : AdvancedDropdownItem
        {
            public readonly string TypeName;

            public TypeItem(string displayName, string typeName) : base(displayName)
            {
                TypeName = typeName;
            }
        }

        /// <summary>Creates a picker.</summary>
        /// <param name="enumsOnly">List enums instead of Json-serializable types.</param>
        /// <param name="includeEditorTypes">Include Editor-only types (for EditorPrefs definitions).</param>
        /// <param name="onSelected">Receives the readable type name.</param>
        public TypePickerDropdown(bool enumsOnly, bool includeEditorTypes, Action<string> onSelected)
            : base(new AdvancedDropdownState())
        {
            m_EnumsOnly = enumsOnly;
            m_IncludeEditorTypes = includeEditorTypes;
            m_OnSelected = onSelected;
            minimumSize = new Vector2(MinimumWidth, MinimumHeight);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new AdvancedDropdownItem(m_EnumsOnly ? "Enum Types" : "Types");

            if (!m_EnumsOnly)
            {
                AdvancedDropdownItem common = new AdvancedDropdownItem(CommonFolderLabel);
                foreach (string typeName in s_CommonJsonTypes) common.AddChild(new TypeItem(typeName, typeName));
                root.AddChild(common);
            }

            IEnumerable<Type> types = m_EnumsOnly ? EnumTypes : m_IncludeEditorTypes ? EditorJsonTypes : PlayerJsonTypes;
            if (m_EnumsOnly && !m_IncludeEditorTypes) types = types.Where(type => !IsEditorNamespace(type.Namespace));

            foreach (IGrouping<string, Type> group in types.GroupBy(type => type.Namespace ?? string.Empty).OrderBy(group => group.Key))
            {
                AdvancedDropdownItem folder = new AdvancedDropdownItem(string.IsNullOrEmpty(group.Key) ? GlobalNamespaceLabel : group.Key);
                foreach (Type type in group.OrderBy(type => type.FullName, StringComparer.Ordinal))
                {
                    string readable = CSharpTypeName.GetReadable(type);
                    string shortName = string.IsNullOrEmpty(type.Namespace) ? readable : readable.Substring(type.Namespace.Length + 1);
                    folder.AddChild(new TypeItem(shortName, readable));
                }

                root.AddChild(folder);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeItem typeItem) m_OnSelected?.Invoke(typeItem.TypeName);
        }

        private static List<Type> EnumTypes => s_EnumTypes ??= TypeCache.GetTypesDerivedFrom<Enum>()
            .Where(type => type.IsEnum && type.IsVisible && !type.IsGenericType && !IsExcluded(type))
            .Distinct()
            .ToList();

        private static List<Type> PlayerJsonTypes => s_PlayerJsonTypes ??= CollectJsonTypes(AssembliesType.PlayerWithoutTestAssemblies);

        private static List<Type> EditorJsonTypes => s_EditorJsonTypes ??= CollectJsonTypes(AssembliesType.Editor);

        private static List<Type> CollectJsonTypes(AssembliesType assembliesType)
        {
            HashSet<string> projectAssemblies = new HashSet<string>(
                CompilationPipeline.GetAssemblies(assembliesType).Select(assembly => assembly.name));
            List<Type> result = new List<Type>();

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || !projectAssemblies.Contains(assembly.GetName().Name)) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                result.AddRange(types.Where(IsJsonCandidate));
            }

            return result;
        }

        private static bool IsJsonCandidate(Type type)
        {
            if (!type.IsVisible || type.IsGenericTypeDefinition || type.IsEnum || type.IsPrimitive || type.IsInterface) return false;
            if (type.IsAbstract) return false;
            if (!type.IsClass && !type.IsValueType) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (typeof(Delegate).IsAssignableFrom(type) || typeof(Attribute).IsAssignableFrom(type) ||
                typeof(Exception).IsAssignableFrom(type))
            {
                return false;
            }

            return !type.IsDefined(typeof(CompilerGeneratedAttribute), false) && !IsExcluded(type);
        }

        private static bool IsExcluded(Type type)
        {
            string ns = type.Namespace;
            if (string.IsNullOrEmpty(ns)) return false;

            foreach (string prefix in s_ExcludedNamespacePrefixes)
            {
                if (ns.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static bool IsEditorNamespace(string ns) =>
            ns != null && ns.StartsWith(UnityEditorNamespace, StringComparison.Ordinal);
    }
}
