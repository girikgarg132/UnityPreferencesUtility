using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>
    /// Lists every key stored in PlayerPrefs or EditorPrefs. Unity has no API for this, so the platform storage is read
    /// directly: the registry on Windows, property lists on macOS and XML files on Linux. Values are always read
    /// back through the Unity APIs, so only key names depend on the platform format.
    /// </summary>
    public static class PrefsKeyEnumerator
    {
        private const string WindowsHashSuffixPattern = @"_h\d+$";
        private const string WindowsEditorPrefsKey = @"Software\Unity Technologies\Unity Editor 5.x";
        private const string WindowsPlayerPrefsKeyFormat = @"Software\Unity\UnityEditor\{0}\{1}";
        private const string MacDefaultsTool = "/usr/bin/defaults";
        private const string MacEditorPrefsDomain = "com.unity3d.UnityEditor5.x";
        private const string MacPlayerPrefsDomainFormat = "unity.{0}.{1}";
        private const string LinuxPlayerPrefsPathFormat = ".config/unity3d/{0}/{1}/prefs";
        private const string LinuxEditorPrefsPath = ".local/share/unity3d/prefs";
        private const int ProcessTimeoutMilliseconds = 5000;

#if UNITY_EDITOR_WIN
        private static readonly Regex s_WindowsHashSuffix = new Regex(WindowsHashSuffixPattern, RegexOptions.Compiled);
#endif

        /// <summary>Result of an enumeration.</summary>
        public sealed class Result
        {
            /// <summary>Distinct key names, sorted ordinally (case-insensitive).</summary>
            public readonly List<string> Keys = new List<string>();

            /// <summary>Human-readable storage location.</summary>
            public string Location = string.Empty;

            /// <summary>Error message, or <c>null</c>.</summary>
            public string Error;
        }

        /// <summary>Enumerates the keys of <paramref name="storage"/>.</summary>
        public static Result GetKeys(PrefsStorage storage)
        {
            Result result = new Result();
            if (storage == PrefsStorage.PlayerPrefs) PlayerPrefs.Save();

            try
            {
#if UNITY_EDITOR_WIN
                ReadWindows(storage, result);
#elif UNITY_EDITOR_OSX
                ReadMac(storage, result);
#elif UNITY_EDITOR_LINUX
                ReadLinux(storage, result);
#else
                result.Error = "Listing preferences is not supported on this platform.";
#endif
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
            }

            HashSet<string> unique = new HashSet<string>(result.Keys, StringComparer.Ordinal);
            result.Keys.Clear();
            result.Keys.AddRange(unique);
            result.Keys.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

#if UNITY_EDITOR_WIN
        private static void ReadWindows(PrefsStorage storage, Result result)
        {
            string subKeyPath = storage == PrefsStorage.EditorPrefs
                ? WindowsEditorPrefsKey
                : string.Format(WindowsPlayerPrefsKeyFormat, PlayerSettings.companyName, PlayerSettings.productName);
            result.Location = @"HKEY_CURRENT_USER\" + subKeyPath;

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath))
            {
                if (key == null) return;

                foreach (string valueName in key.GetValueNames())
                {
                    result.Keys.Add(s_WindowsHashSuffix.Replace(valueName, string.Empty));
                }
            }
        }
#endif

#if UNITY_EDITOR_OSX
        private static void ReadMac(PrefsStorage storage, Result result)
        {
            string domain = storage == PrefsStorage.EditorPrefs
                ? MacEditorPrefsDomain
                : string.Format(MacPlayerPrefsDomainFormat, PlayerSettings.companyName, PlayerSettings.productName);
            result.Location = $"~/Library/Preferences/{domain}.plist";

            ProcessStartInfo startInfo = new ProcessStartInfo(MacDefaultsTool, $"export \"{domain}\" -")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output;
            using (Process process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Could not start 'defaults'.");

                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(ProcessTimeoutMilliseconds);
            }

            if (string.IsNullOrWhiteSpace(output)) return;

            XmlDocument document = LoadXml(output);
            XmlNode dictionary = document.SelectSingleNode("/plist/dict");
            if (dictionary == null) return;

            foreach (XmlNode child in dictionary.ChildNodes)
            {
                if (child.Name == "key") result.Keys.Add(child.InnerText);
            }
        }
#endif

#if UNITY_EDITOR_LINUX
        private static void ReadLinux(PrefsStorage storage, Result result)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string relativePath = storage == PrefsStorage.EditorPrefs
                ? LinuxEditorPrefsPath
                : string.Format(LinuxPlayerPrefsPathFormat, PlayerSettings.companyName, PlayerSettings.productName);
            string path = Path.Combine(home, relativePath);
            result.Location = path;
            if (!File.Exists(path)) return;

            XmlDocument document = LoadXml(File.ReadAllText(path));
            XmlNodeList nodes = document.SelectNodes("//pref");
            if (nodes == null) return;

            foreach (XmlNode node in nodes)
            {
                string name = node.Attributes?["name"]?.Value;
                if (!string.IsNullOrEmpty(name)) result.Keys.Add(name);
            }
        }
#endif

        private static XmlDocument LoadXml(string xml)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            XmlDocument document = new XmlDocument { XmlResolver = null };
            using (StringReader stringReader = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(stringReader, settings))
            {
                document.Load(reader);
            }

            return document;
        }
    }
}
