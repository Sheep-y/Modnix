({
   Id: "Zy.PPDef.Tailwind",
   Name: "Tailwind",
   Version : "1.2",
   Author : "Sheepy",
   Actions: [
      {
         Action: "Default", // Default actions will copy its field to all following actions.
         OnError: "Log,Skip", // Here we set the error policy to Log, and Skip (to next action).
         Phase: "GeoscapeMod", // and that the actions are taken when first entering the Geoscape.
      },
      {
         Include: "PPDefModifier/Tailwind.json" // Load actions from the PPDef json
      },
   ],
   Description:
"
* * * Mod STILL WORKS as long as PPDefModifier is available. * * *
Any mod loader and any PPDefModifier version.

This is a PPDefModifier mod that boost the speed of all air transports, including Pandoran flyers.
tested on Phoenix Point 1.11.1.

This mod is backward compatible with legacy mod loaders and PPDefModifier.
To do that, this mod have two parts:
A '.js' file that contains the mod's info such as version and description,
and its actions which links to a traditional '.json' file in the 'PPDefModifier' folder.

PPDefModifier allows anyone to modify game numbers,
provided you can find the 'guid' of the game object and the path to the number.
The PPDefModifier page (below) have more detailed guides.

A simpler version of this mod is also available on GitHub, but is not backward compatible.

Good luck!
",
   Url: {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "PPDefModifier" : "https://github.com/tracktwo/ppdefmodifier#readme",
   },
   Contacts: { Discord: "https://discord.com/channels/322630986431201283/656933530181435392" },
   Copyright: "Public Domain",
})