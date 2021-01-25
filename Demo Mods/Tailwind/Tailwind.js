﻿{
   Id: "Zy.PPDef.Tailwind",
   Name: "Tailwind",
   Version : "1.0",
   Author : "Sheepy",
   Actions: [
      {
         Action: "Default", // Default actions will copy its field to all following actions.
         OnError: "Log,Skip", // Here we set the error policy to Log, and Skip (to next action).
      },
      {
         Include: "PPDefModifier/Tailwind.json" // Load actions from the PPDef json
      },
   ],
   Requries: [{ Id: "tracktwo.PPDefModifier", Min: 1.7 }],
   Contacts: { Discord: "https://discord.com/channels/322630986431201283/656933530181435392" },
   Url: {
      "Modnix Docs": "https://github.com/Sheep-y/Modnix/wiki/",
      "Dump Data"  : "https://www.nexusmods.com/phoenixpoint/mods/50",
   },
   Description:
"
This is a PPDefModifier mods that boost the speed of the four starting air transports.

This is intended as a technical demo to show that Modnix 3 allows PPDefMods to be managed,
while retaining full compatibility with legacy mod loaders and PPDefModifier.

To do that, this mod have two parts:
A '.js' file that contains the mod's meta info such as description,
which links to one or more good old PPDef '.json' file in the 'PPDefModifier' folder.

PPDef mods allows modders to modify game numbers,
provided they can find the guid of the game object and the path to the number.
The 'Dump Data' mod is designed to put these info in the reach of non-coders.

Good luck!
",
}