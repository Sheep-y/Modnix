# Modnix Design Doc

This is the design documentation for Modnix ver 1.0.

Design doc is the vision and design of an app.
Features may be dropped due to time constrain or other reasons.

Modnix is a user friendly modding tool for Phoenix Point,
and a successor of Phoenix Point Mod Loader.

Aims for the release 1.0, in this order:

1. Fully backward compatible with PPML installation and mods.
2. User friendly, as foolproof as feasible.
3. Enable launch / splash screen modding.
4. Bring resources to players and players to community.



## Architecture

Modnix is coded in pure C# and has three main parts:

1. Injector, for injecting code to game assembly to run Mod Loader.
2. Mod Loader, for parsing and loading mods.
3. Main GUI, for setup, status check, and shortcuts.

Only Windows (Epic) is supported; not even sure whether the other platforms are moddable.
.Net Framework 4.5 is used, for best compatibility with Windows users.


## Injector

C# Console App

Functions:

* Detect, backup, inject, and restore primary injection point.
* Detect and restore PPML injection point.
* Report self version and detect game version.

Depends on Mono.Cecil for detecting and performing injection.

To make splash/launch screen modding possible,
the injection point is different from PPML, first fire is much earlier.
For compatibility, mods are loaded at a later point by default.

Most command line options, main flow, and console options are inherited from PPML,
but the rest is heavily refactored and modified.
The main flow can use some refactoring, if big changes are to be made.

All file operations are implemented in the TargetFile class,
of which one is created for Modnix dll and one for PPML dll.

Return code is always 0 (zero) for successful operations.
"Already installed/restored" also returns 0.

### Main Flow

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
* Load "splash" mods as early as possible.
* Load "mainmenu" mods before main menu, around same time as PPML.

Depends on Newtonsoft JSON.Net, to parse mod information,
and Lib.Harmony to patch main menu.
Harmony dll will be copied to MainGUI for embedding.

Harmony 1.2 is used to maintain backward compatibility with PPML.
All attempts to bring Harmony 1.2 mods to 2.0 has failed,
rewriting them with Mono.Cecil may be the only option left.

Most Modnix files - exe, settings, logs, and mods - are placed in My Documents\My Games\Phoenix Point\Mods
This ensures that they will survive reinstall, verify, patching, moving (in the same pc), and other file operations done on the game.
For 100% PPML compatibility, a symbolic link to this folder would be required at the games's root folder.

For compatibility, mods must be loaded on or around the same time as PPML's injection point, unless they opt-in to load on startup.

Most early loadings are done through async functions, which are difficult to patch with Harmony.
To keep things simple, Cinemabrain.OnEnable is chosen.
The first call comes before first logo, when splash mods are loaded and the main menu patched for default mods.
The second call is before the Hottest Year opening, again roughly same time as PPML, serving as a fallback for default mods.
Subsequence calls are ignored.

Also, Cinemachine may be updated less frequently than main assembly.
Nothing to lose; worse is same as PPML, reinstall after every patch.

### Mod Scanning

Aims:
1. Load manually extracted PPML mods, e.g. Mods/MyMod-1-0-1234/My Mod/MyMod.1.0.dll (note the space in path)
2. Does not load manually created folder e.g. Mods/Backup/MyMod.dll or Mods/Disabled/MyMod.dll
3. Mods collection must explicitly specify mods, e.g. do not load Mod/Collection/AnUnlistedMod.json

Steps:
1. The root mod folder is scanned for files and folders.
2. Files in root folder are parsed as mods.
3. If folders, and if mod_info.js(on) exists, it will be parsed as a mod. The folder will not be further processed.
4. If non-root folders, and only one .js or .json match the folder name, it will be parsed as a mod. The folder will not be further processed.
5. Otherwise, files whose name match the containing folder's, are parsed as mods.
6. If no file match the requirement, subfolders are scanned recursively up to a max depth.

After the "root" set of mods are scanned, they are "resolved" to bulid the working mod tree.
See Mod Resolution.

### Mod Parsing

1. If file extension is .dll, parse mod metadata from assembly information which serve as a default.
2. If file extension is .dll, find embedded "mod_info" and, if found, parse as .js and merge.
3. If file extension is not .dll, parse as mod.js.  See format below.
    1. If success, but mod does not specify any contents (Mods, Dlls, Alters, Assets), and is non-root, adds all dlls whose name match the folder (see above).
4. Check built-in override list.  If any match, parse and merge.
5. Check user override list.  If any match, parse and merge.

Mods that fail to parse at step 1 and 3 will not be loaded.
Parse failures at step 2, 4, and 5 are ignored and proceed to next step.

Currently only dll mods are supported.
Data mods is planned in future releases.

Future releases may also change the parsing to run in parallel,
when mods get bigger and more complicated.


### Mod Resolution

The resolution repeats until no action is taken, or until a max depth.

1. Consolidation
    1. Group mods by id.
    2. For mods that have the same id, find highest version, then find latest modified, then find largest, finally just find the first.
    3. The remaining duplicates are disabled and removed from resolution.
2. Manually disabled mods are disabled and removed from resolution.
3. Modnix, PPML, and Phoenix Point requirements are checked for each mod.  If out of range, disable and removed from resolution.
4. Remaining requirements are checked for each mod.  If missing any requirement, disable and removed from resolution.
5. Conflicts are checked for each mod.  Targetted mods are disabled and removed from resolution.
6. Mods are ordered by Priority. Conflicts cause the rule to be ignored.
7. Expansion
    1. Mods are parsed and added to resolution.

### Example mod_info.js

A mod's metadata is held in mod.js, or (mod name).js,
either as a real file or embedded in dll.

Comments allowed.  Property quotes optional.  All fields optional.
All paths are relative to the containing folder and can only go down.
File may be wrapped by a pair of parentheses for js compatibility, not shown here.

Simple example:

```
({
    Id: "info.mod.simple.demo",
    Name: "Simple Demo Mod",
    Description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit",
    Author: "Demonstrator",
    Url: "https://www.github.com/Sheep-y/Modnix",
})
```

### Mod Settings

Each mod and each dll may have a settings file, that has the same file name but with a .conf extension.
It will be read as UTF-8 string and passed as the first String parameter of each initialiser.

Mods may also expose a setting Type to Modnix.

Each class that has an initialiser will be scanned for a
GetDefaultSettings public method or DefaultSettings public property.
If exists, its value will be used as the default settings and setting type,
used to deseralise .conf file when the type is required by an initialiser.

If no initialiser use the type, the setting will not be called.

### Loader Settings

Loader and Modnix settings are stored in Modnix.conf, in the Mod folder.

It stores:

1. Global Log Level.
2. Status of each mods - manual disabled, log level, manual priority.
3. User override of mod metadata.

The settings are not associated with game path;
if there are multiple games, let the user manage the settings.

### Loading Phase

Mods may implement multiple methods which will be called at different phases.

Currently, two phases are supported.

1. SplashMod
2. MainMod (default, same as and after Init)

More phases are planned, but may change:

1. MainMenuFirstLoad
2. MainMenuLoad
3. GeoLevelFirstLoad
4. GeoLevelLoad
5. TacLevelFirstLoad
6. TacLevelLoad



## Main GUI

C# WPF App

Functions:

* Automated and user-friendly setup and update.
* An all-in-one user interface to see injection status, mod list, and launch game.
* Quick access to mod folders and game information and community including nexus, website, manual, SNS etc.
* Easy diagnostic of injection and mod loading issue through detailed log.

Depends on Mod Loader and Newtonsoft JSON.Net to parse mods.
JSON.Net is also used to check update.
Dependencies are embedded and loaded on demand.

Startup code is located at App.xaml.cs, before any windows are created.

There are two windows: a very simple Setup Window, and the full blown Main Window.
Which window is displayed depends on the startup logic.

Post-build scripts in Injector and Loader will copy their assemblies and libraries to Main GUI's resource folder, embedded as SetupPackage resource.

### Startup Logic

1. If Modnix is already running, bring it to front and exit.
2. If Modnix is installed to mod folder and version is equal or higher, show setup window with launch button.
3. Otherwise, if file name contains "setup", run setup logic:
    1. Show setup screen and try to detect game folder.  Prompt for game folder if not detected.
    2. If game is not found, screens stays the same.  Action button will prompt for folder again.
    3. When game is found, show folder and Setup button.  A change folder link will also be shown.
4. Otherwise, run main logic:
    1. Launch MainWindow, which initialise and ask AppControl to check status.
    2. AppControl asynchronously detects and report self version, game folder, injection status, and game version, in this order and only if previous step is positive.
    3. If injection is positive, dynamically load Newton and Mod Loader and use it to parse mod list.

### Setup

1. If Modnix is already in place and has equal or higher version, prompt to confirm.
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

### Add New Mods

Support .js, .json, .dll, .zip, and .7z

- If single file, or archive has one file and no folder, create a folder from filename and put it there.
- If archive has no file and one folder, extract to root.
- All other cases, create a folder from archive name and extract there.


## Vision

For future versions

- Disable mods without deleting them.
- Mod settings, in a different file from mod info, so that mod info can be updated without changing settings.
- Replace ppdefmodifier with something more powerful.
- Reflection and Patching Helper.
- Asset loader and overrider, like texture, music, sound etc.
