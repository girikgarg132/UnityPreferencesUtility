using UnityEditor;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>The native value kind a key is stored as.</summary>
    public enum PrefsValueKind
    {
        /// <summary>The key does not exist.</summary>
        Missing,

        /// <summary>Stored with <c>SetInt</c> (EditorPrefs booleans are stored as integers too).</summary>
        Int,

        /// <summary>Stored with <c>SetFloat</c>.</summary>
        Float,

        /// <summary>Stored with <c>SetString</c>.</summary>
        String,

        /// <summary>The key exists but its kind could not be determined.</summary>
        Unknown
    }

    /// <summary>Uniform access to PlayerPrefs and EditorPrefs for editor tooling.</summary>
    public interface IPrefsStore
    {
        /// <summary>Which storage this instance wraps.</summary>
        PrefsStorage Storage { get; }

        /// <summary>See <c>HasKey</c>.</summary>
        bool HasKey(string key);

        /// <summary>See <c>DeleteKey</c>.</summary>
        void DeleteKey(string key);

        /// <summary>See <c>GetInt</c>.</summary>
        int GetInt(string key, int fallback);

        /// <summary>See <c>SetInt</c>.</summary>
        void SetInt(string key, int value);

        /// <summary>See <c>GetFloat</c>.</summary>
        float GetFloat(string key, float fallback);

        /// <summary>See <c>SetFloat</c>.</summary>
        void SetFloat(string key, float value);

        /// <summary>See <c>GetString</c>.</summary>
        string GetString(string key, string fallback);

        /// <summary>See <c>SetString</c>.</summary>
        void SetString(string key, string value);

        /// <summary>Reads a boolean the way generated code does for this storage.</summary>
        bool GetBool(string key, bool fallback);

        /// <summary>Writes a boolean the way generated code does for this storage.</summary>
        void SetBool(string key, bool value);

        /// <summary>Flushes pending writes to disk (no-op for EditorPrefs).</summary>
        void Save();
    }

    /// <summary>Factory and helpers for <see cref="IPrefsStore"/>.</summary>
    public static class PrefsStores
    {
        private const int ProbeIntA = 1;
        private const int ProbeIntB = 2;
        private const float ProbeFloatA = 1.5f;
        private const float ProbeFloatB = 2.5f;
        private const string ProbeStringA = "\u0001PrefsUtilityProbeA";
        private const string ProbeStringB = "\u0001PrefsUtilityProbeB";

        /// <summary>The PlayerPrefs store.</summary>
        public static readonly IPrefsStore PlayerPrefsStore = new PlayerPrefsAdapter();

        /// <summary>The EditorPrefs store.</summary>
        public static readonly IPrefsStore EditorPrefsStore = new EditorPrefsAdapter();

        /// <summary>Returns the store for <paramref name="storage"/>.</summary>
        public static IPrefsStore Get(PrefsStorage storage) =>
            storage == PrefsStorage.EditorPrefs ? EditorPrefsStore : PlayerPrefsStore;

        /// <summary>
        /// Detects the native kind of <paramref name="key"/> without knowing it in advance. A getter returns its fallback
        /// when the stored kind differs, so a kind is confirmed when two different fallbacks yield the same value.
        /// </summary>
        public static PrefsValueKind DetectKind(IPrefsStore store, string key)
        {
            if (string.IsNullOrEmpty(key) || !store.HasKey(key)) return PrefsValueKind.Missing;
            if (store.GetInt(key, ProbeIntA) == store.GetInt(key, ProbeIntB)) return PrefsValueKind.Int;
            if (store.GetString(key, ProbeStringA) == store.GetString(key, ProbeStringB)) return PrefsValueKind.String;

            float first = store.GetFloat(key, ProbeFloatA);
            float second = store.GetFloat(key, ProbeFloatB);
            if (first.Equals(second)) return PrefsValueKind.Float;

            return PrefsValueKind.Unknown;
        }

        private sealed class PlayerPrefsAdapter : IPrefsStore
        {
            public PrefsStorage Storage => PrefsStorage.PlayerPrefs;
            public bool HasKey(string key) => PlayerPrefs.HasKey(key);
            public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
            public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
            public void SetString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
            public bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
            public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
            public void Save() => PlayerPrefs.Save();
        }

        private sealed class EditorPrefsAdapter : IPrefsStore
        {
            public PrefsStorage Storage => PrefsStorage.EditorPrefs;
            public bool HasKey(string key) => EditorPrefs.HasKey(key);
            public void DeleteKey(string key) => EditorPrefs.DeleteKey(key);
            public int GetInt(string key, int fallback) => EditorPrefs.GetInt(key, fallback);
            public void SetInt(string key, int value) => EditorPrefs.SetInt(key, value);
            public float GetFloat(string key, float fallback) => EditorPrefs.GetFloat(key, fallback);
            public void SetFloat(string key, float value) => EditorPrefs.SetFloat(key, value);
            public string GetString(string key, string fallback) => EditorPrefs.GetString(key, fallback);
            public void SetString(string key, string value) => EditorPrefs.SetString(key, value ?? string.Empty);
            public bool GetBool(string key, bool fallback) => EditorPrefs.GetBool(key, fallback);
            public void SetBool(string key, bool value) => EditorPrefs.SetBool(key, value);
            public void Save() { }
        }
    }
}
