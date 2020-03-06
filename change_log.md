# Modnix Changelog

# Version ??

* New: Formatted app/game/mod info.  Mod info panel now shows all display info, plus file list and entry points.
* New: Modnix settings now stored in Modnix.conf which is can be edited easily.
* Fix: Lang now accepts a single string as intended.
* Fix: Url and Contact no longer lowercase their keys.
* Fix: Embedded mod_info now overwrites Url, Contact, and Copyright.
* Fix: Typo on log label.
* Fix: Game is now detected when in upper folder.
* Fix: Manual setup package should no longer run in setup mode.
* In mod_info, Langs are change to Lang, Urls changed to Url.  This make info fields consistent.
* Injector's game version check now returns only type of error, instead of full stacktrace, to keep returned text on one line.

# Version 0.81, 2020-03-01

* Fix: Mod list refresh button now works.
* Faster startup: Scan mod list in parallel with startup checks.
* Faster startup: Check game version in parallel with injection status.
* Faster startup: Check ppml and modnix injection status in parallel, and check only public classes.
* New: Detect game folder from Windows 10 Gamebar registry.
* New: Detect game folder from system program path.
* New: Detect game folder from the Program Files on all hard drives, and skip all non-hard drives.  This replace old hardcoded checks.
* New: Detect whether game is running, and disables / enables buttons accordingly.
* New: If mod is in a subfolder, prompt and delete whole subfolder.
* New: Remove empty folders left by deleting a mod.
* Installer no longer scan for mods.
* All log messages are duplicated to standard out, instead of silence after GUI shows up.

# Version 0.80, 2020-02-29

* First public beta.
* One-click install.
* Full GUI showing injection state, game state and communities, mod list, basic mod info, and log of GUI.
* Supported mods: PPML 0.1, PPML 0.2, Modnix.
* Modnix mod phase: SplashMod, MainMod.
* Detect Phoenix Point on default EPG path.
* Rename PPML exe and delete modding dlls from game root on setup, to prevent accidents.