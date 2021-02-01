({
   Id : "Sheepy.LaserOnFire",
   Name : "Laser on Fire",
   Author : "Sheepy",
   Version : "1.0",
   Requires: "Zy.JavaScript",
   Description : "
Adds a small fire damage to laser weapons, plus pierce or shred on selected lasers.

This is done as a simple demo to showcase Modnix 3's Scripting Library,
which allows plain-text scripting mods to run in the game and modify stuffs.

This mod requires Modnix 3+ and Scripting Library 2+.
Tested on Phoenix Point 1.9.3.",
   Actions : [{
      "Action" : "Default",    // Required to set "Script" on all actions.
      "Script" : "JavaScript", // When combined with "Eval", cause the Scripting Library 2 to run the actions.
      "OnError" : "Warn, Continue", // Default is "Log, Stop" which logs the error and stops execution.
      // "Phase" : "MainMod",  // Default phase is "GameMod" which is when player first start/load a game.
      // The MainMod phase works on Home screen, which can be faster to test than GameMod.
   },{
      "Eval" : 'let count = 0;', // Yes!  You can create your own variables.  Or define your own functions.
   },{
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserPistol_WeaponDef" ).Fire( 1 ); count++', // Multi statement is ok.
   },{
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserAssaultRifle_WeaponDef" ).Fire( 1 ); count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserSniperRifle_WeaponDef" ).Fire( 3 ).Pierce( 5 ); count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserPDW_WeaponDef" ).Fire( 1 ); count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserTechTurretGun_WeaponDef" ).Fire( 1 ).Shred( 2 ); count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserArrayPack_WeaponDef" ).Fire( 2 ); count++',
   },{
      "Eval" : 'Log.Info( "Done! {0} lasers now ON FIRE!", count )',
      //"Eval" : 'Api.Call( "log", `Done! ${count} lasers now ON FIRE!` )', // Alternative.
   }],
   Url : {
      "Nexus" : "https://nexusmods.com/phoenixpoint/mods/33/",
      "GitHub" : "https://github.com/Sheep-y/PhoenixPt-Mods/",
      "Modnix" : "https://www.nexusmods.com/phoenixpoint/mods/43",
      "Scripting Library" : "https://www.nexusmods.com/phoenixpoint/mods/49",
   },
   Contacts: { Discord: "https://discord.com/channels/322630986431201283/656933530181435392" },
   Copyright: "Public Domain",
})