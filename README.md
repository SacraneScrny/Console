# Universal Console Overlay

Quake-style runtime console for Unity. Drop it in, press `` ` ``, type commands.
No prefabs, no TextMeshPro, no manual scene setup: the whole UI is built in code
on top of uGUI and the manager self-creates before the first scene loads.

## Features

- Toggleable overlay (`BackQuote` / `F1` by default), `DontDestroyOnLoad`.
- Explicit command packs: `IConsolePack` + `IConsole.Command / Var / VarReadOnly / Scope`.
- Automatic argument conversion (`int float bool string enum Vector2/3/4 Quaternion Color GameObject`, `default` values, `params`).
- Quoting for multi-word args: `spawn "big orc" 5`.
- Input history (`Up/Down`, persisted in `PlayerPrefs`), `Tab` completion with ghost text and a suggestion popup.
- Mirrors Unity logs into the console (info/warning/error filters) and optionally back to the Unity console.
- 60+ built-in commands and 16 variables covering app, screen, render, scene, prefs, physics, audio and more.
- Editor **Pack Generator**: scaffolds a pack class from your static methods.
- IL2CPP-safe: `[Preserve]` attributes plus `link.xml`, no runtime reflection for commands.

## Requirements

- Unity 2019.4 or newer (also works on Unity 6).
- uGUI only. Arial / legacy runtime font by default; ships with the `Silver` pixel font and an ASCII logo, both replaceable.

## Installation

1. Open Package Manager → `+` → **Add package from disk** → select this folder's `package.json`.
2. Or add it by git URL.
3. Press Play. The console manager is created automatically; press `` ` `` or `F1` to open it.

## Quick start

```csharp
// Any MonoBehaviour. The registry already exists at BeforeSceneLoad.
ConsoleManager.RegisterPack(new ExamplePack());
```

Built-in commands are registered automatically at startup (`StandardPack`).
To opt out before startup:

```csharp
StandardPack.Enabled = false;
```

## ConsoleManager settings

| Field | Default | Meaning |
|---|---|---|
| `autoDiscoverPacks` | true | Find all `IConsolePack` implementations via `TypeCache`, except `UniversalConsole*` internals |
| `startOpen` | false | Open console at startup |
| `pauseWhileOpen` | false | Set `Time.timeScale` to 0/1 while open |
| `captureUnityLogs` | true | Mirror Unity logs into the console |
| `captureInfoLogs` / `captureWarningLogs` / `captureErrorLogs` | true | Per-level mirror filters |
| `mirrorToUnityLog` | true | Duplicate console output to the Unity console |
| `showToggleButton` | true | Small `~` button for touch devices |
| `toggleKey` / `toggleKeyAlt` | BackQuote / F1 | Toggle keys (plus `Ctrl+Shift+C`) |
| `verboseStartup` | true | Log pack/command counts at startup |

## Writing a custom pack

```csharp
public sealed class ExamplePack : IConsolePack
{
    public void Build(IConsole c)
    {
        c.Command("spawn", (Action<string, int>)GameCheats.Spawn, "Spawn: spawn \"big orc\" 5", "Cheats");
        c.Command("killall", (Action)GameCheats.KillAll, "Kill all", "Cheats");
        c.Var("fps_max", () => GameCheats.FpsMax, v => GameCheats.FpsMax = v, "FPS limit");
        c.VarReadOnly("engine", () => Application.version, "Engine version");
    }
}
```

- Overloads share one name: `load Menu` and `load 2` coexist; `help` lists both.
- `Var` is get/set; assigning prints the new value. `VarReadOnly` is get-only.
- Vectors use comma syntax: `pos Player 0,1,0`.
- See `Samples~/ExamplePack/ExamplePack.cs` for a full sample.

## Pack Generator

Menu: **Window → Universal Console → Pack Generator**.

1. Pick a static class from the dropdown (scanned from `Assets/` scripts only).
2. Tick the methods to expose.
3. Generate: creates/updates `*Pack.cs` with `global::` type references, required `using`s and `[Preserve]` attributes. Existing `Build` content is preserved via `Overwrite / Append missing / Cancel`.

## Logo and font

- `Runtime/Resources/logo.txt` is printed once at startup. Edit the file to change the banner; no code changes needed.
- `Runtime/Resources/Silver.ttf` is the overlay font, loaded first with fallback to legacy/Arial/system fonts. Replace the file to reskin.

## Built-in command reference

Variables are get/set: typing the name prints the value, `name value` assigns it.
`VarReadOnly` entries are marked `(ro)`.

### Core (`help list echo clear version history copy paste`)

| Command | Args | Description |
|---|---|---|
| `help` | `[filter]` | Commands with help text, optionally filtered |
| `list` | — | Short name list |
| `echo` | `text` | Print text |
| `clear` | — | Clear screen |
| `version` | — | Console + Unity version |
| `history` | — | Last input lines |
| `copy` | `text` | Copy text to clipboard |
| `paste` | — | Print clipboard text |

### App (`quit pause resume restart time appinfo timescale targetfps vsync runbg`)

| Command / Var | Args | Description |
|---|---|---|
| `quit` | — | Quit app (exits play mode in editor) |
| `pause` | — | Freeze time |
| `resume` | — | Unfreeze time |
| `restart` | — | Reload active scene |
| `time` | — | Time scale and clocks |
| `appinfo` | — | Product, platform, language |
| `timescale` | `0..20` | Time scale |
| `targetfps` | `-1..240` | Target fps |
| `vsync` | `0..1` | Vsync count |
| `runbg` | `bool` | Run in background |

### Screen (`resolution quality shot display orient fullscreen sleeptimeout brightness`)

| Command / Var | Args | Description |
|---|---|---|
| `resolution` | `w h fullscreen` | Set resolution, e.g. `resolution 1920 1080 true` |
| `quality` | `[n]` | List levels or set by index |
| `shot` | `file` | Screenshot, e.g. `shot bug.png` |
| `display` | — | Resolution, dpi, orientation |
| `orient` | `portrait landscape left right auto` | Screen orientation |
| `fullscreen` | `bool` | Fullscreen window |
| `sleeptimeout` | `int` | Seconds (`-1` never, `-2` system) |
| `brightness` | `0..1` | Screen brightness |

### Render (`shadows fog`)

| Command / Var | Args | Description |
|---|---|---|
| `shadows` | `[off hard soft all]` | Get or set shadow mode |
| `fog` | `bool` | Scene fog on/off |

### Scene (`scene scenes load load_add unload active_scene reload objects find_type components destroy active pos scale persist`)

| Command | Args | Description |
|---|---|---|
| `scene` | — | Active scene info |
| `scenes` | — | Scenes in build settings |
| `load` | `name` or `index` | Load scene |
| `load_add` | `name` or `index` | Additive load |
| `unload` | `name` or `index` | Unload scene |
| `active_scene` | `name` or `index` | Set active scene |
| `reload` | — | Reload active scene |
| `objects` | `[filter]` | Root objects |
| `find_type` | `Type` | Find components, e.g. `find_type Light` |
| `components` | `name` | List components of object |
| `destroy` | `name` | Destroy objects |
| `active` | `name bool` | Set active flag |
| `pos` | `name [x,y,z]` | Get/set position |
| `scale` | `name [x,y,z]` | Get/set scale |
| `persist` | `name` | `DontDestroyOnLoad` |

### Prefs (`pref_get pref_get_int pref_get_float pref_set pref_set_int pref_set_float pref_del`)

| Command | Args | Description |
|---|---|---|
| `pref_get` | `key` | Read string |
| `pref_get_int` | `key` | Read int |
| `pref_get_float` | `key` | Read float |
| `pref_set` | `key value` | Write string |
| `pref_set_int` | `key value` | Write int |
| `pref_set_float` | `key value` | Write float |
| `pref_del` | `key` | Delete key |

### Log (`log warn err cap_info cap_warn cap_err mirror`)

| Command / Var | Args | Description |
|---|---|---|
| `log` | `text` | Info message |
| `warn` | `text` | Warning message |
| `err` | `text` | Error message |
| `cap_info` | `bool` | Capture info logs |
| `cap_warn` | `bool` | Capture warnings |
| `cap_err` | `bool` | Capture errors |
| `mirror` | `bool` | Mirror to Unity console |

### Physics (`gravity fixed_dt`)

| Var | Args | Description |
|---|---|---|
| `gravity` | `x,y,z` | World gravity |
| `fixed_dt` | `0.0001..1` | Fixed timestep |

### Audio (`mastervol muted`)

| Var | Args | Description |
|---|---|---|
| `mastervol` | `0..1` | Master volume |
| `muted` | `bool` | Mute all audio |

### Camera / Input / Device / Memory (`cameras cursor sysinfo net battery mem gc`)

| Command | Args | Description |
|---|---|---|
| `cameras` | — | List cameras |
| `cursor` | `[lock none confined]` | Get/set cursor lock |
| `sysinfo` | — | Device, OS, CPU, GPU |
| `net` | — | Network reachability |
| `battery` | — | Battery level and status |
| `mem` | — | Managed and native heap |
| `gc` | — | Collect garbage now |

### Util (`url sleep boom seed repeat vibrate`)

| Command | Args | Description |
|---|---|---|
| `url` | `address` | Open address |
| `sleep` | `0..5000` | Freeze ms |
| `boom` | — | Throw a test exception |
| `seed` | `int` | Random seed |
| `repeat` | `n command` | Run N times, e.g. `repeat 3 echo hi` |
| `vibrate` | — | Vibrate device |

## License

MIT. Copyright (c) 2026 sackrany. See `LICENSE`.
