using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Create-menu entries, double-click handling and regeneration commands.</summary>
    internal static class PrefsDefinitionMenus
    {
        private const string CreateMenuRoot = "Assets/Create/Girik Garg/Preferences Utility/";
        private const string AssetMenuRoot = "Assets/Girik Garg/Preferences Utility/";
        private const string ToolsMenuRoot = "Tools/Preferences Utility/";
        private const string WindowMenuRoot = "Window/Preferences Utility/";
        private const int CreateMenuPriority = 0;
        private const string NewPlayerPrefsFileName = "NewPlayerPrefs" + PrefsDefinitionPaths.DottedExtension;
        private const string NewEditorPrefsFileName = "NewEditorPrefs" + PrefsDefinitionPaths.DottedExtension;
        private const string KeyPrefixSeparator = ".";

        /// <summary>Creates a PlayerPrefs definition in the selected Project folder (with in-place rename).</summary>
        [MenuItem(CreateMenuRoot + "Player Prefs Definition", false, CreateMenuPriority)]
        private static void CreatePlayerPrefsDefinition()
        {
            PrefsDefinition definition = new PrefsDefinition { storage = PrefsStorage.PlayerPrefs };
            CreateDefinitionAsset(NewPlayerPrefsFileName, definition);
        }

        /// <summary>Creates an EditorPrefs definition with a product-specific key prefix.</summary>
        [MenuItem(CreateMenuRoot + "Editor Prefs Definition", false, CreateMenuPriority + 1)]
        private static void CreateEditorPrefsDefinition()
        {
            PrefsDefinition definition = new PrefsDefinition
            {
                storage = PrefsStorage.EditorPrefs,
                keyPrefix = CSharpSyntax.ToIdentifier(PlayerSettings.productName) + KeyPrefixSeparator
            };
            CreateDefinitionAsset(NewEditorPrefsFileName, definition);
        }

        private static void CreateDefinitionAsset(string fileName, PrefsDefinition definition)
        {
            string content = PrefsDefinitionSerializer.ToJson(definition);
#if UNITY_6000_4_OR_NEWER
            ProjectWindowUtil.CreateAssetWithTextContent(fileName, content, PrefsDefinitionImporter.Icon);
#else
            ProjectWindowUtil.CreateAssetWithContent(fileName, content, PrefsDefinitionImporter.Icon);
#endif
        }

        /// <summary>Regenerates code for the selected definitions.</summary>
        [MenuItem(AssetMenuRoot + "Regenerate C# Code")]
        private static void RegenerateSelected()
        {
            foreach (string path in SelectedDefinitionPaths())
            {
                PrefsDefinitionSaver.Log(PrefsDefinitionSaver.Regenerate(path), path);
            }
        }

        [MenuItem(AssetMenuRoot + "Regenerate C# Code", true)]
        private static bool ValidateRegenerateSelected() => SelectedDefinitionPaths().Any();

        /// <summary>Regenerates code for every definition in the project.</summary>
        [MenuItem(ToolsMenuRoot + "Regenerate All Definitions")]
        private static void RegenerateAll()
        {
            IReadOnlyList<string> paths = PrefsDefinitionRegistry.FindAllDefinitionPaths().ToList();
            foreach (string path in paths)
            {
                PrefsDefinitionSaver.Log(PrefsDefinitionSaver.Regenerate(path), path);
            }

            if (paths.Count == 0) UnityEngine.Debug.Log("[Preferences Utility] No definitions found in the project.");
        }

        /// <summary>Opens the viewer for all PlayerPrefs and EditorPrefs.</summary>
        [MenuItem(WindowMenuRoot + "Prefs Viewer")]
        [MenuItem(ToolsMenuRoot + "Prefs Viewer")]
        private static void OpenViewer() => PrefsViewerWindow.Open();

        /// <summary>Opens definition assets in the editor window on double-click.</summary>
        [OnOpenAsset]
#if UNITY_6000_3_OR_NEWER
        private static bool OnOpenAsset(UnityEngine.EntityId entityId, int line) => TryOpen(AssetDatabase.GetAssetPath(entityId));
#else
        private static bool OnOpenAsset(int instanceId, int line) => TryOpen(AssetDatabase.GetAssetPath(instanceId));
#endif

        private static bool TryOpen(string assetPath)
        {
            if (!PrefsDefinitionPaths.IsDefinitionPath(assetPath)) return false;

            PrefsDefinitionWindow.Open(assetPath);
            return true;
        }

        private static IEnumerable<string> SelectedDefinitionPaths() =>
            Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(PrefsDefinitionPaths.IsDefinitionPath)
                .Distinct();
    }
}
