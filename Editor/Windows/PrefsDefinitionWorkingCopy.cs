using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Hidden, unsaved object that holds the definition being edited. Using a <see cref="ScriptableObject"/> gives
    /// the editor window Undo/Redo support and lets unsaved edits survive domain reloads and Play Mode.
    /// </summary>
    internal sealed class PrefsDefinitionWorkingCopy : ScriptableObject
    {
        /// <summary>The definition being edited.</summary>
        public PrefsDefinition definition = new PrefsDefinition();
    }
}
