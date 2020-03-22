# Modnix Changelog

# Version 1.0, 2020-03-22

* New: Enable verbose logging in config file and GUI. Also extends to mods that log through Modnix.
* Fix: Deleting configured mods with their config no longer hangs the manager.
* Fix: Files embedded with the "Resource" build action are now detected.
* Fix: Obsolete methods are no longer skipped.
* Fix: Partial versions are now completely zero-filled, instead of partially.
* Gui: When selecting multiple mods, no longer bold disabled mods.
* Gui: Loader Log button now shows log in-panel, good for a quick check.  Diagnostic Log renamed to Manager Log.
* Gui: Faster injection status detection by checking only known injection points.
* Mod: Faster mod scanning by skipping nested classes, non-public classes, abstract classes, interfaces, enums, and system-generated classes.
* Mod: Faster mod scanning by skipping non-public methods and shortcut Prefix/Postfix.
* Mod: Rename Conflicts to Disables to better reflect its purpose.
* Mod: Replace all mod initialiser params with a single mod api delegate param.
* Mod: Log a warning when mod_info specifies a dll that has no initialiser.

# Version 0.90 RC, 2020-03-16

* New: Game in non-standard paths may now be browsed and selected for setup and launch.
* New: Add mod button, placing mods in subfolder. Supports .dll, .zip, .7z, .js while skipping skip .cs, .csproj and putting PPDef mods at root.
* New: Reset button, which reset and recreate a compatible mod's config file.
* New: Multiple mods may now be selected for group mod actions.
* New: Formatted app/game/mod info.  Mod info panel now shows all relevant mod info, including file list, entry points, reason of disable etc.
* New: Show mod installation date on mod list.
* New: Disabled mods are greyed.
* New: mod_info.js now have Duration and DefaultConfig.
* New: Dll mods may now receive a setting string or object, when a config is supplied.
* New: Dll mods may get modsRoot, modPath, and assemblyPath as initialiser params.
* New: Modnix config now stored in Modnix.conf which can be edited easily.
* New: License button to display licenses of all images, parts, and libraries.
* Fix: Game is now offline-launched in its folder, instead of Modnix folder.
* Fix: Game is now detected when Modnix is placed in one to two levels of subfolder, in addition to root.
* Fix: Manual setup package should no longer run in setup mode. Thanks Zyxpsilon on Discord for reporting.
* Fix: Mod loading order should now be consistent. (But subject to change in the future.)
* Fix: Lang of mod info now accepts a single string as intended.
* Fix: Embedded mod_info now have their Url, Contact, and Copyright applied.
* Fix: Url and Contact no longer lowercase their keys.
* Fix: Parameter defaults are now used for unrecognised initialiser parameters.
* Fix: Mono.Cecil.dll is now deleted from Mods folder on setup, in addition to other loader dlls.
* Fix: Typo on log label. Thanks javehaider on NexusMods for reporting.
* Fix: Typo on button. Thanks javehaider and Lunazathoth on NexusMods for reporting.
* Gui: Improved app / injection / game status display and less unnecessary status checks.  Modnix keyvisual faintly visible.
* Mod: In mod_info, Langs are changed to Lang, Urls changed to Url.  This make info fields consistent.
* Mod: Overloaded mod initialisers are now allowed and the first declared will be used.
* Injector's game version check now returns only type of error, instead of full stacktrace, to keep returned text on one line.

# Version 0.81 Beta Patch, 2020-03-01

* New: Faster startup - Scan mod list in parallel with startup checks.
* New: Faster startup - Check game version in parallel with injection status.
* New: Faster startup - Check ppml and modnix injection status in parallel, and check only public classes.
* New: Detect game folder from Windows 10 Gamebar registry.
* New: Detect game folder from system program path.
* New: Detect game folder from the Program Files on all hard drives, and skip all non-hard drives.  This replace old hardcoded checks.
* New: Detect whether game is running, and disables / enables buttons accordingly.
* New: If mod is in a subfolder, prompt and delete whole subfolder.
* New: Remove empty folders left by deleting a mod.
* Fix: Mod list refresh button now works.
* Gui: Installer no longer scan for mods.
* Gui: All log messages are duplicated to standard out, instead of silence after GUI shows up.

# Version 0.80 Beta, 2020-02-29

* First public beta.
* One-click install.
* Full GUI showing injection state, game state and communities, mod list, basic mod info, and log of GUI.
* Supported mods: PPML 0.1, PPML 0.2, Modnix.
* Modnix mod phase: SplashMod, MainMod.
* Detect Phoenix Point on default EPG path.
* Rename PPML exe and delete modding dlls from game root on setup, to prevent accidents.