using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Lists every PlayerPrefs and EditorPrefs key with its native type and current value, and shows which
    /// definition (if any) declares it. Rows are virtualized, so thousands of EditorPrefs stay responsive.
    /// </summary>
    internal sealed class PrefsViewerWindow : EditorWindow
    {
        private const string WindowTitle = "Prefs Viewer";
        private const float RowHeight = 20f;
        private const float HeaderHeight = 20f;
        private const float TypeColumnWidth = 60f;
        private const float OwnerColumnWidthRatio = 0.22f;
        private const float KeyColumnWidthRatio = 0.32f;
        private const float DetailHeight = 110f;
        private const float SearchFieldMaxWidth = 260f;
        private const float SearchScopeWidth = 82f;
        private const float LiveRefreshInterval = 0.5f;
        private const int RowPreviewLength = 200;
        private const string Ellipsis = "…";
        private const string SearchScopeTooltip = "Choose which column the search text is matched against.";
        private const string NoKeysMessage = "No keys to show.";
        private const string NoMatchesFormat = "No keys match \"{0}\" in {1}.";

        private static readonly string[] s_StorageLabels = { "PlayerPrefs", "EditorPrefs" };
        private static readonly string[] s_SearchScopeLabels = { "All Columns", "Key", "Value", "Type", "Defined In" };
        private static readonly GUIContent[] s_SearchScopeContents =
            Array.ConvertAll(s_SearchScopeLabels, label => new GUIContent(label, SearchScopeTooltip));

        /// <summary>Column(s) the search text is matched against.</summary>
        private enum SearchScope
        {
            All,
            Key,
            Value,
            Type,
            DefinedIn
        }

        [SerializeField] private PrefsStorage m_Storage = PrefsStorage.PlayerPrefs;
        [SerializeField] private string m_SearchText = string.Empty;
        [SerializeField] private SearchScope m_SearchScope = SearchScope.All;
        [SerializeField] private bool m_DefinedOnly;
        [SerializeField] private string m_SelectedKey;
        [SerializeField] private Vector2 m_Scroll;
        [SerializeField] private Vector2 m_DetailScroll;

        [NonSerialized] private readonly List<Row> m_Rows = new List<Row>();
        [NonSerialized] private readonly List<Row> m_VisibleRows = new List<Row>();
        [NonSerialized] private string m_Location = string.Empty;
        [NonSerialized] private string m_Error;
        [NonSerialized] private SearchField m_SearchField;
        [NonSerialized] private double m_NextLiveRefresh;
        [NonSerialized] private bool m_FilterDirty = true;
        [NonSerialized] private GUIStyle m_CellStyle;

        private sealed class Row
        {
            public string Key;
            public PrefsValueKind Kind;
            public string Value;
            public string Owner;
            public string OwnerAssetPath;
        }

        private IPrefsStore Store => PrefsStores.Get(m_Storage);

        private GUIStyle CellStyle => m_CellStyle ??= new GUIStyle(EditorStyles.label)
        {
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft
        };

        /// <summary>Opens the viewer.</summary>
        public static void Open()
        {
            PrefsViewerWindow window = GetWindow<PrefsViewerWindow>(WindowTitle);
            window.titleContent = new GUIContent(WindowTitle, PrefsDefinitionImporter.Icon);
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            m_SearchField = new SearchField();
            PrefsDefinitionRegistry.Changed += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Refresh();
        }

        private void OnDisable()
        {
            PrefsDefinitionRegistry.Changed -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnFocus() => Refresh();

        private void OnPlayModeStateChanged(PlayModeStateChange change) => Refresh();

        private void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < m_NextLiveRefresh) return;

            m_NextLiveRefresh = EditorApplication.timeSinceStartup + LiveRefreshInterval;
            RefreshValues();
            if (!string.IsNullOrEmpty(m_SearchText)) m_FilterDirty = true;
            Repaint();
        }

        #region Data

        private void Refresh()
        {
            PrefsKeyEnumerator.Result result = PrefsKeyEnumerator.GetKeys(m_Storage);
            m_Location = result.Location;
            m_Error = result.Error;

            Dictionary<string, (string owner, string path)> owners = CollectOwners();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            m_Rows.Clear();

            foreach (string key in result.Keys)
            {
                seen.Add(key);
                owners.TryGetValue(key, out (string owner, string path) ownerInfo);
                m_Rows.Add(new Row { Key = key, Owner = ownerInfo.owner, OwnerAssetPath = ownerInfo.path });
            }

            foreach (KeyValuePair<string, (string owner, string path)> pair in owners)
            {
                if (seen.Contains(pair.Key)) continue;
                m_Rows.Add(new Row { Key = pair.Key, Owner = pair.Value.owner, OwnerAssetPath = pair.Value.path });
            }

            m_Rows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            RefreshValues();
            m_FilterDirty = true;
            Repaint();
        }

        private void RefreshValues()
        {
            IPrefsStore store = Store;
            foreach (Row row in m_Rows)
            {
                row.Kind = PrefsStores.DetectKind(store, row.Key);
                row.Value = ReadValue(store, row.Key, row.Kind);
            }
        }

        private static string ReadValue(IPrefsStore store, string key, PrefsValueKind kind)
        {
            switch (kind)
            {
                case PrefsValueKind.Int: return PrefsConverter.Format(store.GetInt(key, 0));
                case PrefsValueKind.Float: return PrefsConverter.Format(store.GetFloat(key, 0f));
                case PrefsValueKind.String: return store.GetString(key, string.Empty);
                case PrefsValueKind.Missing: return "(not set)";
                default: return "(unknown)";
            }
        }

        private Dictionary<string, (string owner, string path)> CollectOwners()
        {
            Dictionary<string, (string owner, string path)> owners = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (string path in PrefsDefinitionRegistry.FindAllDefinitionPaths())
            {
                PrefsDefinition definition = PrefsDefinitionRegistry.TryLoad(path);
                if (definition == null || definition.storage != m_Storage) continue;

                string className = PrefsDefinitionPaths.GetEffectiveClassName(definition, path);
                foreach (PrefsEntry entry in definition.entries)
                {
                    string key = definition.GetEffectiveKey(entry);
                    if (!string.IsNullOrEmpty(key) && !owners.ContainsKey(key)) owners.Add(key, (className + "." + entry.name, path));
                }
            }

            return owners;
        }

        private void UpdateFilter()
        {
            if (!m_FilterDirty) return;

            m_FilterDirty = false;
            m_VisibleRows.Clear();
            foreach (Row row in m_Rows)
            {
                if (m_DefinedOnly ? row.Owner == null : row.Kind == PrefsValueKind.Missing) continue;
                if (!string.IsNullOrEmpty(m_SearchText) && !Matches(row, m_SearchText.Trim(), m_SearchScope)) continue;
                m_VisibleRows.Add(row);
            }
        }

        /// <summary>Returns true when <paramref name="search"/> occurs (case-insensitively) in the column(s) selected by <paramref name="scope"/>.</summary>
        private static bool Matches(Row row, string search, SearchScope scope)
        {
            switch (scope)
            {
                case SearchScope.Key: return Contains(row.Key, search);
                case SearchScope.Value: return Contains(row.Value, search);
                case SearchScope.Type: return Contains(row.Kind.ToString(), search);
                case SearchScope.DefinedIn: return Contains(row.Owner, search);
                default:
                    return Contains(row.Key, search) || Contains(row.Value, search) ||
                           Contains(row.Kind.ToString(), search) || Contains(row.Owner, search);
            }
        }

        private static bool Contains(string text, string search) =>
            text != null && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        #endregion

        #region GUI

        private void OnGUI()
        {
            HandleSearchShortcut();
            DrawToolbar();
            UpdateFilter();

            string summary = $"{m_VisibleRows.Count} of {m_Rows.Count} keys";
            if (!string.IsNullOrEmpty(m_Location)) summary += "   ·   " + m_Location;
            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(m_Error)) EditorGUILayout.HelpBox(m_Error, MessageType.Warning);
            if (m_Storage == PrefsStorage.EditorPrefs)
            {
                EditorGUILayout.HelpBox("EditorPrefs are shared by every Unity project on this machine, and many keys belong to Unity itself.",
                    MessageType.None);
            }

            DrawTable();
            DrawDetail();
        }

        /// <summary>Focuses the search field on Ctrl+F (Cmd+F on macOS).</summary>
        private void HandleSearchShortcut()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.F || !EditorGUI.actionKey) return;

            m_SearchField.SetFocus();
            current.Use();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                int storageIndex = GUILayout.Toolbar((int)m_Storage, s_StorageLabels, EditorStyles.toolbarButton, GUILayout.Width(200f));
                if (EditorGUI.EndChangeCheck())
                {
                    m_Storage = (PrefsStorage)storageIndex;
                    m_SelectedKey = null;
                    Refresh();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();

                EditorGUI.BeginChangeCheck();
                m_DefinedOnly = GUILayout.Toggle(m_DefinedOnly,
                    new GUIContent("Defined Only", "Show only keys declared in definition assets (including unset ones)."),
                    EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck()) m_FilterDirty = true;

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                m_SearchScope = (SearchScope)EditorGUILayout.Popup(GUIContent.none, (int)m_SearchScope, s_SearchScopeContents,
                    EditorStyles.toolbarPopup, GUILayout.Width(SearchScopeWidth));
                m_SearchText = m_SearchField.OnToolbarGUI(m_SearchText, GUILayout.MaxWidth(SearchFieldMaxWidth));
                if (EditorGUI.EndChangeCheck()) m_FilterDirty = true;

                if (m_Storage == PrefsStorage.PlayerPrefs &&
                    GUILayout.Button(new GUIContent("Delete All", "Delete every PlayerPrefs key of this project."), EditorStyles.toolbarButton))
                {
                    if (EditorUtility.DisplayDialog("Delete All PlayerPrefs",
                            $"Delete every PlayerPrefs key for '{PlayerSettings.productName}'? This cannot be undone.", "Delete All", "Cancel"))
                    {
                        PlayerPrefs.DeleteAll();
                        PlayerPrefs.Save();
                        Refresh();
                    }

                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawTable()
        {
            Rect area = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (area.height <= HeaderHeight) return;

            Rect headerRect = new Rect(area.x, area.y, area.width, HeaderHeight);
            Rect bodyRect = new Rect(area.x, area.y + HeaderHeight, area.width, area.height - HeaderHeight);
            float contentWidth = bodyRect.width - GUI.skin.verticalScrollbar.fixedWidth;
            ColumnLayout columns = new ColumnLayout(contentWidth);

            if (Event.current.type == EventType.Repaint) EditorStyles.toolbar.Draw(headerRect, false, false, false, false);
            GUI.Label(columns.Key(headerRect), "Key", EditorStyles.miniBoldLabel);
            GUI.Label(columns.Type(headerRect), "Type", EditorStyles.miniBoldLabel);
            GUI.Label(columns.Value(headerRect), "Value", EditorStyles.miniBoldLabel);
            GUI.Label(columns.Owner(headerRect), "Defined In", EditorStyles.miniBoldLabel);

            if (m_VisibleRows.Count == 0)
            {
                string message = string.IsNullOrEmpty(m_SearchText)
                    ? NoKeysMessage
                    : string.Format(NoMatchesFormat, m_SearchText.Trim(), s_SearchScopeLabels[(int)m_SearchScope]);
                GUI.Label(bodyRect, message, EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, contentWidth, m_VisibleRows.Count * RowHeight);
            m_Scroll = GUI.BeginScrollView(bodyRect, m_Scroll, viewRect);

            int first = Mathf.Max(0, Mathf.FloorToInt(m_Scroll.y / RowHeight));
            int last = Mathf.Min(m_VisibleRows.Count - 1, Mathf.CeilToInt((m_Scroll.y + bodyRect.height) / RowHeight));
            for (int index = first; index <= last; index++)
            {
                DrawRow(new Rect(0f, index * RowHeight, contentWidth, RowHeight), m_VisibleRows[index], index, columns);
            }

            GUI.EndScrollView();
        }

        private void DrawRow(Rect rowRect, Row row, int index, ColumnLayout columns)
        {
            bool isSelected = row.Key == m_SelectedKey;
            if (Event.current.type == EventType.Repaint)
            {
                if (isSelected) EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.4f));
                else if (index % 2 == 1) EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.06f));
            }

            GUI.Label(columns.Key(rowRect), new GUIContent(row.Key, row.Key), CellStyle);
            GUI.Label(columns.Type(rowRect), row.Kind.ToString(), CellStyle);
            GUI.Label(columns.Value(rowRect), new GUIContent(Truncate(row.Value), Truncate(row.Value)), CellStyle);
            GUI.Label(columns.Owner(rowRect), row.Owner ?? string.Empty, EditorStyles.miniLabel);

            Event current = Event.current;
            if (current.type == EventType.MouseDown && rowRect.Contains(current.mousePosition))
            {
                m_SelectedKey = row.Key;
                if (current.button == 1) ShowRowMenu(row);
                current.Use();
                Repaint();
            }
        }

        private void DrawDetail()
        {
            Row selected = m_VisibleRows.Find(row => row.Key == m_SelectedKey);
            if (selected == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(DetailHeight)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{selected.Key}  ({selected.Kind})", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy Value", EditorStyles.miniButtonLeft)) EditorGUIUtility.systemCopyBuffer = selected.Value;
                    using (new EditorGUI.DisabledScope(selected.OwnerAssetPath == null))
                    {
                        if (GUILayout.Button("Open Definition", EditorStyles.miniButtonMid)) PrefsDefinitionWindow.Open(selected.OwnerAssetPath);
                    }

                    using (new EditorGUI.DisabledScope(selected.Kind == PrefsValueKind.Missing))
                    {
                        if (GUILayout.Button("Delete Key", EditorStyles.miniButtonRight)) DeleteKey(selected);
                    }
                }

                m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
                EditorGUILayout.SelectableLabel(selected.Value ?? string.Empty, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void ShowRowMenu(Row row)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Key"), false, () => EditorGUIUtility.systemCopyBuffer = row.Key);
            menu.AddItem(new GUIContent("Copy Value"), false, () => EditorGUIUtility.systemCopyBuffer = row.Value);
            if (row.OwnerAssetPath != null)
            {
                menu.AddItem(new GUIContent("Open Definition"), false, () => PrefsDefinitionWindow.Open(row.OwnerAssetPath));
            }

            menu.AddSeparator(string.Empty);
            if (row.Kind != PrefsValueKind.Missing) menu.AddItem(new GUIContent("Delete Key"), false, () => DeleteKey(row));
            else menu.AddDisabledItem(new GUIContent("Delete Key"));
            menu.ShowAsContext();
        }

        private void DeleteKey(Row row)
        {
            string storageName = s_StorageLabels[(int)m_Storage];
            if (!EditorUtility.DisplayDialog("Delete Key", $"Delete '{row.Key}' from {storageName}?", "Delete", "Cancel")) return;

            Store.DeleteKey(row.Key);
            Store.Save();
            Refresh();
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            string singleLine = value.Replace('\n', ' ').Replace('\r', ' ');
            return singleLine.Length > RowPreviewLength ? singleLine.Substring(0, RowPreviewLength) + Ellipsis : singleLine;
        }

        private readonly struct ColumnLayout
        {
            private const float Padding = 4f;

            private readonly float m_KeyWidth;
            private readonly float m_ValueWidth;
            private readonly float m_OwnerWidth;

            public ColumnLayout(float totalWidth)
            {
                m_KeyWidth = totalWidth * KeyColumnWidthRatio;
                m_OwnerWidth = totalWidth * OwnerColumnWidthRatio;
                m_ValueWidth = Mathf.Max(0f, totalWidth - m_KeyWidth - m_OwnerWidth - TypeColumnWidth);
            }

            public Rect Key(Rect row) => new Rect(row.x + Padding, row.y, m_KeyWidth - Padding * 2f, row.height);

            public Rect Type(Rect row) => new Rect(row.x + m_KeyWidth + Padding, row.y, TypeColumnWidth - Padding * 2f, row.height);

            public Rect Value(Rect row) =>
                new Rect(row.x + m_KeyWidth + TypeColumnWidth + Padding, row.y, m_ValueWidth - Padding * 2f, row.height);

            public Rect Owner(Rect row) =>
                new Rect(row.x + m_KeyWidth + TypeColumnWidth + m_ValueWidth + Padding, row.y, m_OwnerWidth - Padding * 2f, row.height);
        }

        #endregion
    }
}
