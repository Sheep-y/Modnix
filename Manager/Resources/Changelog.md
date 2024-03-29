Modnix Changelog

# Version 3, in development

* New: .bbcode support, such as readme.bbcode, history.bbcode, or license.bbcode.
* Fix: Full game version (with build) are now logged and displayed correctly.
* Gui: Move Nexus and Mods Folder button to mod list's top bar.
* Misc: Mono.Cecil update to 0.11.4, Newtonsoft Json update to 12.0.3.

# Version 2.5.7, 2021-02-19

* Fix: Fix game path detection error when Steam is not installed for current user.
* Fix: Make sure Add Mods button is enabled on non-standard installation path.

# Version 3 Beta 3, 2021-02-04

* New: Support GOG Galaxy online launch. Thanks mad2342 for helping with detection.
* New: Include action may now have a "property" field, which reads the file into current action's property.
* Fix: Manager no longer stuck at "Busy" when game version cannot be detected (regression). Thanks csuklyaspatkany on GitHub. (#35)
* Fix: Microsoft libraries are now also auto-loaded from system .Net, same as framework system libraries.
* Fix: ActionMod methods may now use IDictionary as param.  Dictionary still works.
* Fix: Action errors now no longer got reported an additional time at error level (regression).
* Fix: OnError: "Skip" now properly skip to next action, instead of skipping only current handler.
* Fix: .js mod that is bracket wrapped now installs correctly (regression).
* Gui: Support detection of game version 1.10 Orryx.
* Gui: Add Mod now removes version postfix from folder name, so that new version of a mod will be installed in same folder.
* Gui: Installer now relaunch itself as admin.  GUI setup remains unchanged because it requires major refactor.
* Log: DLL mod call exceptions are now logged under the mod's id, instead of standalone.
* Log: Shorten the paths in assembly list.

# Version 3 Beta 2, 2021-01-25
https://github.com/Sheep-y/Modnix/releases/tag/v3.0.2021.0125

* New: Injection-less mod loader.  Simpler, less risk, earlier load, easier to install and remove.
* New: Mod Pack support.  Multiple mods may now be pre-configured and bundled into one package.
* New: "Include" Action, allowing actions to be contained in standalone js/json files.
* New: "mod_stack" api to get api callstack.
* New: "mod_disarm" and "mod_rearm" api to support dynamic mod disarming and rearming.
* New: "Preloads" mod field for overriding game dlls without replacing them.
* Fix: Multiple mods with same Id no longer crash the Manager (regression).
* Fix: Actions without phase or with incorrect phases no longer crash the manager.
* Fix: Deleting root mods now also remove all associated files, such as actions and preloads.
* Fix: When an action handler returns true, the process now correctly moves on to next action.
* Fix: Pasting config text from Notepad no longer generate excessive blank lines.
* Fix: Always close handles on embedded files to use less system resources.
* Gui: Install/Setup/Revert will now revert PPML and legacy (injection-based) Modnix.
* Gui: .json mods may be added.  If a .js or .json mod is an array, it is put in the PPDefModifier folder.
* Gui: .md documents are now parsed and formatted, instead of plain text.
* Gui: Mismatching mod loader is now detected, and switch action button to Setup.
* Gui: Double click now enable / disable a mod.
* Gui: Mod load order updated to match Modnix 3 phases.
* Gui: Mod file list updates: now report missing files, and can open file explorer.
* Gui: Game path may now be changed in Installer and Manager.
* Gui: Update wording of game status.  Show build status in Installer.
* Mod: Action includes are now restricted to the same folder or a subfolder.
* Mod: Action.OnError now properly supports "continue", "skip", and "stop", and now always log unless "silent" is given.
* Mod: Actions now support multiple (comma separated) phsaes.
* Mod: API "path" and "dir" see two new params: "assembly-csharp" and "managed".
* Log: OS type and version are now logged.
* Log: Flushing log no longer log reason as "null".
* Log: Actions without handler (both at parse and at execution) are now logged.
* Log: Instantiation of mod classes by the Loader are now logged.
* Log: Initial assemblies are now logged, to help diagnose preloads.
* Lib: Updated Mono Cecil to 0.11.3.

# Version 2.5.6, 2021-01-25
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.6

* New: Identify and disable Modnix 3 mod packs.
* Fix: Contact and Url that are not proper uri, are now displayed correctly and no longer blank the mod info.
* Fix: Multiple Changlog / License resources no longer prevent all documents from being displayed.
* Fix: Unsupported Modnix 3 features are now displayed on a new line.
* Gui: Log filter now cascades from first line to following lines, e.g. stacktrace.
* Gui: Removed leading asterisk from one-line string Contact and Url.
* Gui: Setup and Revert now remove Modnix 3 bootloader (doorstep).
* Gui: Change Installed column to Last Update, because install time is not recorded and is unknown.

# Version 2.5.5, 2021-01-12
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.5

* Gui: Create runtime settings (loadfromRemoteSources) on install and on launch, to further reduce sandbox creation error. (#33)
* Gui: Unblock mod dlls downloaded from Internet so that they can be loaded for config. (#33)
* Gui: Replaced fandom icon with generic wiki icon, since the button no longer points to fandom.

# Version 2.5.4, 2020-12-30
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.4

* New: Support Steam installation, detected with priority over EGS.
* Gui: Wiki link now opens official wiki instead of fandom wiki.

# Version 2.5.3, 2020-07-09
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.3

* Misc: Update injector's game version extraction for game 1.5.2.
* Misc: Update loader's game version acquisition for game 1.5.2.

# Version 2.5.2, 2020-07-01
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.2

* Fix: Empty zip/7z no longer creates empty mod folder.
* Fix: Adding an empty 7z no longer throws AggregateException. (#30)
* Fix: Sandbox failure now triggers app config creation.  Thanks JulianSkies on NexusMods. (#29)
* Fix: Sandbox assembly path will be local if possible.
* Gui: Mods with flags no longer trigger warning; flags would be safe even when not supported.
* Gui: Info popup when Add Mod failed to add any files.
* Gui: Softer mod error wordings.  Statistically I made most mods, and they are resilience.

# Version 3 Beta 1, 2020-06-02
https://github.com/Sheep-y/Modnix/releases/tag/v3.0.2020.0602

* New: Show PPML version in app status.
* New: Mod Actions - a new system that allows mods to declare Actions and/or to support them.
* New: Mod Phases HomeMod, GameMod, TacticalMod, GeoscapeMod (one-off), and relevant OnShow/OnHide (repeated).
* Gui: Title now shows only major and minor version.  PPML compat version is included on status panel, after full version.
* Mod: Native API now returns Exception on error.
* Mod: Logger and config param no longer supported for mod initialisers and will be passed null.  Use API instead.
* Mod: DefaultConfig is no longer supported in mod_info.  Use ConfigType instead.
* Mod: Faster API extension calls when return value is void, class, or bool. (Non-bool value return types are still dynamically invoked for auto boxing.)
* Mod: "api_add" api now rejects compiler-geneated non-static methods, such as lambdas, due to the switch to direct delegate call.
* Mod: Mods without a recognised phase will be disabled, as a new last step of the resolve loop.
* Mod: Action-only mods also be disabled when there are no action handlers.
* Log: Mod loading messages are now logged with the mod's logger.
* Log: Timestamp shortened to 1ms from 0.1ms, to align with SI and DateTime precision.
* Misc: Non-SplashMod phases now depends on correct Harmony to function.  Specifically, MainMod no longer runs without Harmony.

# Version 2.5.1, 2020-06-02
https://github.com/Sheep-y/Modnix/releases/tag/v2.5.1

* Fix: "mod_info" api now return null when mod is not found.
* Fix: Mod content detection corrected.  Mods with content no longer auto-scan dlls and mods without content now auto-scan.
* Fix: Fixed a minor ArgumentNullException on GUI startup, before app version is loaded.
* Gui: Mod list can now identify Modnix 3 action mods.
* Gui: Skip resolution of Visual Studio dev library. This removes a few perplexing errors from the log.

# Version 2.5, 2020-05-20
https://github.com/Sheep-y/Modnix/releases/tag/v2.5

* New: "api_add" api now accepts parameter-less delegates such as Func<string> or Action.
* New: Mods may start an api call with vertical tab '\v' to suppress warnings.
* Fix: "api_add" api now rejects non-static and abstract methods, and methods with in, out, or ref parameters.
* Fix: Logger will now try to create directory when one does not exist, and write error will trigger OnError.
* Fix: Ignore .js and .dll with empty or space-only filename when scanning mods.
* Fix: Auto-scanning of dlls on an empty mod now skips a few known libraries.
* Fix: .NET fallback resolver now use correct path separator.  Should be a cosmetic difference.
* Mod: Avoids are now processed after Requires, and in the resolve loop.
* Mod: Resolve loop will restart between steps after a mod is disabled, instead of finishing the loop.
* Mod: Because of easier restart, resolve loop cap is raised from 20 to 30.
* Log: Revised mod disabled messages.
* Log: Disabled mods are now logged at info level, so that manually disabled mods will not have error when re-enabled.
* Log: Each dll load will only be logged once.
* Log: API exceptions are now logged as warning instead of error.  They may be out of control of the calling mod.
* Log: Exceptions returned by API extensions will now be logged as warning.  This align their handling with Modnix 3.  Native APIs still return null on error.

# Version 2.4, 2020-05-10
https://github.com/Sheep-y/Modnix/releases/tag/v2.4

* New: Avoids field of mod_info - self-kill if target exists.
* Fix: mod_info with Version field are now parsed correctly.
* Fix: A mod's requirement(s) on itself are now ignored.
* Fix: LoadIndex and LogLevel may be overrode together in modnix config, instead of index over level.
* Fix: Config write error now popup a prompt, instead of crashing mod manager. (#25)
* Mod: "assemblies" api now returns null when mod is not found.
* Mod: api( "assemblies", "modnix" ) no longer returns ppml assembly.
* Mod: Requires and Disables are now resolved together in each resolve loop, instead of only Requires.
* Mod: Multiple requires on the same mod are now processed as "or" instead of "and".
* Mod: Flags and Actions in mod_info (intended for Modnix 3) now trigger a notice in Manager and log.
* Mod: Actions field are now considered mod content and will prevent auto-dll scanning.
* Log: A mod that try to disable itself by mod_info now triggers a warning.
* Log: Loader log now tries to capture game crashs originating from the scope of CLR.
* Log: Loader log now warns on unresolved assemblies.
* Log: Loader now logs mscorlib version and end of phases at verbose level.

# Version 2.3.1, 2020-05-03
https://github.com/Sheep-y/Modnix/releases/tag/v2.3.1

* Fix: Game version now parsed correctly from loader log. (Regression from v2.3 due to text reader rewrite.)
* Fix: Mods' runtime error/warning notices are now refreshed when mod log is updated.

# Version 2.3, 2020-04-28
https://github.com/Sheep-y/Modnix/releases/tag/v2.3
Roadmap: https://github.com/Sheep-y/Modnix/issues/23

* New: A mod's LoadIndex and LogLevel now take effect after manually changed in Modnix.conf. Requested by Silent on Discord.
* New: Monitor loader log and console log even when Modnix is in background. Requested by Silent on Discord.
* Fix: Mod manager now use game version when parsing mods. This aligns it with mod loader, when game version is known.
* Fix: Mod config deletion (by saving a blank config in mod manager) now property resets config memory.
* Fix: "api_info" now return original method instead of internal wrapper method, when original has two parameters.
* Fix: "add_api" now accepts method of any return type, including void (null) and value types, not limited to objects.
* Fix: Mod list now preserve sort over refresh, instead of always reverting to name ascending. Thanks Silent on Discord.
* Fix: Modnix config no longer save IsDefaultSettings.
* Gui: Press Ctrl+S in config editor to save.  Press Ins/Del on mod list to Add/Delete mods, and Home/End to move to top/bottom.
* Gui: Mods with runtime warnings but no runtime errors are now highlighted in Blue instead of OrangeRed.
* Gui: Console log is now deleted before game launch. It cannot be properly deleted by loader / mods.
* Gui: Log panels and config editor now use monospace font (Consolas). Thanks Tahvohck on GitHub.
* Gui: Setup now deletes "JetBrains.Annotations.dll" from game root and Mods folder. Come with PPML 0.1 but is unused.
* Gui: Manually check update now prompt a message on no updates or error. Thanks Silent on NexusMods.
* Gui: Windows and splitter position now remembered. Restoration can be skipped by /s switch. Requested by Silent on Discord.
* Gui: New config to control game launch parameters. Requested by Tahvohck on Discord.
* Mod: "assembly" and "assemblies" now support "ppml", "phoenixpointmodloader", and "phoenix point mod loader" param.
* Mod: Getting "assemblies" of "modnix" or "loader" now includes ppml if and only if the embedded ppml is loaded.
* All text files reading (logs, configs, documents etc.) now ignore write locks. Thanks Silent on Discord.

# Version 2.2, 2020-04-17
https://github.com/Sheep-y/Modnix/releases/tag/v2.2

* New: New API "api_list" and "api_info".
* Fix: Config panel no longer throws RemotingException when a config is first accessed after five minutes from launch or last config. (#20)
* Fix: When config panel can't show config, it will display an error instead of stuck on old tab.
* Fix: Launch game will now wait for the config save prompt, if any. (#21)
* Fix: Setup will now refresh game version. (#22)
* Fix: Save, Enable, and Disable buttons are now disabled when app is busy or game is running.
* Mod: "api_add" and "api_remove" now rejects if there are content after API key.

# Version 2.1, 2020-04-12
https://github.com/Sheep-y/Modnix/releases/tag/v2.1

* Fix: Downgraded to PPML 0.2 because of mod incompatibility of PPML 0.3. (#16) Thanks Sangvis Ferri on Discord.
* Fix: Some mods were not properly detected after Add Mod. (#18) Thanks Sangvis Ferri on Discord.
* Fix: General resolution of mods now does not factor Version when determining mod order.
* Fix: Resolution of duplicate mods now prioritises Version and ignore LoadIndex. (#14)
* Gui: Remove "can create config file" notice.  Config is now handled by the config tab.
* Mod: "config" api action now supports "default" and "delete" spec.
* Mod: Logged exceptions are now ignored by mod. Duplicate exception from different mods will be shorted instead of ignored. (#17)

# Version 2.0, 2020-04-11
https://github.com/Sheep-y/Modnix/releases/tag/v2.0
Roadmap: https://github.com/Sheep-y/Modnix/issues/13

* New: Mods may now be manually disabled.
* New: Status panels and their buttons may now be minified to give more space to mod info.
* New: Mod config editor. Save without content to delete the config file.
* New: Read mod's readme, history / changelog, and license in mod info panel.
* New: Read game's console log and Modnix's changelog in log tab. Loader and console log may be filtered.
* New: Mod that logs an error or warning will be detected, highlighted, and show a notice in mod info.
* New: Add Mod now supports .gz and .bz2.  Refined mod name logic for adding and scanning mods.
* New: New API "api_add", "api_remove", "assemblies", "dir", "stacktrace".
* Fix: Hyperlinks are now clickable in mod info panel.
* Fix: Setup now keep only one PPML backup. This removes a potential logged error during setup.
* Fix: Embedded mod_info with BOM can now be correctly read.
* Fix: Mods with multiple non-static initialiser classes are now called with the correct classes.
* Fix: Mod api log action will return true when successfully added to log queue.
* Fix: Unknown mod api action will trigger a warning.
* Fix: Mods without dll will no longer crash gui mod list.
* Fix: Func<string> log params will now be resolved to string.
* Fix: Release build now skips pre-release updates and development builds now check them.
* App: Scan all game bar records for Phoenix Point path.
* App: Compress resources to make file size smaller.
* Gui: Mod list now shows load order, and sort by mod name by default.
* Gui: Mod list and loader log now auto-refresh on loader log update.
* Gui: Click on a mod's disabled reason to jump to the cause.
* Gui: Click on game path / mod path to open Windows Explorer.
* Gui: Show version in title, and show dev status.  Update button shows (dev) when on dev channel.
* Gui: Keep current mod selection when refreshing mod.
* Mod: Upgrade PPML+ to 0.3.  PPML+ is now properly initialised.
* Mod: New mod_info field "LoadIndex" and "ConfigType". ConfigType replaces DefaultConfig; simpler, faster, and less prone to error.
* Mod: Log action now supports log level and flush.
* Mod: Config action now supports save / write, and will use ConfigType by default if found.
* Mod: "path" api action now supports Modnix / Loader path.
* Mod: Multiple dll declarations no longer allowed in order to keep things simple.
* Mod: Fix reported version for "ppml" when queries through api.
* Mod: DefaultConfig in mod_info will be compared with a new instance of the config type, on the config action, and warn if different.
* Mod: Mod API are now case-insensitive, including registered extensions.
* Log: Log is flushed to disk after every game phase.

# Version 1.0, 2020-03-23
https://github.com/Sheep-y/Modnix/releases/tag/v1.0
Roadmap: https://github.com/Sheep-y/Modnix/issues/1

* New: Enable verbose logging in config file and GUI. Also extends to mods that log through Modnix.
* Fix: Deleting configured mods with their config no longer hangs the manager. (#5)
* Fix: Reset config now really reset to default config, instead of rewriting current config. (#12)
* Fix: Files embedded with the "Resource" build action are now detected.
* Fix: Obsolete class methods are no longer skipped when scanning mod initialiser.
* Fix: Partial versions are now completely zero-filled, instead of partially.
* Fix: Typo in revert injection message.
* Fix: Static mod classes are now scanned. (#11)
* App: Faster injection status detection by checking only known injection points. (#10)
* Gui: When selecting multiple mods, no longer bold disabled mods.
* Gui: Loader Log button now shows log in-panel, good for a quick check.  Diagnostic Log renamed to Manager Log.
* Gui: Rewrote post-setup message to be less intimidating and mention where to re-setup.
* Mod: Faster mod scanning by skipping nested classes, non-public classes, abstract classes, interfaces, enums, and system-generated classes. (#9)
* Mod: Faster mod scanning by skipping non-public methods and shortcut Prefix/Postfix.
* Mod: Rename Conflicts to Disables to better reflect its purpose.
* Mod: Replace all mod initialiser params with a single mod api delegate param.
* Mod: Disable mod_init parsing, may be redesigned.
* Mod: Log a warning when mod_info specifies a dll that has no initialiser.

# Version 0.90 RC, 2020-03-16
https://github.com/Sheep-y/Modnix/releases/tag/v0.90

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
* Fix: Manual setup package should no longer run in setup mode. Thanks Zyxpsilon on Discord.
* Fix: Mod loading order should now be consistent. (But subject to change in the future.)
* Fix: Lang of mod info now accepts a single string as intended.
* Fix: Embedded mod_info now have their Url, Contact, and Copyright applied.
* Fix: Url and Contact no longer lowercase their keys.
* Fix: Parameter defaults are now used for unrecognised initialiser parameters.
* Fix: Mono.Cecil.dll is now deleted from Mods folder on setup, in addition to other loader dlls.
* Fix: Typo on log label. Thanks javehaider on NexusMods.
* Fix: Typo on button. Thanks javehaider and Lunazathoth on NexusMods.
* Gui: Improved app / injection / game status display and less unnecessary status checks.  Modnix keyvisual faintly visible.
* Mod: In mod_info, Langs are changed to Lang, Urls changed to Url.  This make info fields consistent.
* Mod: Overloaded mod initialisers are now allowed and the first declared will be used.
* Injector's game version check now returns only type of error, instead of full stacktrace, to keep returned text on one line.

# Version 0.81 Beta Patch, 2020-03-01
https://github.com/Sheep-y/Modnix/releases/tag/v0.81

* New: Faster startup - Scan mod list in parallel with startup checks.
* New: Faster startup - Check game version in parallel with injection status.
* New: Faster startup - Check ppml and modnix injection status in parallel, and check only public classes.
* New: Detect game folder from Windows 10 Gamebar registry.
* New: Detect game folder from system program path.
* New: Detect game folder from the Program Files on all hard drives, and skip all non-hard drives.  This replace old hardcoded checks.
* New: Detect whether game is running, and disables / enables buttons accordingly.
* New: If mod is in a subfolder, prompt and delete whole subfolder. (#5)
* New: Remove empty folders left by deleting a mod.
* Fix: Mod list refresh button now works. (#4)
* Gui: Installer no longer scan for mods.
* Gui: All log messages are duplicated to standard out, instead of silence after GUI shows up.

# Version 0.80 Beta, 2020-02-29
https://github.com/Sheep-y/Modnix/releases/tag/v0.80

* First public beta.
* One-click install.
* Full GUI showing injection state, game state and communities, mod list, basic mod info, and log of GUI.
* Supported mods: PPML 0.1, PPML 0.2 (PPML+), Modnix.
* Modnix mod phase: SplashMod, MainMod.
* Detect Phoenix Point on default EPG path.
* Rename PPML exe and delete modding dlls from game root on setup, to prevent accidents.