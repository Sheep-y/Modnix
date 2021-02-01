# Modnix Demo Mods

These demo mods showcast Modnix's modding features.
Scroll up to browse their source code.

Mods packages can be downloaded from the [release page](https://github.com/Sheep-y/Modnix/releases/).
Extract the archive and you'll get individual 7z files that can be installed to Modnix.

## Essential Mods

Difficulty: Easy

A [Mod Pack](https://github.com/Sheep-y/Modnix/wiki/Mod-Types#Modnix_3_Mod_Packs) demo that bundles and pre-configure two other mods:
[Skip Intro](https://www.nexusmods.com/phoenixpoint/mods/17) and [Block Telemetry](https://www.nexusmods.com/phoenixpoint/mods/48).

Mod Packs are very easy to make.  Copy some files, change some text, and zip them up.
Most mods have an open license that allows them to be bundled this way.

## Tailwind

Difficulty: Moderate

A [PPDefModifier](https://github.com/tracktwo/ppdefmodifier) mod that can be managed by Modnix 3, when using an up-to-date PPDefModifier.
These mods are plain text and can be created with any text editor, the hardest part is finding the data to mod.

There are two versions, one that is backward compatible with old loaders and old PPDefModifier,
but is more complicated and can show inconsistent messages on Modnix 2.

The other version is very simple, single file, and is consistent, but is not backward compatible.

The user can install both; the simple one will disable the legacy one because it has a higher version.

## Laser On Fire

Difficulty: Moderate and up

A JavaScript mod that allows plain text mods to run code in the game.
As plain-text mods, they can be edited with Notepad to change their behaviour.

Because JavaScript is a [*complete*](https://en.wikipedia.org/wiki/Turing_completeness) scripting language,
it is way more powerful than PPDefModifier,
but have a steeper learning curve as you tread outside the easy helpers and into the deep water.

## Legend Prologue

Difficulty: Expert

A C# DLL mod that can be [disarmed](https://github.com/Sheep-y/Modnix/wiki/Mod-Phases#DisarmMod) and rearmed in game.

1. Install [Debug Console](https://www.nexusmods.com/phoenixpoint/mods/44/) and Legend Prologue.
2. Launch game, new game, select Legend difficulty and see that Prologue checkbox is visible. (Do not start game!)
3. Press '\`' to open console, enter `modnix mod_disarm Zy.LegendPrologue`.  Console should reports "true".
4. Select other difficulty, then Legend.  Prologue should now disappear, showing that the mod is disarmed.
5. Back to console, enter `modnix mod_rearm Zy.LegendPrologue`.  Console should reports "true".
6. Select other difficulty, then Legend.  Prologue should now reappear, showing that the mod is rearmed.