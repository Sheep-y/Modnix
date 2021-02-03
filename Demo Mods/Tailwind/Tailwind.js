({
   Id: "Zy.PPDef.Tailwind",
   Name: "Tailwind",
   Version : "1.1.1",
   Author : "Sheepy",
   Requires: [
      { Id: "tracktwo.PPDefModifier", Min: 1.7, Name: "PPDefModifier 1.7+", Url: "https://github.com/tracktwo/ppdefmodifier/releases" },
      { Id: "Modnix", Min: "3.0.2021.0125", Name: "Modnix", Url: "https://github.com/Sheep-y/Modnix/releases" },
   ],
   Actions: [
      {
         Action: "Default", // Default actions will copy its field to all following actions.
         OnError: "Log,Skip", // Here we set the error policy to Log, and Skip (to next action).
         Phase: "GeoscapeMod", // and that the actions are taken when first entering the Geoscape.
      },
      // Increase the speed of all vehicles, based on Year One Edition stats.
      {
         comment : "Manticore Speedup, originally 500",
         guid  : "228f2cd8-8ca2-4224-ead6-c9c684f52172",
         field : "BaseStats.Speed.Value",
         value : 750,
      },{
         comment : "Thunderbird Speedup, originally 380",
         guid  : "4c1178cc-e4a9-3f14-9ab4-2b397780b694",
         field : "BaseStats.Speed.Value",
         value : 570,
      },{
         comment : "Tiamat Speedup, orignally 375",
         guid  : "a5a79edb-6d8a-dc54-d828-61ed825cd770",
         field : "BaseStats.Speed.Value",
         value : 375,
      },{
         comment : "Helios Speedup, originally 650",
         guid  : "abcc9fdc-b601-cfc4-1b8d-5241e4cbb613",
         field : "BaseStats.Speed.Value",
         value : 975,
      },
   ],
   Description: "
This is a PPDefModifier mod that boost the speed of the four starting air transports,
tested on Phoenix Point 1.9.3.

PPDefModifier allows anyone to modify game numbers,
provided you can find the 'guid' of the game object and the path to the number.
The PPDefModifier page (below) have more detailed guides.

This mod requires Modnix 3+ and PPDefModifier 1.7+
A backward compatible version of this mod is also available on GitHub.

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