# Modnix

⮞⮞⮞ [Downloads](https://github.com/Sheep-y/Modnix/releases) ⮜⮜⮜

Modnix is a mod loader and mod manager for [Phoenix Point](https://phoenixpoint.info/), [Snapshot Games](http://www.snapshotgames.com/) 2019.

Supporting three mod formats,
it is designed to succeed (replace) [Phoenix Point](https://github.com/RealityMachina/PhoenixPointModInjector/#readme) [Mod Loader](https://github.com/Ijwu/PhoenixPointModLoader/)s.

Modnix is a lot more complicated than PPML,
in order to give everyone a better modding experience.
I try my best, but if you find bugs, please report on [GitHub](https://github.com/Sheep-y/Modnix/issues) or [Nexus](https://www.nexusmods.com/phoenixpoint/mods/43?tab=bugs).

## User Features

1. Easy to use.  One-click setup, simple mod manager, detailed diagnostic log.
2. Advanced mods, such as *real* Skip Intro, and can load *both* PPML 0.1 and 0.2 mods.
3. Create and reset mod configs for compatible mods.
4. Quick access to mod files, communities, resources, and of course the game.

See the [Wiki](https://github.com/Sheep-y/Modnix/wiki#wiki-wrapper) for documentation,
such as [User Guide](https://github.com/Sheep-y/Modnix/wiki/User-Guide#wiki-wrapper)
and [Troubleshooting](https://github.com/Sheep-y/Modnix/wiki/Troubleshooting-Modnix#wiki-wrapper).

## Modder Features

1. Mods can be built to support PPML, Modnix, and any future tools, without explicit Modnix dependency.
2. Display mod information to users, such as author and links, with embedded or stand-alone info file.
3. Mod are placed in their own folder, with optinal config file managed by Modnix.
4. Declarative mod dependencies and conflicts.  Opt-in pre-splash loading.
5. Non-binding api for mod config, unified background logger, query game versions, get mod list etc.

See the [Wiki](https://github.com/Sheep-y/Modnix/wiki#wiki-wrapper) for mod specs.

See [my mods](https://github.com/Sheep-y/PhoenixPt-Mods/) for examples of dual PPML/Modnix support.
(Not the best examples, given their complexity, but that's all we have for now.)

You can try to get support on the #mods channel on [official Discord](https://discordapp.com/invite/phoenixpoint).

## License

The Injector and Mod Loader are released to the public domain.
The Manager and most libraries are licensed under MIT,
except 7-Zip which is license under LGPL.
