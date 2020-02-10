# Modnix Design Doc

This is the design documentation for Modnix.

Modnix is a user friendly successor to Phoenix Point Mod Loader,
the first modding tool for Phoenix Point.

Aims for the release 1.0, in this order:

1. Fully backward compatible with PPML installation and mods.
2. User friendly, as foolproof as feasible.
3. Enable launch / splash screen modding.



## Version History

2020-02-10 Initial documentation



## Architecture

Modnix is coded in pure C# and make up of three main parts:

1. Injector, for injecting code to game assembly to run Mod Loader.
2. Mod Loader, for parsing and loading mods.
3. Main GUI, for setup, status check, and shortcuts.



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

For requirements / specification on the mods, see mod writing documentation.



## Main GUI

C# WPF App

Functions:

* Automated and user-friendly setup
* An all-in-one user interface to see injection status, mod list, and launch game.
* Quick access to mod folders and game information and community including nexus, website, manual, SNS etc.

Startup code is located at App.xaml.cs, before any windows are created.

