({
   Id: "Zy.PPDef.Tailwind",
   Name: "Tailwind",
   Version : "1.2.1",
   Author : "Sheepy",
   Requires: [
      { Id: "tracktwo.PPDefModifier", Min: 1.7, Name: "PPDefModifier", Url: "https://github.com/tracktwo/ppdefmodifier/releases" },
      { Id: "Modnix", Min: "3.0.2021.0125", Name: "Modnix", Url: "https://github.com/Sheep-y/Modnix/releases" },
   ],
   Actions: [
      {
         Action: "Default", // Default actions will copy its field to all following actions.
         OnError: "Log,Skip", // Here we set the error policy to Log, and Skip (to next action).
         Phase: "GeoscapeMod", // and that the actions are taken when first entering the Geoscape.
      },
      // Increase the speed of all vehicles, based on Phoenix Point 1.11 stats.
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
         comment : "Tiamat Speedup, orignally 250",
         guid  : "a5a79edb-6d8a-dc54-d828-61ed825cd770",
         field : "BaseStats.Speed.Value",
         value : 375,
      },{
         comment : "Helios Speedup, originally 650",
         guid  : "abcc9fdc-b601-cfc4-1b8d-5241e4cbb613",
         field : "BaseStats.Speed.Value",
         value : 975,
      },{
         comment : "Manticore (Masked) Speedup, originally 500",
         guid  : "228f2cd8-8ca2-4224-ead6-c9c684f52172",
         field : "BaseStats.Speed.Value",
         value : 750,
      },{
         comment : "Thunderbird (Infested) Speedup, originally 380",
         guid  : "052e290d-80b1-d394-c835-7fc076c0aa2f",
         field : "BaseStats.Speed.Value",
         value : 570,
      },{
         comment : "Tiamat (Infested) Speedup, orignally 250",
         guid  : "e9b1cffc-f21f-e234-3880-af8c9e03b17a",
         field : "BaseStats.Speed.Value",
         value : 375,
      },{
         comment : "Helios (Infested) Speedup, originally 650",
         guid  : "d57641fb-ee5d-44b4-f8da-a560c7196fdb",
         field : "BaseStats.Speed.Value",
         value : 975,
      },{
         comment : "Charun (Small Pandoran) Speedup, originally 250",
         guid  : "c235f229-820f-cfa4-7b74-fa6329015aaa",
         field : "BaseStats.Speed.Value",
         value : 375,
      },{
         comment : "Berith (Medium Pandoran) Speedup, originally 350",
         guid  : "096ee7aa-4ca3-c3f4-baea-926d4e4a7c6a",
         field : "BaseStats.Speed.Value",
         value : 525,
      },{
         comment : "Abaddon (Large Pandoran) Speedup, originally 300",
         guid  : "096ee7aa-4ca3-c3f4-baea-926d4e4a7c6a",
         field : "BaseStats.Speed.Value",
         value : 450,
      },
   ],
   Description: "
This is a PPDefModifier mod that boost the speed of all air transports, including Pandoran flyers.
tested on Phoenix Point 1.11.1.

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