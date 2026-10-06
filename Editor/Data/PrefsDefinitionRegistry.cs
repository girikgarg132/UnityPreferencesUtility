using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Finds and caches the definition assets in the project.</summary>
    public static class PrefsDefinitionRegistry
    {
        private const string TypeFilter = "t:" + nameof(PrefsDefinitionAsset);

        private static readonly Dictionary<string, CachedDefinition> s_Cache =
            new Dictionary<string, CachedDefinition>(StringComparer.OrdinalIgnoreCase);

        private static List<string> s_Paths;

        private readonly struct CachedDefinition
        {
            public readonly DateTime WriteTime;
            public readonly PrefsDefinition Definition;

            public CachedDefinition(DateTime writeTime, PrefsDefinition definition)
            {
                WriteTime = writeTime;
                Definition = definition;
            }
        }

        /// <summary>Raised after definitions are imported, moved or deleted.</summary>
        public static event Action Changed;

        /// <summary>Project-relative paths of every definition asset.</summary>
        public static IReadOnlyList<string> FindAllDefinitionPaths()
        {
            if (s_Paths != null) return s_Paths;

            s_Paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(TypeFilter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (PrefsDefinitionPaths.IsDefinitionPath(path) && !s_Paths.Contains(path)) s_Paths.Add(path);
            }

            s_Paths.Sort(StringComparer.OrdinalIgnoreCase);
            return s_Paths;
        }

        /// <summary>Loads (cached by file write time) a definition, or returns <c>null</c> if it cannot be read.</summary>
        public static PrefsDefinition TryLoad(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath)) return null;

                DateTime writeTime = File.GetLastWriteTimeUtc(fullPath);
                if (s_Cache.TryGetValue(assetPath, out CachedDefinition cached) && cached.WriteTime == writeTime)
                {
                    return cached.Definition;
                }

                PrefsDefinition definition = PrefsDefinitionSerializer.Load(fullPath);
                s_Cache[assetPath] = new CachedDefinition(writeTime, definition);
                return definition;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Clears cached paths and definitions and notifies listeners.</summary>
        internal static void Invalidate()
        {
            s_Paths = null;
            s_Cache.Clear();
            Changed?.Invoke();
        }

        /// <summary>Invalidates the registry when definitions change.</summary>
        private sealed class Postprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (!ContainsDefinition(importedAssets) && !ContainsDefinition(deletedAssets) &&
                    !ContainsDefinition(movedAssets) && !ContainsDefinition(movedFromAssetPaths))
                {
                    return;
                }

                Invalidate();
                PrefsDefinitionWindow.NotifyAssetsChanged();

                for (int i = 0; i < movedAssets.Length; i++)
                {
                    string newPath = movedAssets[i];
                    string oldPath = movedFromAssetPaths[i];
                    if (!PrefsDefinitionPaths.IsDefinitionPath(newPath)) continue;

                    EditorApplication.delayCall += () => PrefsDefinitionSaver.HandleMoved(oldPath, newPath);
                }
            }

            private static bool ContainsDefinition(string[] paths)
            {
                foreach (string path in paths)
                {
                    if (PrefsDefinitionPaths.IsDefinitionPath(path)) return true;
                }

                return false;
            }
        }
    }
}
