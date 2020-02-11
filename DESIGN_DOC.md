# Modnix Design Doc

This is the design documentation for Modnix ver 1.0.

Modnix is a user friendly successor to Phoenix Point Mod Loader,
the first modding tool for Phoenix Point.

Aims for the release 1.0, in this order:

1. Fully backward compatible with PPML installation and mods.
2. User friendly, as foolproof as feasible.
3. Enable launch / splash screen modding.



## Architecture

Modnix is coded in pure C# and has three main parts:

1. Injector, for injecting code to game assembly to run Mod Loader.
2. Mod Loader, for parsing and loading mods.
3. Main GUI, for setup, status check, and shortcuts.

Only Windows is supported; I don't even know whether the Mac/Linux version is built in a "moddable" way.
.Net Framework 4.5 is used, for best compatibility with Windows users.


## Injector

C# Console App

Functions:

* Detect, backup, inject, and restore primary injection point.
* Detect and restore PPML injection point.
* Report self version and detect game version.

Depends on Mono.Cecil for detecting and performing injection.

To make splash/launch screen modding possible,
the injection point is different from PPML, fired much earlier.

Most command line options, main flow, and console options are inherited from PPML,
but the rest of the code is heavily refactored and modified.
The main flow can use some refactoring, if big changes are to be made.

All actual file operations are implemented in the TargetFile class,
of which one is created for Modnix and one for PPML, to reuse code for PPML compatibility.

Return code is always zero for successful operations.
For install and restore, not needing to act is also considered success.

### Initialisation

1. `ParseOptions` parses options and handles simple commands that does not involve game assemblies.
2. `LoadGameAssembly` tests assembly paths, checks game version, and detects injection status.
3. Install or Restore is executed.

### Detect injection status

Assuming the assemblies can be read,
may report one of four status on standard out:

* none
* ppml
* modnix
* both

Other output should be human-readable error messages.

### Install

If PPML injection is detected, it will be restored.
If PPML backup is not found or also injected, throw error.

Then, if Modnix injection is not detected,
a backup will be made, followed by the injection.
If backup cannot be made, throw error.

Injection point is at the end of Cinemachine.CinemachineBrain.OnEnable, in Cinemachine.dll.
The end is choosen as to not affect branching instructions such as `if`.

Because OnEnable is not an async function, the injection logic is pretty simple, relative to PPML.

If injection is successful, or already injected, PPML's injector will be checked.
PPML does not know Modnix. If PPML is installed after Modnix, mods can be loaded twice.
Thus, a warning will be displayed if its injector is detected.

### Restore

Both PPML and Modnix injection will be restored, in this order.
If either inject exists and cannot be restored, throw error.



## Mod Loader

C# .Net DLL

Functions:

* Scan mods and parse their information.
* Load "startup" mods as early as possible.
* Load "main" mods before main menu, around same time as PPML.

Depends on Lib.Harmony, so that the latest harmony dll will be copied to output and can then be copied to MainGUI for embedding.
The loader itself does not need Harmony, for now.

All Modnix files - exe, settings, logs, and mods - are placed in My Documents\My Games\Phoenix Point\Mods
This ensures that they will survive reinstall, verify, patching, moving (in the same pc), and other file operations done on the game.
For 100% PPML compatibility, a symbolic link to this folder would be required at the games's root folder.

For compatibility, mods must be loaded on or around the same time as PPML's injection point, unless they opt-in to load on startup.

Most early loadings are done through async functions, which are difficult to patch with Harmony.
To keep things simple, Cinemabrain.OnEnable is choosen.
The first call comes before first logo, and the second call is before the Hottest Year opening, roughly the same stage with PPML.
Subsequence calls are ignored.

Also, Cinemachine may be updated less frequently than main assembly.
Nothing to lose; worse is same as PPML, reinstall after every patch.

### Mod Parsing

1. The root mod folder is scanned for files and folders.
2. Files in root, dlls are parsed as code mods, json are parsed as data mods.
3. Folders in root are recursively scanned.
4. Non-root folders, if mod.json exists, it will be parsed and, if success, the folder will not be further processed.
5. Non-root files and folders, if first three alphabetic characters of name are the same as folder's first three (heuristic), process it, otherwise ignore.
6. dlls are parsed as code mods.
7. json are parsed as data mods, if the name does not contain "setting" (case insensitive).
8. If no mods are found, subfolders will be recursively scanned as long as the first three characters match the first.
9. All parsing errors are ignored, as if the file does not exist.

Currently only code mods are supported.
Data mods will be partially implemented in future releases.

Future releases may also change the parsing to run in parallel.
It should be useful when mods get more complicated.



## Main GUI

C# WPF App

Functions:

* Automated and user-friendly setup
* An all-in-one user interface to see injection status, mod list, and launch game.
* Quick access to mod folders and game information and community including nexus, website, manual, SNS etc.
* Easy diagnostic of injection and mod loading issue through detailed log.

Startup code is located at App.xaml.cs, before any windows are created.

There are two windows: a very simple, message box like Setup Window, and the full blown Main Window.
Which window is displayed depends on the startup logic.

Post-build scripts in Injector and Loader will copy their assembly and library to Main GUI's resource folder, embedded as SetupPackage resource.

To minimise dependency and dynamic loading, GUI save its own settings in an ini file, at the root of mod folder, instead of json.
Mod settings are more complicated, so it is either xml or json.
Most homo sapiens modders seems to find the later easier to read and write.
We ovis aries should go and rule the world.

### Startup Logic

If modnix is already running, switch it to front and exit.
If self name contains "setup" (case insensitive), run setup logic.
Otherwise, run main logic.

Setup Logic:

1. Check whether Modnix.exe exists at Mods folder, and version is equal or more up to date.
2. If yes, show setup screen, but says Modnix is already installed, with a Launch button, and stop.
3. Show setup screen and try to detect game folder.  Prompt for game folder if not detected.
4. If game is not found, screens stays the same.  Action button will prompt for folder again.
5. When game is found, show folder and Setup button.  A change folder link will also be shown.

Main Logic:

1. If Modnix is installed to mod folder and version is equal or higher, do the same as setup, but with option to skip launch.
2. Launch MainWindow, which initialise and ask AppControl to check status.
3. AppControl asynchronously detects and report self version, game folder, injection status, and game version, in this order and only if previous step is positive.
4. If injection is positive, dynamically load Newton and Mod Loader and use it to parse mod list.

### Setup

1. If Injector or Modnix is already in place and has equal or higher version, prompt to confirm.
2. Injector, Loader, and their libraries are loaded from SetupPackage, and saved to game folder.
3. Injector is called to run injection. It should also undo PPML if exists.
4. If game is not injected, abort setup and report error.
5. A copy of self is placed in mod folder (create if not exists), with the term "setup" removed from the clone's filename.
6. Move all files and folders from game's Mods folder to there, if target of same name does not already exists.
7. Delete the game's Mods folder if it is empty.
8. Create a symbolic link to the correct mod folder, for mods with hardcoded paths and gullible users who follow outdated instructions.
9. If PPML exists, try to rename it.
10. Prompt for Desktop shortcut, if not exists.
11. Report to user and, if step 1-5 success, prompt for launch or restart.



## Vision

For future versions

- Auto-updater.
- Disable mods without deleting them.
- Mod info with dependency, game version range, incompatibility, supported languages, and the usual crowd.
- Mod settings, in a different file from mod info, so that mod info can be updated without changing settings.
- Replace ppdefmodifier with something more powerful.
- Supply, on run time, a list of mods, plus Logger, Reflection, and Patching Helper.
- Asset loader and overrider, like texture, music, sound etc.

### Example mod.json

Just an early draft. 100% certain to change.

```
{
    "Name": "My Awesome Mod",
    "Description": "An awesome mod to do awesome things",
    "Author": "Sheepy",
    "Website": "https://www.github.com/Sheep-y/Modnix",
    "Contact": "fakeemail@fakeemail.com",
    "DLL": "MyAwesomeMod.dll",
    "InitAt": "Main",
    "Manifest": [
        { "Type": "WeaponDef", "Path": "MyWeaponDefs" },
        { "Type": "ResearchRewardDef", "Path": "MyRewardDefs/research_reward_def.json" }
    ]
}
```