# Modnix Demo Mods

These demo mods showcast Modnix's modding features.

## Essential Mods

A [Mod Pack](https://github.com/Sheep-y/Modnix/wiki/Mod-Types#Modnix_3_Mod_Packs) demo that bundles and pre-configure two other mods.

Most mods have an open license that allows them to be bundled this way.

## Legend Prologue

A DLL mod that can be [disarmed](https://github.com/Sheep-y/Modnix/wiki/Mod-Phases#DisarmMod) and rearmed in game.

1. Install [https://www.nexusmods.com/phoenixpoint/mods/44/ Debug Console] and Legend Prologue.
2. Launch game, new game, select Legend difficulty and see that Prologue checkbox is visible. (Do not start game!)
3. Press '\`' to open console, enter `modnix mod_disarm Zy.LegendPrologue`.  Console should reports "true".
4. Select other difficulty, then Legend.  Prologue should now disappear, showing that the mod is disarmed.
5. Back to console, enter `modnix mod_rearm Zy.LegendPrologue`.  Console should reports "true".
6. Select other difficulty, then Legend.  Prologue should now reappear, showing that the mod is rearmed.

## Tailwind

A PPDef mod that can be managed by Modnix 3, with PPDefModifier 1.7 and up.

There are two versions, one that is backward compatible with old loaders and PPDefModifier 1.6,
but is more complicated and can show inconsistent messages on Modnix 2.

The other version is very simple, single file, and is consistent, but is not backward compatible.

The user can install both; the simple version will disable the other version when its requirements is met.