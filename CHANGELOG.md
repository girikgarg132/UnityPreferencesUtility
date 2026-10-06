# Changelog

All notable changes to this package are documented in this file.

## [1.0.0]

### Added

- `.prefsdef` definition assets (indented Json) with a `ScriptedImporter`, a read-only Inspector, and Create menu entries for PlayerPrefs and EditorPrefs.
- Definition editor window with a reorderable list, a detail panel, type picker, typed default-value fields, live stored values, validation, Undo/Redo, and Unity's unsaved-changes prompt.
- Deterministic C# generation: a `static partial class` per definition with direct `PlayerPrefs`/`EditorPrefs` calls, `Keys`, `Defaults`, `AllKeys`, `HasKey`, `DeleteKey`, `DeleteAll`, and `Save`.
- Configurable class name, namespace, output path, key prefix, and accessibility, with asset-relative defaults.
- Automatic relocation of generated files when a definition is renamed or moved, or its output path changes.
- Built-in support for every C# primitive, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, Unity vectors, `Quaternion`, colors, rects, bounds, and all enums.
- Newtonsoft Json support for any other type, with Unity struct converters and fault-tolerant reads.
- Prefs Viewer window listing every PlayerPrefs and EditorPrefs key on Windows, macOS, and Linux.
- Edit Mode and Play Mode tests.
