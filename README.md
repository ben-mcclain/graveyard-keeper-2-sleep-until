# Sleep Until

### A bedtime menu for Graveyard Keeper 2

Sleep normally, wake at the next dawn, or stay in bed until a particular weekday. No more repeatedly going to bed just to reach the day you need.

**[Download the latest release](https://github.com/ben-mcclain/graveyard-keeper-2-sleep-until/releases/latest)** · **[Report a bug](https://github.com/ben-mcclain/graveyard-keeper-2-sleep-until/issues)** · [MIT license](LICENSE)

## Features

- **Normal:** keep the game's usual sleep behavior.
- **Till morning:** wake at the next **6 a.m.**
- **Choose a weekday:** sleep until the morning of Pride, Lust, Gluttony, Envy, Wrath, or Sloth. Names and order are resolved from the game.
- **See the wait:** each scheduled option previews the approximate in-game hours until waking.
- **Sleep at full energy:** scheduled sleep continues even when your energy bar is full.
- **Change your mind:** cancel before sleeping, or use **F8 / Wake now** to wake early.
- **Let the world run:** time advances through the normal accelerated sleep simulation, not by rewriting the date.

The selection menu pauses the game while you choose. Mouse and the game's keyboard/controller navigation bindings are implemented.

> **Status:** v0.1.0 has been reported working in-game. This is an early release, not a claim of complete compatibility testing across controllers, game updates, other mods, or platforms.

## Requirements

- **Graveyard Keeper 2**, not the original Graveyard Keeper.
- **BepInEx 5**. The build uses **5.4.23.5** references.
- The instructions below cover **Windows x64**. Other operating systems need their own loader setup and are not validated here.

You do **not** need the .NET SDK, the GK2 Mod Framework, or any development tools to install the release DLL.

## Installation

### 1. Back up your save

Close the game and copy your save folder somewhere safe. On a standard Windows installation, press **Win + R** and paste:

```text
%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2
```

Copy the whole folder; don't move it. If the folder is missing, locate your actual saves before proceeding.

### 2. Install BepInEx, if needed

Skip this step if BepInEx 5 is already working.

1. Download the official [BepInEx 5.4.23.5 Windows x64 ZIP](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip).
2. In Steam, right-click the game → **Manage → Browse local files**.
3. Extract BepInEx into that folder, alongside `GraveyardKeeper2.exe`.
4. Launch the game once, then close it.

Use the BepInEx 5 Windows x64 package, not BepInEx 6 or an IL2CPP package.

### 3. Install Sleep Until

1. Download **`SleepUntil-0.1.0.zip`** from the [Releases page](https://github.com/ben-mcclain/graveyard-keeper-2-sleep-until/releases). The automatically generated **Source code** ZIP is for developers, not installation.
2. Extract the release ZIP into the game folder, merging the `BepInEx` folder.
3. Check that the DLL is here:

```text
Graveyard Keeper 2/
├── GraveyardKeeper2.exe
└── BepInEx/
    └── plugins/
        └── SleepUntil/
            └── SleepUntil.dll
```

4. Start the game and interact with a straw or regular bed.

For your first test, disable other sleep/time-changing mods, especially **Better Sleep**. Compatibility with those mods and with multiplayer/co-op is untested.

## Steam Workshop

A [Workshop publishing kit and step-by-step guide](workshop/PUBLISH.md) are available. **No Workshop listing has been published yet.** The kit uses the same v0.1.0 DLL. Workshop subscriptions still require BepInEx plus manual installation or an optional community Workshop Loader.

## How sleeping works

| Choice | Result |
| --- | --- |
| **Normal** | Vanilla sleep, including the normal full-energy restriction. |
| **Till morning** | The next future 6 a.m., even if it is later on the current calendar day. |
| **Until a weekday** | The next 6 a.m. on that weekday. |
| **Cancel** | No sleep or save; resumes the game's declined-sleep flow. A bed may show its usual refusal dialogue. |

**If you select today's weekday:** before 6 a.m., you wake that morning. At or after 6 a.m., you sleep until that weekday next week. Check the wait shown beside the choice.

GK2's calendar rolls over at midnight, but this mod uses **6 a.m.**, the game's day/night boundary, for morning. Waking happens on the first simulation tick crossing dawn, so the displayed clock can be a few in-game minutes past 06:00.

The native **50× sleep speed** is retained. Long sleeps take real seconds. Crops, NPC schedules, corpse decay, and other world processes continue through the game's simulation, so sleeping can pass over events. The normal wake/save path is used. No custom save-file schema is added.

Non-bed fades and scripted sleeps marked not to save are left alone.

## Controls and configuration

- **Choose:** mouse, or the game's Up/Down and Select bindings.
- **Cancel menu:** Escape / the game's Back binding, or click Cancel.
- **Wake early:** **F8** or the on-screen **Wake now** button during scheduled sleep.

After the first launch, change the early-wake key in:

```text
BepInEx/config/mcclains.gk2.sleepuntil.cfg
```

Controller bindings are implemented; exhaustive controller-specific testing is still pending.

## Troubleshooting

**The menu doesn't appear**

- Confirm the DLL is at the path shown above, not inside an extra nested ZIP folder.
- Check `BepInEx/LogOutput.log` for `Sleep Until`.
- Try without other sleep/time mods.
- Game updates can change the methods the mod patches.

**Something behaves unexpectedly**

Close the game, move `BepInEx/plugins/SleepUntil` out of the plugins folder, and restart. Uninstalling does not rewind elapsed game time; restore your backup if needed.

[Open an issue](https://github.com/ben-mcclain/graveyard-keeper-2-sleep-until/issues) with your game version, BepInEx version, other installed mods, steps to reproduce, and the relevant log excerpt. Remove personal paths or account details from logs. Please do not upload game DLLs, assets, or save files to public issues.

## Build from source

Requires **.NET SDK 8 or later**, your own game installation, and BepInEx 5 core libraries. This repository intentionally contains **no game assemblies, assets, or decompiled game source**.

Clone the repo, open PowerShell in its root, and set the paths to your installation:

```powershell
$gameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Graveyard Keeper 2'
$managedDir = Join-Path $gameDir 'GraveyardKeeper2_Data\Managed'
$bepInExCore = Join-Path $gameDir 'BepInEx\core'

dotnet build src/SleepUntil.csproj -c Release "-p:ManagedDir=$managedDir" "-p:BepInExCore=$bepInExCore"
```

The output is `src/bin/Release/netstandard2.1/SleepUntil.dll`. Copy **only that DLL** into your plugin folder. Building does not automatically modify your game installation.

### Tests

Run the calendar tests and checks against your game's sleep/save interfaces:

```powershell
dotnet run --project tests/SleepUntil.Tests.csproj -c Release "-p:BepInExCore=$bepInExCore" -- "$managedDir\Assembly-CSharp.dll" 'src/bin/Release/netstandard2.1/SleepUntil.dll'
```

Omit the two arguments after `--` to run only the calendar test groups. Tests use Mono.Cecil from the BepInEx core folder.

For v0.1.0, **14 test groups passed**, including **4,032 complete minute-step calendar simulations**. Cases cover midnight, dawn, six-day wraparound, same-day selection, exact-dawn entry, frame overshoot, scripted date skips, and single completion. Static checks verify the supplied game's relevant method signatures and wake/save call structure. They are not a substitute for an in-game test.

The development build reports one transitive `System.IO.Compression` version-conflict warning from the game references. The compiled plugin does not reference that API, and no replacement system assemblies are shipped.

### Project layout

```text
src/Plugin.cs          Bed interception, scheduled sleep, native wake/save hooks
src/SleepMenu.cs       Modal selection menu and input
src/SleepSchedule.cs   Engine-independent dawn/weekday scheduling
tests/Program.cs      Calendar simulations and game-interface checks
```

## Credits and license

Created by **Ben McClain with Shart**, an AI coding assistant.

Thanks to Lazy Bear Games for Graveyard Keeper 2, the [BepInEx team](https://github.com/BepInEx/BepInEx), [Harmony](https://github.com/pardeike/Harmony), and the [GK2 modding examples](https://github.com/BloodyLink2/GraveyardKeeper2-Mods) that helped identify the integration points.

Sleep Until's original source is released under the [MIT License](LICENSE). Game code and assets are not included and remain the property of their owners. This is an unofficial community mod, not affiliated with or endorsed by the game's developers.
