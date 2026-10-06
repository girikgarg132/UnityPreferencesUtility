using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Read-only Inspector for definition assets with shortcuts to the editor window and code generation.</summary>
    [CustomEditor(typeof(PrefsDefinitionImporter))]
    internal sealed class PrefsDefinitionImporterEditor : ScriptedImporterEditor
    {
        private const float ButtonHeight = 24f;
        private const string DefaultLabel = "(default)";

        private static readonly GUIContent s_EditContent = new GUIContent("Edit Preferences", "Open the Preferences Utility editor (or double-click the asset).");
        private static readonly GUIContent s_RegenerateContent = new GUIContent("Regenerate C#", "Generate the C# file from the saved definition.");
        private static readonly GUIContent s_PingContent = new GUIContent("Ping", "Highlight the generated file in the Project window.");

        protected override bool needsApplyRevert => false;

        public override bool showImportedObject => false;

        public override void OnInspectorGUI()
        {
            PrefsDefinitionImporter importer = (PrefsDefinitionImporter)target;
            string assetPath = importer.assetPath;
            PrefsDefinitionAsset asset = assetTarget as PrefsDefinitionAsset;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(s_EditContent, GUILayout.Height(ButtonHeight))) PrefsDefinitionWindow.Open(assetPath);
                if (GUILayout.Button(s_RegenerateContent, GUILayout.Height(ButtonHeight)))
                {
                    PrefsDefinitionSaver.Log(PrefsDefinitionSaver.Regenerate(assetPath), assetPath);
                }
            }

            EditorGUILayout.Space();

            if (asset == null) return;

            if (!string.IsNullOrEmpty(asset.ImportError))
            {
                EditorGUILayout.HelpBox(asset.ImportError, MessageType.Error);
                return;
            }

            PrefsDefinition definition = asset.Definition;
            string className = PrefsDefinitionPaths.GetEffectiveClassName(definition, assetPath);
            string fullName = string.IsNullOrEmpty(definition.namespaceName) ? className : definition.namespaceName + "." + className;
            string outputPath = PrefsDefinitionPaths.GetEffectiveOutputPath(definition, assetPath);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Storage", definition.storage);
                EditorGUILayout.TextField("Class", fullName);
                EditorGUILayout.TextField("Key Prefix", string.IsNullOrEmpty(definition.keyPrefix) ? DefaultLabel : definition.keyPrefix);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Output", outputPath);
                }

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(outputPath);
                using (new EditorGUI.DisabledScope(script == null))
                {
                    if (GUILayout.Button(s_PingContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        EditorGUIUtility.PingObject(script);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Entries ({definition.entries.Count})", EditorStyles.boldLabel);
            foreach (PrefsEntry entry in definition.entries)
            {
                PrefsValueHandler handler = PrefsValueHandlers.Get(entry.type);
                string typeLabel = handler == null ? entry.type : handler.UsesTypeName ? entry.typeName : handler.DisplayName;
                EditorGUILayout.LabelField(entry.name, typeLabel);
            }
        }
    }
}
