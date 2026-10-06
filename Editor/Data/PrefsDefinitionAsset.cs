using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Imported representation of a <c>.prefsdef</c> file. The file itself is the source of truth;
    /// this object only exists so the asset has a type, an icon and a read-only snapshot for the Inspector.
    /// </summary>
    public sealed class PrefsDefinitionAsset : ScriptableObject
    {
        [SerializeField] private PrefsDefinition m_Definition = new PrefsDefinition();
        [SerializeField] private string m_ImportError = string.Empty;

        /// <summary>Snapshot of the definition at import time. Do not modify; edit the file through the editor window.</summary>
        public PrefsDefinition Definition => m_Definition;

        /// <summary>Parse error message, or empty when the file is valid.</summary>
        public string ImportError => m_ImportError;

        /// <summary>Initializes the snapshot. Called by the importer.</summary>
        internal void Initialize(PrefsDefinition definition, string importError)
        {
            m_Definition = definition ?? new PrefsDefinition();
            m_ImportError = importError ?? string.Empty;
        }
    }
}
