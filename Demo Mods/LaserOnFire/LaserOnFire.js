{
   Id : "Sheepy.LaserOnFire",
   Name : "Laser on Fire",
   Author : "Sheepy",
   Version : "1.0",
   Requires: "Zy.JavaScript",
   Description : "
Adds a small fire damage to laser weapons, plus pierce or shred on selected lasers.

This is done as a simple demo to showcase Modnix 3 Scripting,
which allows plain-text JavaScript mod to run in the game and modify stuffs.

Most lines use the 'Repo' helper to find a 'WeaponDef' of a given name,
then use the damage extensions to set its fire/pierce/shred damage.

The mod also declares a counter and log it, for demonstration.

(Built in helpers are listed in JavaScript Runtime's readme, while common in-game object types are listed by the Dump Data mod.)

This mod requires Modnix 3+ and JavaScript Runtime.
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
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserPistol_WeaponDef" ).Fire( 1 );
                count++', // Multi statement is ok.
   },{
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserAssaultRifle_WeaponDef" ).Fire( 1 );
                count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "SY_LaserSniperRifle_WeaponDef" ).Fire( 3 ).Pierce( 5 );
                count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserPDW_WeaponDef" ).Fire( 1 );
                count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserTechTurretGun_WeaponDef" ).Fire( 1 ).Shred( 2 );
                count++',
   },{
      "Eval" : 'Repo.Get( WeaponDef, "PX_LaserArrayPack_WeaponDef" ).Fire( 2 );
                count++',
   },{
      "Eval" : 'Log.Info( "Done! {0} lasers now ON FIRE!", count )',
      //"Eval" : 'Api.Call( "log", `Done! ${count} lasers now ON FIRE!` )', // Alternative.
   }],
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "JavaScript Runtime" : "https://www.nexusmods.com/phoenixpoint/mods/49",
      "Dump Data" : "https://www.nexusmods.com/phoenixpoint/mods/50",
   },
   Copyright: "Public Domain",
}