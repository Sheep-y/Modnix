# Modnix Demo Mods

These demo mods showcast Modnix's modding features.
Scroll up to browse their source code.

Prepackaged demo mods is available on the [release page](https://github.com/Sheep-y/Modnix/releases/).
Extract the tarball to get a bunch of 7z files that can be installed to Modnix.

## Essential Mods

Difficulty: Trivial

A [Mod Pack](https://github.com/Sheep-y/Modnix/wiki/Mod-Types#Modnix_3_Mod_Packs) demo that bundles and pre-configure two other mods:
[Skip Intro](https://www.nexusmods.com/phoenixpoint/mods/17) and [Block Telemetry](https://www.nexusmods.com/phoenixpoint/mods/48).

Mod Packs are very easy to make.  Copy some files, change some text, and zip them up.
Most mods have an open license that allows them to be bundled this way.

## Tailwind

Difficulty: Easy

A [PPDefModifier](https://github.com/tracktwo/ppdefmodifier) mod that can be managed by Modnix 3, when using an up-to-date PPDefModifier.
These mods are plain text and can be created with any text editor, the hardest part is finding the data to mod.

There are two versions, one that is backward compatible with old loaders and old PPDefModifier,
but is more complicated and can show inconsistent messages on Modnix 2.

The other version is very simple, single file, and is consistent, but is not backward compatible.

The user can install both; the simple one will disable the legacy one because it has a higher version.

## Hide Socials

Difficulty: Moderate

A simple [JavaScript](https://www.nexusmods.com/phoenixpoint/mods/49) mod that hides the social icons and version text on the game's home screen, making it cleaner.

Like PPDefModifier mods, JavaScript mods can be created and edited with Notepad, and thus easier than building DLL mods.

They are different from PPDefModifier in that they can do a much wider range of operations.
Whatever the game can do, you can do with JavaScript.

In this mod, a few simple methods are called to find two Unity objects, so that they can be disabled.
If you have [experience with Unity](https://learn.unity.com/), it should be trivial to apply the code to other screens.

If you don't, well, wait until I get a better demo mod idea.  It's hard.

## Laser On Fire

Difficulty: Hard

A [JavaScript](https://www.nexusmods.com/phoenixpoint/mods/49) mod that adds a small fire damage to all laser weapons.

JavaScript Runtime comes with a rich set of damage helpers, because they are pretty popular in the early modding days,
but also faced the restriction that PPDefModifier cannot create new damage on its own.

This mod firmly targets people with some [coding experience](https://eloquentjavascript.net/),
starting off with a list of different variables that is used at the end to showcast logging and api.

And, yes, it is easy to [get lost](https://www.thinkful.com/blog/why-learning-to-code-is-so-damn-hard/).
You do have the whole game, whole engine, whole framework, and *two* programming languages at your disposal.

When you are lost, come to the [modding channel](https://discord.com/channels/322630986431201283/656933530181435392) on Discord.

## Legend Prologue

Difficulty: Expert

A C# DLL mod that can be [disarmed](https://github.com/Sheep-y/Modnix/wiki/Mod-Phases#DisarmMod) and rearmed in game.
Easier said than done on non-trivial mods.

Steps to disarm / rearm this mod:

1. Install [Debug Console](https://www.nexusmods.com/phoenixpoint/mods/44/) and Legend Prologue.
2. Launch game, new game, select Legend difficulty and see that Prologue checkbox is visible. (Do not start game!)
3. Press '\`' to open console, enter `modnix mod_disarm Zy.LegendPrologue`.  Console should reports "true".
4. Select other difficulty, then Legend.  Prologue should now disappear, showing that the mod is disarmed.
5. Back to console, enter `modnix mod_rearm Zy.LegendPrologue`.  Console should reports "true".
6. Select other difficulty, then Legend.  Prologue should now reappear, showing that the mod is rearmed.