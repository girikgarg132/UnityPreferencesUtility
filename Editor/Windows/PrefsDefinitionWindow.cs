using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Editor for one <c>.prefsdef</c> asset. Edits happen on a hidden working copy (with Undo) and are only written
    /// when saved. Unity's unsaved-changes prompt protects edits when the window or the Editor is closed.
    /// </summary>
    internal sealed class PrefsDefinitionWindow : EditorWindow, IHasCustomMenu
    {
        private const float DefaultListWidth = 320f;
        private const float MinListWidth = 220f;
        private const float MinDetailWidth = 320f;
        private const float SplitterWidth = 5f;
        private const float RowPadding = 2f;
        private const float IconSize = 16f;
        private const float IssuesMaxHeight = 140f;
        private const float SearchFieldMaxWidth = 240f;
        private const float SmallButtonWidth = 60f;
        private const float SummaryMinHeight = 36f;
        private const float JsonAreaMinHeight = 80f;
        private const float SplitterLineAlpha = 0.25f;
        private const string UndoName = "Edit Preferences Definition";
        private const string NewEntryBaseName = "NewPreference";
        private const string DocumentationUrl = "https://github.com/girikgarg132/UnityPreferencesUtility#readme";
        private const string ErrorIconName = "console.erroricon.sml";
        private const string WarningIconName = "console.warnicon.sml";
        private const string InfoIconName = "console.infoicon.sml";
        private const string ShortcutSave = "Preferences Utility/Save Definition";
        private const string ShortcutDuplicate = "Preferences Utility/Duplicate Entry";
        private const string EntriesPropertyPath = "definition.entries";

        private static readonly List<PrefsDefinitionWindow> s_OpenWindows = new List<PrefsDefinitionWindow>();

        [SerializeField] private string m_AssetGuid;
        [SerializeField] private PrefsDefinitionWorkingCopy m_WorkingCopy;
        [SerializeField] private string m_SavedJson;
        [SerializeField] private string m_LastOutputPath;
        [SerializeField] private int m_SelectedIndex = -1;
        [SerializeField] private float m_ListWidth = DefaultListWidth;
        [SerializeField] private bool m_ShowSettings = true;
        [SerializeField] private bool m_ShowIssues = true;
        [SerializeField] private Vector2 m_ListScroll;
        [SerializeField] private Vector2 m_DetailScroll;
        [SerializeField] private Vector2 m_IssuesScroll;
        [SerializeField] private string m_SearchText = string.Empty;
        [SerializeField] private bool m_ExternalChangePending;

        [NonSerialized] private SerializedObject m_SerializedObject;
        [NonSerialized] private ReorderableList m_List;
        [NonSerialized] private PrefsDefinitionAnalysis m_Analysis;
        [NonSerialized] private string m_LoadError;
        [NonSerialized] private bool m_AssetMissing;
        [NonSerialized] private SearchField m_SearchField;
        [NonSerialized] private string m_LiveJsonKey;
        [NonSerialized] private string m_LiveJsonOriginal;
        [NonSerialized] private string m_LiveJsonBuffer;
        [NonSerialized] private string m_LiveJsonError;
        [NonSerialized] private string m_DefaultJsonMessage;
        [NonSerialized] private GUIStyle m_PlaceholderStyle;
        [NonSerialized] private GUIStyle m_RowTypeStyle;

        private PrefsDefinition Definition => m_WorkingCopy.definition;

        private string AssetPath => string.IsNullOrEmpty(m_AssetGuid) ? string.Empty : AssetDatabase.GUIDToAssetPath(m_AssetGuid);

        private string AssetName => Path.GetFileNameWithoutExtension(AssetPath);

        private IPrefsStore Store => PrefsStores.Get(Definition.storage);

        private PrefsDefinitionAnalysis Analysis =>
            m_Analysis ??= PrefsDefinitionAnalyzer.Analyze(Definition, AssetPath, true);

        private GUIStyle PlaceholderStyle => m_PlaceholderStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f) },
            padding = EditorStyles.textField.padding
        };

        private GUIStyle RowTypeStyle => m_RowTypeStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };

        #region Opening and lifetime

        /// <summary>Opens (or focuses) the editor for the definition at <paramref name="assetPath"/>.</summary>
        public static void Open(string assetPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;

            foreach (PrefsDefinitionWindow openWindow in s_OpenWindows)
            {
                if (openWindow.m_AssetGuid != guid) continue;

                openWindow.Show();
                openWindow.Focus();
                return;
            }

            PrefsDefinitionWindow window = CreateWindow<PrefsDefinitionWindow>(typeof(PrefsDefinitionWindow), typeof(SceneView));
            window.m_AssetGuid = guid;
            window.LoadFromDisk();
            window.Show();
            window.Focus();
        }

        /// <summary>Called when definition assets are imported, moved or deleted.</summary>
        internal static void NotifyAssetsChanged()
        {
            foreach (PrefsDefinitionWindow window in s_OpenWindows.ToArray())
            {
                window.OnAssetsChanged();
            }
        }

        private void OnEnable()
        {
            s_OpenWindows.Add(this);
            Undo.undoRedoPerformed += OnUndoRedo;
            m_SearchField = new SearchField();
            minSize = new Vector2(MinListWidth + MinDetailWidth, 300f);

            if (string.IsNullOrEmpty(m_AssetGuid)) return;

            if (m_WorkingCopy == null) LoadFromDisk();
            else RebuildEditingState();
        }

        private void OnDisable()
        {
            s_OpenWindows.Remove(this);
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnDestroy()
        {
            if (m_WorkingCopy == null) return;

            Undo.ClearUndo(m_WorkingCopy);
            DestroyImmediate(m_WorkingCopy);
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        /// <summary>Called by Unity when the user chooses Save in the unsaved-changes dialog.</summary>
        public override void SaveChanges()
        {
            if (Save()) base.SaveChanges();
        }

        /// <summary>Called by Unity when the user chooses Discard in the unsaved-changes dialog.</summary>
        public override void DiscardChanges()
        {
            base.DiscardChanges();
        }

        #endregion

        #region Loading, saving and dirty state

        private void LoadFromDisk()
        {
            string path = AssetPath;
            m_AssetMissing = string.IsNullOrEmpty(path) || !File.Exists(Path.GetFullPath(path));
            if (m_AssetMissing)
            {
                m_LoadError = "The definition asset no longer exists.";
                hasUnsavedChanges = false;
                return;
            }

            PrefsDefinition definition;
            try
            {
                definition = PrefsDefinitionSerializer.Load(Path.GetFullPath(path));
            }
            catch (Exception exception)
            {
                m_LoadError = exception.Message;
                hasUnsavedChanges = false;
                UpdateTitle();
                return;
            }

            m_LoadError = null;
            if (m_WorkingCopy == null)
            {
                m_WorkingCopy = CreateInstance<PrefsDefinitionWorkingCopy>();
                m_WorkingCopy.hideFlags = HideFlags.HideAndDontSave;
            }

            Undo.ClearUndo(m_WorkingCopy);
            m_WorkingCopy.definition = definition;
            m_SavedJson = PrefsDefinitionSerializer.ToJson(definition);
            m_LastOutputPath = PrefsDefinitionPaths.GetEffectiveOutputPath(definition, path);
            m_ExternalChangePending = false;
            m_SelectedIndex = Mathf.Clamp(m_SelectedIndex, -1, definition.entries.Count - 1);
            ResetLiveJson();
            RebuildEditingState();
        }

        private void RebuildEditingState()
        {
            m_SerializedObject = new SerializedObject(m_WorkingCopy);
            SerializedProperty entries = m_SerializedObject.FindProperty(EntriesPropertyPath);
            m_List = new ReorderableList(m_SerializedObject, entries, true, false, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + RowPadding * 2f,
                drawElementCallback = DrawListElement,
                onSelectCallback = list => Select(list.index),
                onAddDropdownCallback = (rect, list) => ShowAddMenu(rect),
                onRemoveCallback = list => RemoveEntry(list.index),
                onReorderCallbackWithDetails = (list, oldIndex, newIndex) => m_SelectedIndex = newIndex,
                index = m_SelectedIndex
            };
            m_Analysis = null;
            UpdateDirtyState();
        }

        private bool Save()
        {
            if (m_WorkingCopy == null || m_AssetMissing) return false;

            GUI.FocusControl(null);
            string path = AssetPath;
            PrefsSaveResult result = PrefsDefinitionSaver.Save(path, Definition, m_LastOutputPath);
            if (result.IoError != null)
            {
                EditorUtility.DisplayDialog("Preferences Utility", result.IoError, "OK");
                return false;
            }

            m_SavedJson = PrefsDefinitionSerializer.ToJson(Definition);
            m_ExternalChangePending = false;
            m_Analysis = result.Analysis;
            if (result.CodeGenerated)
            {
                m_LastOutputPath = result.Analysis.OutputPath;
                ShowNotification(new GUIContent(result.CodeChanged ? "Saved and generated C#" : "Saved (C# up to date)"));
            }
            else
            {
                ShowNotification(new GUIContent("Saved. Fix the errors to generate C#."));
            }

            UpdateDirtyState();
            return true;
        }

        private void Revert()
        {
            if (hasUnsavedChanges &&
                !EditorUtility.DisplayDialog("Revert Changes", $"Discard all unsaved changes to '{AssetName}'?", "Revert", "Cancel"))
            {
                return;
            }

            LoadFromDisk();
        }

        private void Regenerate()
        {
            string path = AssetPath;
            PrefsSaveResult result = PrefsDefinitionSaver.Regenerate(path);
            PrefsDefinitionSaver.Log(result, path);
            if (result.CodeGenerated) m_LastOutputPath = result.Analysis.OutputPath;
            ShowNotification(new GUIContent(result.Success ? "C# regenerated" : "Regeneration failed (see Console)"));
        }

        private void OnAssetsChanged()
        {
            string path = AssetPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(Path.GetFullPath(path)))
            {
                m_AssetMissing = true;
                hasUnsavedChanges = false;
                Repaint();
                return;
            }

            if (m_AssetMissing || m_WorkingCopy == null)
            {
                LoadFromDisk();
                Repaint();
                return;
            }

            string diskJson;
            try
            {
                diskJson = PrefsDefinitionSerializer.ToJson(PrefsDefinitionSerializer.Load(Path.GetFullPath(path)));
            }
            catch (Exception)
            {
                diskJson = null;
            }

            if (diskJson != m_SavedJson)
            {
                if (hasUnsavedChanges) m_ExternalChangePending = true;
                else LoadFromDisk();
            }

            m_Analysis = null;
            UpdateTitle();
            Repaint();
        }

        private void RecordUndo() => Undo.RecordObject(m_WorkingCopy, UndoName);

        private void MarkChanged()
        {
            m_Analysis = null;
            m_SerializedObject?.Update();
            UpdateDirtyState();
            Repaint();
        }

        private void UpdateDirtyState()
        {
            if (m_WorkingCopy != null && !m_AssetMissing)
            {
                hasUnsavedChanges = PrefsDefinitionSerializer.ToJson(Definition) != m_SavedJson;
            }

            saveChangesMessage = $"'{AssetName}' has unsaved changes. Do you want to save them?";
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string name = string.IsNullOrEmpty(AssetName) ? "Preferences" : AssetName;
            titleContent = new GUIContent(name, PrefsDefinitionImporter.Icon, AssetPath);
        }

        private void OnUndoRedo()
        {
            if (m_WorkingCopy == null) return;

            m_SelectedIndex = Mathf.Clamp(m_SelectedIndex, -1, Definition.entries.Count - 1);
            if (m_List != null) m_List.index = m_SelectedIndex;
            MarkChanged();
        }

        #endregion

        #region Entry operations

        private void Select(int index)
        {
            m_SelectedIndex = index;
            if (m_List != null) m_List.index = index;
            ResetLiveJson();
            m_DefaultJsonMessage = null;
            GUI.FocusControl(null);
            Repaint();
        }

        private void ShowAddMenu(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();
            foreach (PrefsValueHandler handler in PrefsValueHandlers.All)
            {
                string id = handler.Id;
                menu.AddItem(new GUIContent(handler.MenuPath), false, () => AddEntry(id));
            }

            menu.DropDown(buttonRect);
        }

        private void AddEntry(string handlerId)
        {
            PrefsValueHandler handler = PrefsValueHandlers.Get(handlerId);
            RecordUndo();
            PrefsEntry entry = new PrefsEntry
            {
                name = MakeUniqueName(NewEntryBaseName),
                type = handlerId,
                defaultValue = handler.UsesTypeName ? string.Empty : handler.FormatText(handler.GetZeroValue(null), null)
            };
            Definition.entries.Add(entry);
            MarkChanged();
            Select(Definition.entries.Count - 1);
        }

        private void DuplicateEntry(int index)
        {
            if (index < 0 || index >= Definition.entries.Count) return;

            RecordUndo();
            PrefsEntry copy = Definition.entries[index].Clone();
            copy.name = MakeUniqueName(copy.name);
            if (!string.IsNullOrEmpty(copy.key)) copy.key = MakeUniqueKey(copy.key);
            Definition.entries.Insert(index + 1, copy);
            MarkChanged();
            Select(index + 1);
        }

        private void RemoveEntry(int index)
        {
            if (index < 0 || index >= Definition.entries.Count) return;

            RecordUndo();
            Definition.entries.RemoveAt(index);
            MarkChanged();
            Select(Mathf.Min(index, Definition.entries.Count - 1));
        }

        private string MakeUniqueName(string baseName)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (PrefsEntry entry in Definition.entries) names.Add(entry.name);
            return MakeUnique(baseName, names);
        }

        private string MakeUniqueKey(string baseKey)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (PrefsEntry entry in Definition.entries) keys.Add(entry.key);
            return MakeUnique(baseKey, keys);
        }

        private static string MakeUnique(string baseName, HashSet<string> existing)
        {
            if (!existing.Contains(baseName)) return baseName;

            string stem = baseName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            for (int suffix = 1; ; suffix++)
            {
                string candidate = stem + suffix;
                if (!existing.Contains(candidate)) return candidate;
            }
        }

        [Shortcut(ShortcutSave, typeof(PrefsDefinitionWindow), KeyCode.S, ShortcutModifiers.Action)]
        private static void SaveShortcut(ShortcutArguments arguments)
        {
            if (arguments.context is PrefsDefinitionWindow window) window.Save();
        }

        [Shortcut(ShortcutDuplicate, typeof(PrefsDefinitionWindow), KeyCode.D, ShortcutModifiers.Action)]
        private static void DuplicateShortcut(ShortcutArguments arguments)
        {
            if (arguments.context is PrefsDefinitionWindow window) window.DuplicateEntry(window.m_SelectedIndex);
        }

        /// <inheritdoc />
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Regenerate C#"), false, Regenerate);
            menu.AddItem(new GUIContent("Open Generated File"), false, () =>
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(Analysis.OutputPath);
                if (script != null) AssetDatabase.OpenAsset(script);
            });
            menu.AddItem(new GUIContent("Open Definition in Text Editor"), false,
                () => EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetPath)));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Prefs Viewer"), false, PrefsViewerWindow.Open);
            menu.AddItem(new GUIContent("Documentation"), false, () => Application.OpenURL(DocumentationUrl));
        }

        #endregion

        #region GUI: layout

        private void OnGUI()
        {
            if (m_WorkingCopy == null || m_LoadError != null || m_AssetMissing)
            {
                DrawUnavailable();
                return;
            }

            DrawToolbar();
            if (m_ExternalChangePending) DrawExternalChangeBar();
            DrawSettings();

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(m_ListWidth)))
                {
                    DrawEntryList();
                }

                DrawSplitter();

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawDetails();
                }
            }

            DrawIssues();
        }

        private void DrawUnavailable()
        {
            EditorGUILayout.Space();
            string message = m_AssetMissing
                ? "The definition asset was deleted or moved outside the project."
                : $"The definition could not be loaded:\n{m_LoadError}";
            EditorGUILayout.HelpBox(message, MessageType.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!m_AssetMissing && GUILayout.Button("Retry")) LoadFromDisk();
                if (!m_AssetMissing && GUILayout.Button("Open in Text Editor")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetPath));
                if (GUILayout.Button("Close")) Close();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                {
                    if (GUILayout.Button(new GUIContent("Save", "Save the definition and generate C# (Ctrl/Cmd+S)."), EditorStyles.toolbarButton)) Save();
                    if (GUILayout.Button(new GUIContent("Revert", "Discard unsaved changes."), EditorStyles.toolbarButton)) Revert();
                }

                using (new EditorGUI.DisabledScope(hasUnsavedChanges))
                {
                    if (GUILayout.Button(new GUIContent("Regenerate C#", "Generate C# from the saved definition."), EditorStyles.toolbarButton)) Regenerate();
                }

                GUILayout.FlexibleSpace();
                m_SearchText = m_SearchField.OnToolbarGUI(m_SearchText, GUILayout.MaxWidth(SearchFieldMaxWidth));

                if (GUILayout.Button(new GUIContent("Select Asset", "Select the definition in the Project window."), EditorStyles.toolbarButton))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                if (GUILayout.Button(new GUIContent("Prefs Viewer", "Show all PlayerPrefs and EditorPrefs."), EditorStyles.toolbarButton))
                {
                    PrefsViewerWindow.Open();
                }
            }
        }

        private void DrawExternalChangeBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon(
                    "The definition file changed on disk (for example after a VCS update) while you have unsaved changes.",
                    MessageType.Warning), EditorStyles.wordWrappedLabel);

                if (GUILayout.Button("Reload", GUILayout.Width(SmallButtonWidth)))
                {
                    LoadFromDisk();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Keep Mine", GUILayout.Width(SmallButtonWidth + 15f))) m_ExternalChangePending = false;
            }
        }

        private void DrawSplitter()
        {
            Rect splitterRect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, GUILayout.ExpandHeight(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when splitterRect.Contains(current.mousePosition) && current.button == 0:
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    m_ListWidth = Mathf.Clamp(current.mousePosition.x, MinListWidth, Mathf.Max(MinListWidth, position.width - MinDetailWidth));
                    current.Use();
                    Repaint();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
                case EventType.Repaint:
                    Rect line = new Rect(splitterRect.center.x, splitterRect.y, 1f, splitterRect.height);
                    EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, SplitterLineAlpha));
                    break;
            }
        }

        #endregion

        #region GUI: settings

        private void DrawSettings()
        {
            m_ShowSettings = EditorGUILayout.BeginFoldoutHeaderGroup(m_ShowSettings, "Code Generation");
            if (m_ShowSettings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PrefsDefinition definition = Definition;
                    string assetPath = AssetPath;

                    EditorGUI.BeginChangeCheck();
                    PrefsStorage storage = (PrefsStorage)EditorGUILayout.EnumPopup(
                        new GUIContent("Storage", "PlayerPrefs work in builds. EditorPrefs are Editor-only and shared by all projects."),
                        definition.storage);
                    if (EditorGUI.EndChangeCheck()) ApplySetting(() => definition.storage = storage);

                    string className = PlaceholderTextField(new GUIContent("Class Name", "Empty uses the asset name."),
                        definition.className, PrefsDefinitionPaths.GetDefaultClassName(assetPath));
                    if (className != definition.className) ApplySetting(() => definition.className = className);

                    string namespaceName = PlaceholderTextField(new GUIContent("Namespace", "Empty uses the global namespace."),
                        definition.namespaceName, "(global namespace)");
                    if (namespaceName != definition.namespaceName) ApplySetting(() => definition.namespaceName = namespaceName);

                    DrawOutputPathField(definition, assetPath);

                    string keyPrefix = PlaceholderTextField(new GUIContent("Key Prefix", "Prepended to every key, for example 'MyGame.'."),
                        definition.keyPrefix, "(none)");
                    if (keyPrefix != definition.keyPrefix) ApplySetting(() => definition.keyPrefix = keyPrefix);

                    EditorGUI.BeginChangeCheck();
                    PrefsAccessModifier access = (PrefsAccessModifier)EditorGUILayout.EnumPopup("Accessibility", definition.accessModifier);
                    if (EditorGUI.EndChangeCheck()) ApplySetting(() => definition.accessModifier = access);
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOutputPathField(PrefsDefinition definition, string assetPath)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string defaultPath = PrefsDefinitionPaths.GetDefaultOutputPath(definition, assetPath);
                string outputPath = PlaceholderTextField(
                    new GUIContent("Output Path", "Project-relative .cs path. Empty generates next to the asset."),
                    definition.outputPath, defaultPath);
                if (outputPath != definition.outputPath) ApplySetting(() => definition.outputPath = outputPath);

                if (GUILayout.Button(new GUIContent("…", "Choose the generated file location."), EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    string current = PrefsDefinitionPaths.GetEffectiveOutputPath(definition, assetPath);
                    string chosen = EditorUtility.SaveFilePanelInProject("Generated C# File", Path.GetFileName(current), "cs",
                        "Choose where the generated C# file is saved.", Path.GetDirectoryName(current));
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        string normalized = PrefsDefinitionPaths.NormalizeSeparators(chosen);
                        ApplySetting(() => definition.outputPath = normalized == defaultPath ? string.Empty : normalized);
                    }

                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(definition.outputPath)))
                {
                    if (GUILayout.Button(new GUIContent("Reset", "Use the default path."), EditorStyles.miniButton, GUILayout.Width(44f)))
                    {
                        ApplySetting(() => definition.outputPath = string.Empty);
                    }
                }
            }
        }

        private void ApplySetting(Action apply)
        {
            RecordUndo();
            apply();
            MarkChanged();
        }

        private string PlaceholderTextField(GUIContent label, string value, string placeholder)
        {
            string result = EditorGUILayout.TextField(label, value ?? string.Empty);
            if (string.IsNullOrEmpty(value) && Event.current.type == EventType.Repaint)
            {
                Rect fieldRect = GUILayoutUtility.GetLastRect();
                fieldRect.xMin += EditorGUIUtility.labelWidth + 2f;
                PlaceholderStyle.Draw(fieldRect, new GUIContent(placeholder), false, false, false, false);
            }

            return result;
        }

        #endregion

        #region GUI: entry list

        private void DrawEntryList()
        {
            m_ListScroll = EditorGUILayout.BeginScrollView(m_ListScroll);
            if (string.IsNullOrEmpty(m_SearchText))
            {
                m_SerializedObject.Update();
                m_List.index = m_SelectedIndex;
                m_List.DoLayoutList();
                if (m_SerializedObject.ApplyModifiedProperties())
                {
                    m_SelectedIndex = m_List.index;
                    MarkChanged();
                }
            }
            else
            {
                DrawFilteredList();
            }

            if (Definition.entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No preferences yet. Press + to add one.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFilteredList()
        {
            for (int index = 0; index < Definition.entries.Count; index++)
            {
                PrefsEntry entry = Definition.entries[index];
                bool matches = entry.name.IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Definition.GetEffectiveKey(entry).IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches) continue;

                Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + RowPadding * 2f);
                if (index == m_SelectedIndex && Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                }

                DrawListElement(rowRect, index, index == m_SelectedIndex, false);
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    Select(index);
                    Event.current.Use();
                }
            }
        }

        private void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= Definition.entries.Count) return;

            PrefsEntry entry = Definition.entries[index];
            PrefsEntryInfo info = index < Analysis.Entries.Count ? Analysis.Entries[index] : null;
            rect.y += RowPadding;
            rect.height = EditorGUIUtility.singleLineHeight;

            Rect iconRect = new Rect(rect.x, rect.y, IconSize, rect.height);
            float remaining = rect.width - IconSize - 4f;
            Rect nameRect = new Rect(iconRect.xMax + 2f, rect.y, remaining * 0.42f, rect.height);
            Rect typeRect = new Rect(nameRect.xMax + 4f, rect.y, remaining * 0.24f, rect.height);
            Rect valueRect = new Rect(typeRect.xMax + 4f, rect.y, rect.xMax - typeRect.xMax - 4f, rect.height);

            Texture issueIcon = GetIssueIcon(index);
            if (issueIcon != null) GUI.Label(iconRect, issueIcon);

            EditorGUI.LabelField(nameRect, string.IsNullOrEmpty(entry.name) ? "(unnamed)" : entry.name,
                isActive ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUI.LabelField(typeRect, GetTypeLabel(entry), RowTypeStyle);
            EditorGUI.LabelField(valueRect, GetLivePreview(info), RowTypeStyle);

            Event current = Event.current;
            if (current.type == EventType.ContextClick && rect.Contains(current.mousePosition))
            {
                ShowEntryContextMenu(index);
                current.Use();
            }
        }

        private void ShowEntryContextMenu(int index)
        {
            Select(index);
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateEntry(index));
            menu.AddItem(new GUIContent("Delete"), false, () => RemoveEntry(index));
            menu.AddSeparator(string.Empty);
            string key = Definition.GetEffectiveKey(Definition.entries[index]);
            menu.AddItem(new GUIContent("Copy Key"), false, () => EditorGUIUtility.systemCopyBuffer = key);
            menu.AddItem(new GUIContent("Delete Stored Value"), false, () =>
            {
                Store.DeleteKey(key);
                Store.Save();
                ResetLiveJson();
            });
            menu.ShowAsContext();
        }

        private static string GetTypeLabel(PrefsEntry entry)
        {
            PrefsValueHandler handler = PrefsValueHandlers.Get(entry.type);
            if (handler == null) return entry.type;
            if (!handler.UsesTypeName) return handler.DisplayName;
            return string.IsNullOrEmpty(entry.typeName) ? handler.DisplayName : entry.typeName;
        }

        private string GetLivePreview(PrefsEntryInfo info)
        {
            if (info?.Handler == null || string.IsNullOrEmpty(info.Key)) return string.Empty;

            PrefsLiveValue live = info.Handler.Read(Store, info.Key, info.ValueType);
            switch (live.State)
            {
                case PrefsLiveState.Missing: return "(not set)";
                case PrefsLiveState.Valid: return "= " + info.Handler.Preview(live.Value, info.ValueType);
                case PrefsLiveState.KindMismatch: return $"(stored as {live.StoredKind})";
                default: return "(unreadable)";
            }
        }

        private Texture GetIssueIcon(int index)
        {
            PrefsIssueSeverity? worst = null;
            foreach (PrefsIssue issue in Analysis.Issues)
            {
                if (issue.EntryIndex != index) continue;
                if (worst == null || issue.Severity > worst) worst = issue.Severity;
            }

            switch (worst)
            {
                case PrefsIssueSeverity.Error: return EditorGUIUtility.IconContent(ErrorIconName).image;
                case PrefsIssueSeverity.Warning: return EditorGUIUtility.IconContent(WarningIconName).image;
                default: return null;
            }
        }

        #endregion

        #region GUI: details

        private void DrawDetails()
        {
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
            if (m_SelectedIndex < 0 || m_SelectedIndex >= Definition.entries.Count)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Select a preference, or press + to add one.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                return;
            }

            PrefsEntry entry = Definition.entries[m_SelectedIndex];
            PrefsEntryInfo info = Analysis.Entries[m_SelectedIndex];

            EditorGUILayout.LabelField("Preference", EditorStyles.boldLabel);
            DrawEntryProperties(entry, info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Value", EditorStyles.boldLabel);
            DrawDefaultValue(entry, info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Current Value ({Definition.storage})", EditorStyles.boldLabel);
            DrawLiveValue(info);

            EditorGUILayout.Space();
            foreach (PrefsIssue issue in Analysis.Issues)
            {
                if (issue.EntryIndex == m_SelectedIndex) EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryProperties(PrefsEntry entry, PrefsEntryInfo info)
        {
            string name = EditorGUILayout.TextField(new GUIContent("Name", "Generated C# property name."), entry.name);
            if (name != entry.name) ApplySetting(() => entry.name = name);

            string key = PlaceholderTextField(new GUIContent("Key", "Storage key without the prefix. Empty uses the name."),
                entry.key, entry.name);
            if (key != entry.key) ApplySetting(() => entry.key = key);

            if (!string.IsNullOrEmpty(Definition.keyPrefix))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Stored Key", info.Key);
                }
            }

            int currentTypeIndex = PrefsValueHandlers.IndexOf(entry.type);
            EditorGUI.BeginChangeCheck();
            int newTypeIndex = EditorGUILayout.Popup(new GUIContent("Type"), Mathf.Max(0, currentTypeIndex),
                Array.ConvertAll(PrefsValueHandlers.MenuPaths, path => new GUIContent(path)));
            if (EditorGUI.EndChangeCheck() && newTypeIndex != currentTypeIndex) ChangeType(entry, PrefsValueHandlers.All[newTypeIndex]);

            if (info.Handler != null && info.Handler.UsesTypeName) DrawTypeNameField(entry, info);

            EditorGUILayout.LabelField("Summary");
            string summary = EditorGUILayout.TextArea(entry.summary, EditorStyles.textArea, GUILayout.MinHeight(SummaryMinHeight));
            if (summary != entry.summary) ApplySetting(() => entry.summary = summary);
        }

        private void DrawTypeNameField(PrefsEntry entry, PrefsEntryInfo info)
        {
            bool isEnum = info.Handler.Id == PrefsValueHandlers.EnumId;
            using (new EditorGUILayout.HorizontalScope())
            {
                string typeName = EditorGUILayout.TextField(
                    new GUIContent("Type Name", "C# type, for example MyGame.Difficulty, List<int> or Dictionary<string, Vector3>."),
                    entry.typeName);
                if (typeName != entry.typeName) ApplySetting(() => entry.typeName = typeName);

                Rect pickRect = GUILayoutUtility.GetRect(new GUIContent("Pick"), EditorStyles.miniPullDown, GUILayout.Width(SmallButtonWidth));
                if (EditorGUI.DropdownButton(pickRect, new GUIContent("Pick"), FocusType.Passive, EditorStyles.miniPullDown))
                {
                    bool includeEditorTypes = Definition.storage == PrefsStorage.EditorPrefs;
                    new TypePickerDropdown(isEnum, includeEditorTypes, picked => OnTypePicked(entry, picked, isEnum)).Show(pickRect);
                }
            }

            string status = info.ValueType != null
                ? "Resolved: " + CSharpTypeName.GetReadable(info.ValueType)
                : string.IsNullOrWhiteSpace(entry.typeName) ? "Enter or pick a type." : "Unresolved: " + info.TypeError;
            EditorGUILayout.LabelField(" ", status, EditorStyles.miniLabel);
        }

        private void OnTypePicked(PrefsEntry entry, string typeName, bool isEnum)
        {
            RecordUndo();
            entry.typeName = typeName;
            if (isEnum)
            {
                TypeNameResolver.ResolveResult result = TypeNameResolver.Resolve(typeName, Definition.namespaceName);
                string[] names = result.Type != null && result.Type.IsEnum ? Enum.GetNames(result.Type) : Array.Empty<string>();
                entry.defaultValue = names.Length > 0 ? names[0] : string.Empty;
            }
            else
            {
                entry.defaultValue = string.Empty;
            }

            MarkChanged();
        }

        private void ChangeType(PrefsEntry entry, PrefsValueHandler newHandler)
        {
            RecordUndo();
            PrefsValueHandler oldHandler = PrefsValueHandlers.Get(entry.type);
            entry.type = newHandler.Id;

            if (!newHandler.UsesTypeName) entry.typeName = string.Empty;
            else if (oldHandler != null && oldHandler.UsesTypeName != newHandler.UsesTypeName) entry.typeName = string.Empty;

            if (newHandler.UsesTypeName)
            {
                entry.defaultValue = string.Empty;
            }
            else if (!newHandler.TryParseText(entry.defaultValue, null, out object converted, out _))
            {
                entry.defaultValue = newHandler.FormatText(newHandler.GetZeroValue(null), null);
            }
            else
            {
                entry.defaultValue = newHandler.FormatText(converted, null);
            }

            ResetLiveJson();
            MarkChanged();
        }

        private void DrawDefaultValue(PrefsEntry entry, PrefsEntryInfo info)
        {
            if (info.Handler == null) return;

            if (info.Handler.IsJson)
            {
                DrawDefaultJson(entry, info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            object edited = info.Handler.DrawField(new GUIContent("Value"), info.DefaultValue, info.ValueType);
            if (EditorGUI.EndChangeCheck())
            {
                string text = info.Handler.FormatText(edited, info.ValueType);
                if (text != entry.defaultValue) ApplySetting(() => entry.defaultValue = text);
            }
        }

        private void DrawDefaultJson(PrefsEntry entry, PrefsEntryInfo info)
        {
            EditorGUILayout.LabelField("Json (empty means default/null)", EditorStyles.miniLabel);
            string json = EditorGUILayout.TextArea(entry.defaultValue, EditorStyles.textArea,
                GUILayout.MinHeight(JsonAreaMinHeight), GUILayout.ExpandWidth(true));
            if (json != entry.defaultValue) ApplySetting(() => entry.defaultValue = json);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(info.ValueType == null))
                {
                    if (GUILayout.Button(new GUIContent("From Type", "Fill with a new instance of the type."), EditorStyles.miniButtonLeft))
                    {
                        if (JsonValueHandler.TryCreateTemplate(info.ValueType, out string template, out string error))
                        {
                            ApplySetting(() => entry.defaultValue = template);
                            m_DefaultJsonMessage = null;
                        }
                        else
                        {
                            m_DefaultJsonMessage = error;
                        }

                        GUI.FocusControl(null);
                    }
                }

                if (GUILayout.Button("Format", EditorStyles.miniButtonMid))
                {
                    string formatted = JsonValueHandler.Indent(entry.defaultValue);
                    if (formatted != entry.defaultValue) ApplySetting(() => entry.defaultValue = formatted);
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Minify", EditorStyles.miniButtonMid))
                {
                    string minified = Minify(entry.defaultValue);
                    if (minified != entry.defaultValue) ApplySetting(() => entry.defaultValue = minified);
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Clear", EditorStyles.miniButtonRight))
                {
                    ApplySetting(() => entry.defaultValue = string.Empty);
                    GUI.FocusControl(null);
                }
            }

            if (!string.IsNullOrEmpty(m_DefaultJsonMessage)) EditorGUILayout.HelpBox(m_DefaultJsonMessage, MessageType.Warning);
        }

        #endregion

        #region GUI: live values

        private void DrawLiveValue(PrefsEntryInfo info)
        {
            if (info.Handler == null || string.IsNullOrEmpty(info.Key)) return;

            IPrefsStore store = Store;
            bool canEdit = !info.Handler.UsesTypeName || info.ValueType != null;
            PrefsLiveValue live = info.Handler.Read(store, info.Key, info.ValueType);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (live.State)
                {
                    case PrefsLiveState.Missing:
                        EditorGUILayout.LabelField("Not set. The property returns the default value.", EditorStyles.wordWrappedMiniLabel);
                        break;
                    case PrefsLiveState.KindMismatch:
                        EditorGUILayout.HelpBox($"The key is stored as {live.StoredKind}, but this type uses {info.Handler.GetSlot(info.ValueType)}. " +
                                                "Reading returns the default value.", MessageType.Warning);
                        break;
                    case PrefsLiveState.Malformed:
                        EditorGUILayout.HelpBox("The stored text cannot be read as this type. Reading returns the default value.", MessageType.Warning);
                        EditorGUILayout.SelectableLabel(live.Raw ?? string.Empty, EditorStyles.textField,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        break;
                    default:
                        if (!canEdit)
                        {
                            EditorGUILayout.HelpBox("Resolve the type to edit the stored value.", MessageType.Info);
                        }
                        else if (info.Handler.IsJson)
                        {
                            DrawLiveJson(info, store, live.Raw ?? string.Empty);
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            object edited = info.Handler.DrawField(new GUIContent("Value"), live.Value, info.ValueType);
                            if (EditorGUI.EndChangeCheck()) info.Handler.Write(store, info.Key, edited, info.ValueType);
                        }

                        break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!canEdit))
                    {
                        if (GUILayout.Button(new GUIContent("Set to Default", "Store the default value."), EditorStyles.miniButtonLeft))
                        {
                            WriteDefault(info, store);
                        }
                    }

                    using (new EditorGUI.DisabledScope(live.State == PrefsLiveState.Missing))
                    {
                        if (GUILayout.Button(new GUIContent("Delete Key", "Remove the stored value."), EditorStyles.miniButtonRight))
                        {
                            store.DeleteKey(info.Key);
                            store.Save();
                            ResetLiveJson();
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private void DrawLiveJson(PrefsEntryInfo info, IPrefsStore store, string raw)
        {
            string indentedOriginal = JsonValueHandler.Indent(raw);
            if (m_LiveJsonKey != info.Key)
            {
                m_LiveJsonKey = info.Key;
                m_LiveJsonOriginal = raw;
                m_LiveJsonBuffer = indentedOriginal;
                m_LiveJsonError = null;
            }
            else if (raw != m_LiveJsonOriginal)
            {
                bool hasPendingEdits = m_LiveJsonBuffer != JsonValueHandler.Indent(m_LiveJsonOriginal);
                m_LiveJsonOriginal = raw;
                if (!hasPendingEdits) m_LiveJsonBuffer = indentedOriginal;
            }

            m_LiveJsonBuffer = EditorGUILayout.TextArea(m_LiveJsonBuffer, EditorStyles.textArea,
                GUILayout.MinHeight(JsonAreaMinHeight), GUILayout.ExpandWidth(true));
            bool isDirty = m_LiveJsonBuffer != indentedOriginal;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!isDirty))
                {
                    if (GUILayout.Button("Apply", EditorStyles.miniButtonLeft, GUILayout.Width(SmallButtonWidth)))
                    {
                        ApplyLiveJson(info, store);
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("Revert", EditorStyles.miniButtonRight, GUILayout.Width(SmallButtonWidth)))
                    {
                        m_LiveJsonBuffer = indentedOriginal;
                        m_LiveJsonError = null;
                        GUI.FocusControl(null);
                    }
                }
            }

            if (!string.IsNullOrEmpty(m_LiveJsonError)) EditorGUILayout.HelpBox(m_LiveJsonError, MessageType.Error);
        }

        private void ApplyLiveJson(PrefsEntryInfo info, IPrefsStore store)
        {
            if (!info.Handler.TryParseText(m_LiveJsonBuffer, info.ValueType, out _, out string error))
            {
                m_LiveJsonError = error;
                return;
            }

            string compact;
            try
            {
                compact = string.IsNullOrWhiteSpace(m_LiveJsonBuffer)
                    ? string.Empty
                    : PrefsJson.Serialize(PrefsJson.DeserializeStrict(m_LiveJsonBuffer, info.ValueType), info.ValueType, false);
            }
            catch (Exception exception)
            {
                m_LiveJsonError = exception.Message;
                return;
            }

            if (string.IsNullOrEmpty(compact)) store.DeleteKey(info.Key);
            else store.SetString(info.Key, compact);

            store.Save();
            m_LiveJsonError = null;
            ResetLiveJson();
        }

        private void WriteDefault(PrefsEntryInfo info, IPrefsStore store)
        {
            if (info.Handler.IsJson)
            {
                string defaultJson = Minify(info.Entry.defaultValue);
                if (string.IsNullOrEmpty(defaultJson)) store.DeleteKey(info.Key);
                else store.SetString(info.Key, defaultJson);
                store.Save();
            }
            else
            {
                info.Handler.Write(store, info.Key, info.DefaultValue, info.ValueType);
            }

            ResetLiveJson();
            GUI.FocusControl(null);
        }

        private void ResetLiveJson()
        {
            m_LiveJsonKey = null;
            m_LiveJsonOriginal = null;
            m_LiveJsonBuffer = null;
            m_LiveJsonError = null;
        }

        private static string Minify(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                return JToken.Parse(json).ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return json.Trim();
            }
        }

        #endregion

        #region GUI: issues

        private void DrawIssues()
        {
            PrefsDefinitionAnalysis analysis = Analysis;
            int errors = analysis.Count(PrefsIssueSeverity.Error);
            int warnings = analysis.Count(PrefsIssueSeverity.Warning);
            int infos = analysis.Count(PrefsIssueSeverity.Info);

            string header = errors + warnings + infos == 0
                ? $"No issues. Saving generates {analysis.OutputPath}"
                : $"Issues: {errors} error(s), {warnings} warning(s), {infos} info";
            m_ShowIssues = EditorGUILayout.BeginFoldoutHeaderGroup(m_ShowIssues, header);
            if (m_ShowIssues && analysis.Issues.Count > 0)
            {
                float height = Mathf.Min(IssuesMaxHeight, analysis.Issues.Count * (EditorGUIUtility.singleLineHeight + 4f) + 6f);
                m_IssuesScroll = EditorGUILayout.BeginScrollView(m_IssuesScroll, GUILayout.Height(height));
                foreach (PrefsIssue issue in analysis.Issues)
                {
                    DrawIssueRow(issue);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawIssueRow(PrefsIssue issue)
        {
            string prefix = issue.EntryIndex >= 0 && issue.EntryIndex < Definition.entries.Count
                ? Definition.entries[issue.EntryIndex].name + ": "
                : string.Empty;
            string iconName = issue.Severity == PrefsIssueSeverity.Error ? ErrorIconName
                : issue.Severity == PrefsIssueSeverity.Warning ? WarningIconName : InfoIconName;
            GUIContent content = new GUIContent(prefix + issue.Message, EditorGUIUtility.IconContent(iconName).image, issue.Message);

            if (GUILayout.Button(content, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f)) &&
                issue.EntryIndex >= 0)
            {
                Select(issue.EntryIndex);
            }
        }

        private static MessageType ToMessageType(PrefsIssueSeverity severity)
        {
            switch (severity)
            {
                case PrefsIssueSeverity.Error: return MessageType.Error;
                case PrefsIssueSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        #endregion
    }
}
