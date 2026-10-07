# Preferences Utility

A visual editor and C# code generator for Unity's `PlayerPrefs` and `EditorPrefs`.

Define preferences in a version-controlled `.prefsdef` asset, set default values in the Editor, and save. You get a strongly typed, auto-generated static class whose properties call `PlayerPrefs` / `EditorPrefs` directly, with no reflection, no generic `Get<T>` dispatch, and no boxing.

```csharp
// Generated from GamePrefs.prefsdef
if (!GamePrefs.IsTutorialDone)
{
    StartTutorial();
    GamePrefs.IsTutorialDone = true;
}

GamePrefs.MasterVolume = 0.8f;
GamePrefs.Difficulty = Difficulty.Hard;      // any enum
GamePrefs.SpawnPoint = transform.position;   // Unity structs
GamePrefs.CareerData = careerData;           // any class, via Newtonsoft Json
```

| | |
| --- | --- |
| **Author** | Girik Garg |
| **Unity** | 6.0 LTS or newer (developed and verified in Unity 6000.6) |
| **Render pipeline** | Any |
| **Dependencies** | `com.unity.nuget.newtonsoft-json` (installed automatically) |
| **Runtime footprint** | Two small static classes (`PrefsConverter`, `PrefsJson`) plus your generated code. All tooling is Editor-only |
| **License** | MIT |

## Features

- **Visual definition editor**: add, reorder, duplicate, and document preferences; pick types from a searchable list; edit defaults with native Unity fields.
- **Generated C#**: one `static partial class` per definition, with direct `PlayerPrefs` / `EditorPrefs` calls, `const` keys, and `const` / `static readonly` defaults.
- **Every built-in type**: `bool`, `int`, `float`, `string`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `long`, `ulong`, `double`, `decimal`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, and all common Unity structs. Any enum works too, including `[Flags]` and `long`-backed enums.
- **Any other type** (classes, structs, `List<T>`, `Dictionary<K,V>`, arrays, nullables, ...) through Newtonsoft Json, with converters for Unity structs.
- **Live values**: see and edit what is currently stored for each preference, even during Play Mode.
- **Prefs Viewer**: a separate window listing every PlayerPrefs and EditorPrefs key with its type, its value, and the definition that declares it.
- **PlayerPrefs and EditorPrefs**: EditorPrefs code is wrapped in `#if UNITY_EDITOR` automatically.
- **Multiple definitions** per project, each with its own class name, namespace, output path, key prefix, and accessibility.
- **Unsaved-change protection**: no auto-save. Unity's standard *Save / Discard / Cancel* prompt appears when you close the window or the Editor, and edits survive script reloads and Play Mode. Undo/Redo is fully supported.
- **Version-control friendly**: definitions are indented Json; generated files are deterministic (no timestamps, LF line endings) and are only rewritten when their content changes.
- **Safe by design**: validation catches invalid names, duplicate keys, reserved members, unresolved types, and output paths that would overwrite hand-written files.

## Installation

There are two ways to install the package. With either one, Newtonsoft Json is installed automatically as a dependency.

### Option 1: Git URL

In **Window > Package Manager**, choose **+ > Install package from git URL** and enter:

```text
https://github.com/girikgarg132/UnityPreferencesUtility.git
```

Append a tag to pin a version, for example `https://github.com/girikgarg132/UnityPreferencesUtility.git#v1.0.0`. Git must be installed on your machine.

### Option 2: Tarball (.tgz)

1. Download `com.girikgarg.preferencesutility-<version>.tgz` from the [Releases page](https://github.com/girikgarg132/UnityPreferencesUtility/releases).
2. In **Window > Package Manager**, choose **+ > Install package from tarball** and select the downloaded file.

Unity stores the tarball's path in `Packages/manifest.json`. To share the installation through version control, keep the `.tgz` inside your project, for example in a `Packages` subfolder, so the path resolves on every machine.

## Quick start

1. In the Project window, right-click a folder and choose **Create > Girik Garg > Preferences Utility > Player Prefs Definition** (or **Editor Prefs Definition**). Name it, for example `GamePrefs`.
2. Double-click the asset to open the editor.
3. Press **+** and choose a type. Set the **Name** (the C# property name) and the **Default Value**.
4. Press **Save** (Ctrl+S / Cmd+S). The definition is written, and `GamePrefs.cs` is generated next to it.
5. Use it from any script: `GamePrefs.MasterVolume = 0.5f;`

### Code generation settings

| Setting | Default | Notes |
| --- | --- | --- |
| Storage | PlayerPrefs | EditorPrefs code is wrapped in `#if UNITY_EDITOR`. |
| Class Name | Asset file name | Must be a valid C# identifier. |
| Namespace | Global namespace | For example `MyGame.Settings`. |
| Output Path | `<asset folder>/<ClassName>.cs` | Any `.cs` path under `Assets/` or `Packages/`. Use **…** to browse. |
| Key Prefix | None (EditorPrefs: `<ProductName>.`) | Prepended to every key. Recommended for EditorPrefs, which are shared by all projects. |
| Accessibility | Public | `public` or `internal`. |

Leave a setting empty to use its default. Defaults follow the asset when it's renamed or moved, like Input Actions. When the output path changes, the previously generated file is moved (keeping its GUID) or deleted, so you never get a duplicate class.

### Preference settings

| Setting | Notes |
| --- | --- |
| Name | The generated property name. |
| Key | The storage key without the prefix. Empty uses the name. |
| Type | A built-in type, **Enum**, or **Custom (Json)**. |
| Type Name | For Enum and Custom types: any C# type expression, such as `MyGame.Difficulty`, `KeyCode`, `List<int>`, `Dictionary<string, Vector3>`, `int[]`, or `float?`. Use **Pick** to browse project types. Short names resolve against the definition namespace, then `System`, `System.Collections.Generic`, and `UnityEngine`. |
| Summary | Becomes the XML documentation of the property. |
| Default Value | Edited with the matching Unity field (toggle, vector, color, enum popup, ...). For Json types, edit the Json directly or press **From Type** to start from a new instance. Empty Json means `default` (`null` for classes). |

### Live values

The **Current Value** box reads the stored value exactly the way the generated code does. Edits are written immediately. You can also **Set to Default** or **Delete Key**. The list shows a preview of every stored value. Stored values that are missing, stored as a different native type, or unparseable are flagged clearly.

### Saving and unsaved changes

There is no auto-save. Changes are kept in an in-memory working copy until you press **Save**:

- The tab shows Unity's unsaved-changes indicator.
- Closing the window or quitting the Editor shows Unity's standard *Save / Discard / Cancel* dialog, the same one Unity uses for its own asset editors.
- Script reloads and entering Play Mode keep your unsaved edits.
- If the file changes on disk (for example after a VCS pull) while you have unsaved edits, you can choose to **Reload** or **Keep Mine**.

Saving always writes the definition, even when there are validation errors, so no work is lost. C# is generated only when there are no errors.

## The Prefs Viewer

Open **Window > Preferences Utility > Prefs Viewer** (also under **Tools**), or use the button in the definition editor.

- Switch between **PlayerPrefs** (for this project's company and product name) and **EditorPrefs**.
- Search by key, value, or owning definition. Turn on **Defined Only** to show just the keys your definitions declare, including unset ones.
- Select a row to see the full value, copy it, open its definition, or delete the key.
- Values refresh automatically during Play Mode.

Unity has no API for listing keys, so key names are read from the platform storage (values are always read through the Unity API):

| Editor platform | PlayerPrefs | EditorPrefs |
| --- | --- | --- |
| Windows | `HKCU\Software\Unity\UnityEditor\<Company>\<Product>` | `HKCU\Software\Unity Technologies\Unity Editor 5.x` |
| macOS | `~/Library/Preferences/unity.<Company>.<Product>.plist` | `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist` |
| Linux | `~/.config/unity3d/<Company>/<Product>/prefs` | `~/.local/share/unity3d/prefs` |

On Linux, EditorPrefs may only be flushed to disk when the Editor quits, so keys created in the current session can be missing from the list. Keys declared in a definition always appear.

## Generated code

For a definition with a few entries, the generated file looks like this (abridged):

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Preferences Utility (com.girikgarg.preferencesutility).
//     Source definition: Assets/Settings/GamePrefs.prefsdef
//     ...
// </auto-generated>
//------------------------------------------------------------------------------

namespace MyGame
{
    [global::System.CodeDom.Compiler.GeneratedCode("Preferences Utility", "1.0.0")]
    public static partial class GamePrefs
    {
        public static class Keys
        {
            public const string IsTutorialDone = "IsTutorialDone";
            public const string SpawnPoint = "SpawnPoint";
            public const string CareerData = "CareerData";
        }

        public static class Defaults
        {
            public const bool IsTutorialDone = false;
            public static readonly global::UnityEngine.Vector3 SpawnPoint = new global::UnityEngine.Vector3(0f, 1f, 0f);
            public static global::MyGame.CareerData CareerData => global::GirikGarg.PreferencesUtility.PrefsJson.ReadDefault<global::MyGame.CareerData>(DefaultsJson.CareerData, Keys.CareerData);
        }

        public static class DefaultsJson
        {
            public const string CareerData = "{\"Level\":1}";
        }

        public static global::System.Collections.Generic.IReadOnlyList<string> AllKeys => s_AllKeys;

        public static bool IsTutorialDone
        {
            get => global::UnityEngine.PlayerPrefs.GetInt(Keys.IsTutorialDone, Defaults.IsTutorialDone ? 1 : 0) != 0;
            set => global::UnityEngine.PlayerPrefs.SetInt(Keys.IsTutorialDone, value ? 1 : 0);
        }

        public static global::UnityEngine.Vector3 SpawnPoint
        {
            get => global::GirikGarg.PreferencesUtility.PrefsConverter.ToVector3(global::UnityEngine.PlayerPrefs.GetString(Keys.SpawnPoint, string.Empty), Defaults.SpawnPoint);
            set => global::UnityEngine.PlayerPrefs.SetString(Keys.SpawnPoint, global::GirikGarg.PreferencesUtility.PrefsConverter.Format(value));
        }

        public static global::MyGame.CareerData CareerData
        {
            get => global::GirikGarg.PreferencesUtility.PrefsJson.Read<global::MyGame.CareerData>(global::UnityEngine.PlayerPrefs.GetString(Keys.CareerData, string.Empty), DefaultsJson.CareerData, Keys.CareerData);
            set => global::UnityEngine.PlayerPrefs.SetString(Keys.CareerData, global::GirikGarg.PreferencesUtility.PrefsJson.Serialize<global::MyGame.CareerData>(value));
        }

        public static bool HasKey(string key) => global::UnityEngine.PlayerPrefs.HasKey(key);
        public static void DeleteKey(string key) => global::UnityEngine.PlayerPrefs.DeleteKey(key);
        public static void DeleteAll() { /* deletes only this class's keys */ }
        public static void Save() => global::UnityEngine.PlayerPrefs.Save(); // PlayerPrefs only
    }
}
```

| Member | Purpose |
| --- | --- |
| `Keys.X` | The storage key of `X` (a `const string`). |
| `Defaults.X` | The default value of `X`. Json types return a fresh instance on every call. |
| `AllKeys` | Every key declared by the class, in declaration order. |
| `HasKey(Keys.X)` / `DeleteKey(Keys.X)` | Check or reset a single preference. |
| `DeleteAll()` | Deletes only this class's keys. Other preferences are untouched. |
| `Save()` | Flushes PlayerPrefs to disk (PlayerPrefs definitions only). |

All type references are `global::`-qualified, so the generated code never conflicts with your own type names. The class is `partial`, so you can add helpers in another file:

```csharp
namespace MyGame
{
    public static partial class GamePrefs
    {
        public static void SetActiveCharacter(Element element, string characterid) =>
            UnityEngine.PlayerPrefs.SetString(element + "_Active_Character", characterid);
    }
}
```

These names are reserved and can't be used as preference names: `Keys`, `Defaults`, `DefaultsJson`, `AllKeys`, `HasKey`, `DeleteKey`, `DeleteAll`, `Save`, and the `object` members (`Equals`, `ToString`, ...).

## Supported types and storage format

Every type uses exactly one key. Types that `PlayerPrefs` stores natively use one native call; other built-in types use one `GetString` call plus allocation-light, culture-invariant parsing.

| Type | Stored as | Notes |
| --- | --- | --- |
| `bool` | Int (0/1) | Native `EditorPrefs.GetBool`/`SetBool` for EditorPrefs. |
| `int`, `byte`, `sbyte`, `short`, `ushort`, `char` | Int | |
| `uint` | Int | Lossless bit reinterpretation. |
| `float` | Float | |
| `string` | String | Setting `null` stores an empty string. |
| `long`, `ulong`, `double`, `decimal` | String | Invariant culture. Floating-point values use a lossless round-trip format. |
| `DateTime`, `DateTimeOffset` | String | ISO 8601 round-trip (`"o"`). `DateTimeKind` and offsets are preserved. |
| `TimeSpan`, `Guid` | String | Constant (`"c"`) and `"D"` formats. |
| `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`, `Rect`, `RectInt`, `Bounds`, `BoundsInt` | String | Comma-separated components, for example `1,2,3`. |
| `Color32` | Int | Packed RGBA. |
| Enums (`int`-compatible, `uint`) | Int | Stored by numeric value. Defaults are stored by member name in the definition. |
| Enums (`long`, `ulong`) | String | Numeric value. |
| Anything else | String (Json) | Newtonsoft Json; see below. |

Reading never throws. A missing, malformed, or wrongly typed value returns the default.

## Custom types (Json)

Select **Custom (Json)** and enter or pick a type. Values are serialized with [Newtonsoft Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@latest), using these robust defaults:

- **Unity struct converters**: `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`, `Color32`, `Rect`, `RectInt`, `Bounds`, and `BoundsInt` serialize as compact objects like `{"x":1,"y":2,"z":3}`. Without them, Newtonsoft fails on `Vector3`'s self-referencing `normalized` property.
- **`ObjectCreationHandling.Replace`**: lists initialized in constructors aren't duplicated on load (a classic Newtonsoft pitfall).
- **`TypeNameHandling.None`**: stored data can't instantiate arbitrary types. Use concrete types rather than interfaces or abstract classes.
- **Fault tolerant**: invalid stored Json logs a warning and returns the default value. Set `PrefsJson.LogFailures = false` to silence it.
- **Fresh instances**: every get deserializes a new object, so mutating a returned object never changes the stored value until you assign it back.

```csharp
CareerData data = GamePrefs.CareerData; // a copy
data.Wins++;
GamePrefs.CareerData = data;            // persist
```

To customize serialization (for example to add your own converters), replace the settings once at startup. Changing the settings can make previously stored data unreadable.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ConfigurePrefsJson()
{
    var settings = PrefsJson.CreateDefaultSettings();
    settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    PrefsJson.Settings = settings;
}
```

**IL2CPP / code stripping**: Newtonsoft creates objects through reflection. If *Managed Stripping Level* is Medium or High, mark your Json types with `[UnityEngine.Scripting.Preserve]` or add them to a `link.xml` file.

## Performance

- Native types compile to a single `PlayerPrefs` call with `const` keys. There's no reflection, boxing, generic dispatch, or string concatenation.
- Defaults are `const` where C# allows it, and `static readonly` otherwise.
- Other built-in types parse with `Span<T>` and `stackalloc`, without temporary arrays or `string.Split`.
- Json types reuse one cached `JsonSerializer`. Deserialization still runs on every get, so cache the result in your own code if you read a large object every frame.
- Unity keeps PlayerPrefs in memory. Call `Save()` at meaningful moments (Unity also saves on quit), not after every write.

## Version control

Commit these files:

| File | Why |
| --- | --- |
| `*.prefsdef` + `.meta` | The source of truth: indented Json that merges cleanly. |
| Generated `*.cs` + `.meta` | So teammates and CI can build without opening the editor. |

Generated files are deterministic and only rewritten when their content changes, so saving an unchanged definition never creates a diff. When a definition is renamed or moved, its generated file follows it automatically. Deleting a definition doesn't delete its generated file, so remove that file yourself.

If a merge conflict touches a generated file, resolve the `.prefsdef` conflict first, then use **Regenerate C#** (asset context menu: **Preferences Utility > Regenerate C# Code**). To regenerate every definition, use **Tools > Preferences Utility > Regenerate All Definitions**.

## Known limitations

- EditorPrefs are per machine and shared by all projects. Use a key prefix.
- Changing a preference's type, or reordering or renaming enum members, changes how existing stored values are read. Values that can't be read fall back to the default.
- `UnityEngine.Object` references (assets, scene objects) can't be stored. Store an id or path instead.
- The unsaved-changes prompt follows Unity's own asset editors: it appears when the window or the Editor closes, not when another window gains focus.

## Contributing

Issues and pull requests are welcome. When reporting a bug, include your Unity version, OS, the `.prefsdef` file (or a minimal repro), and the generated file if relevant.

For pull requests:

- Keep generated code deterministic and free of reflection.
- Keep all tooling in the Editor assembly. The runtime assembly should only contain what generated code calls.
- Add or update tests for new types or generator changes.

## License

MIT © Girik Garg. See [LICENSE](LICENSE).
