# Modnix Point

A mod loader for [Phoenix Point](https://phoenixpoint.info/), [Snapshot Games](http://www.snapshotgames.com/) 2019.

Modnix Point is fully compatible with RealityMachina's [Phoenix Point Mod Injector](https://github.com/RealityMachina/PhoenixPointModInjector/),
aka. PPML, and is intended to be a straight upgrade.

Currently, only [Harmony](https://github.com/pardeike/Harmony)-based mods are supported.

This is a work in progress, valid only for further development.

## Features

Planned features, roughly in order:

0. Install to game root.
1. Load mods extracted into deep folders, e.g. Mods/MyMod-1-1-123456/MyMod/MyMod.dll
2. Mod info with dependency, game version range, incompatibility, supported languages, and the usual crowd.
3. Mod settings, in a different file from mod info, so that mod info can be updated without changing settings.
4. Optional early mod initiation to skip intro.  Default use current initiation.
5. Integration of ppdefmodifier.
6. List of mods, Console Logger, File Logger, and Reflection Helper.
7. Phoenix Point helpers, like lazily cached Dictionary of weapons, items, tags etc.
8. Auto-updater.
9. Example mod to demo and test the features.

Future:

1. GUI and Installer with settings, e.g. to disable mod without deletion.
2. Asset loader and overrider, like texture, music, sound etc.


## License

PPML is released to the public domain, so Modnix Point is the same.