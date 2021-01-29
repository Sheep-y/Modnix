({
   Id : "Zy.Core", // Id is still required for management purpose
   Name : "Essential Mods",
   Version : "2021.01.25",
   Author : "Sheepy",
   Mods : [ // List of mods, can be .dll or .json
      "BlockTelemetry.dll",
      "Subfolder/SkipIntro.dll",
   ],
   Requires : [{ Id: "Modnix", Min: "3.0.2021.0120" }],
   Description :
"
This Mod Pack contains two mods: BlockTelemetry and SkipIntro.
SkipIntro is pre-configured to skip new game intro.

This is intended as a technical demo for Modnix 3,
but the mods are real and tested on Year One Edition.

Mod Packs are very easy to create, requires no coding at all.
Put some mods in a folder, copy this mod_info.js, open it in Notepad, update mod list.

Then simply refresh in Modnix to see the mods, and change their config.
When it is ready for release, just zip the folder.
",
   Contacts : { Discord : "https://discord.com/channels/322630986431201283/656933530181435392" },
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
   },
   Copyright: "Public Domain",
})