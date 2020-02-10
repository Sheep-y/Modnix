# Modnix

Modnix is a mod loader for [Phoenix Point](https://phoenixpoint.info/), [Snapshot Games](http://www.snapshotgames.com/) 2019.

It is currently under development, and is not ready for even experimental use.

Modnix Point will be fully compatible with RealityMachina's [Phoenix Point Mod Injector](https://github.com/RealityMachina/PhoenixPointModInjector/),
aka. PPML, and is intended to be a straight upgrade.

Currently, only [Harmony](https://github.com/pardeike/Harmony)-based mods are supported.


## Features

Planned features, roughly in order:

0. Installable from game root, and moves PPML away to avoid accidents.
1. Load mods extracted into deep folders, e.g. Mods/MyMod-1-1-123456/MyMod/MyMod.dll
2. Mod info with dependency, game version range, incompatibility, supported languages, and the usual crowd.
3. Mod settings, in a different file from mod info, so that mod info can be updated without changing settings.
4. Optional early mod initiation to skip intro.  Default use current initiation.
5. Integration of ppdefmodifier.
6. List of mods, Console Logger, File Logger, Reflection, and Patching Helper.
7. Phoenix Point helpers, like lazily cached Dictionary of weapons, items, tags etc.
8. Auto-updater.
9. Example mod to demo and test the features.
10. GUI and Installer with settings, e.g. to disable mod without deletion.
11. Asset loader and overrider, like texture, music, sound etc.


## Mod Loading Logic

1. All dll files and folders in the Mods folder are scanned recursively.
2. If the folder contains `mod.json` and is not root, it is considered a Modnix mod, and folder processing stops.
3. Each dll in the folder will be scanned for ModInit static methods.  If either exists, it is considered a Modnix dll mod.
4. Otherwise, if the dll has any Type that contains the Init static method, it is considered a PPML mod.

### Modnix code mod

A modnix dll mod may be loaded at one of these times:

1. Startup - Very very early, for modifying Splash and Intro.
2. Main - (Default) After first PP load screen, before the hottest year cutscene, i.e. same as PPML.
3. Geoscape - When the game enters Geoscape for the first time.
4. Tactic - When the game enters Tactical Mission for the first time.

Prefer Geoscape and Tactic.
They are lazily loaded, which allows the game to launch faster.
(They may also be bumped up to Main by dependencies, but never to Splash.)

If the mod folder contains `settings.default.json`, it will be parsed first.
Then `settings.json` is parsed, replacing any default.
The result will then be passed to `ModInit()`.
`Init()` will *not* be called by Modnix when `ModInit()` exists.
If settings are not found but  and passed instead.

If `modinfo.json` exists and it specified a type, that type will be scanned for `public static void ModInit`.
If unspecified, or the type or method does not exists, every non-nested type in the dll will be scanned.
If multiple `ModInit` are found, the mod will not be loaded.

### PPML mod

For a PPML mods, every Type will be scanned for `public static void Init()`, and will be called in order of discovery.

They are always loaded on Main (see above).

### mod.json

This file provides additional mod information.
Here is a simple example:

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

All fields are optional; the file can contain only a pair of brackets.

Compatible game versions, load priority, mod dependencies, mod conflicts, etc.
can also be stated in mod.json.

## License

PPML is released to the public domain, so Modnix Point is the same.