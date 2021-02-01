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
      "OnError" : "Warn, Skip", // Default is "Log, Stop" which logs the error and stops execution.
      // "Phase" : "MainMod",  // Default phase is "GameMod" which is when player first start/load a game.
      // The MainMod phase runs on Home screen, which is faster to test, but there will be no campaign data.
   },{
      // Variables to keep track of status, for logging a report.
      "Eval" : 'let startTime = new Date();
                let done = [];
                let weapon;',
   },{
      // Add 1 fire damage to "SY_LaserPistol_WeaponDef".
      "Eval" : 'weapon = Repo.Get( WeaponDef, "SY_LaserPistol_WeaponDef" )
                             .Fire( 1 );
                done.push( weapon ); ',
   },{
      "Eval" : 'weapon = Repo.Get( WeaponDef, "SY_LaserAssaultRifle_WeaponDef" )
                             .Fire( 1 );
                done.push( weapon ); ', // `done` is a JavaScript array, thus the lowercase methods
   },{
      // Add fire and pierce.
      "Eval" : 'weapon = Repo.Get( WeaponDef, "SY_LaserSniperRifle_WeaponDef" )
                             .Fire( 3 )
                             .Pierce( 5 );
                done.push( weapon ); ',
   },{
      "Eval" : 'weapon = Repo.Get( WeaponDef, "PX_LaserPDW_WeaponDef" )
                             .Fire( 1 );
                done.push( weapon ); ',
   },{
      // Add fire and shred.  Normal damage is just "Damage", by the way.
      "Eval" : 'weapon = Repo.Get( WeaponDef, "PX_LaserTechTurretGun_WeaponDef" )
                             .Fire( 1 )
                             .Shred( 2 );
                done.push( weapon ); ',
   },{
      "Eval" : 'weapon = Repo.Get( WeaponDef, "PX_LaserArrayPack_WeaponDef" )
                             .Fire( 2 );
                done.push( weapon ); ',
   },{
      // This one is mine.  You didn't know I am an AI on a hidden space platform?
      "Eval" : 'weapon = Repo.Get( WeaponDef, "ZY_LaserOrbitalPlatform_WeaponDef" )
                             .Fire( 2000 );
                done.push( weapon ); ', // The first line crashed.  Get return null.  So this line will not run.
      // Of course it would fail.  Too wimpy.  Who would install a weapon that can't wipe out humanity.
      // So, do not log, and skip to next action.  (Log is bugged in Modnix 3 Beta 2; will be fixed.)
      "OnError" : "NoLog, Skip",
   },{
      // A little gem.  Skip to next code if too complex.
      // Idea: if Ares AR-1's damage is exactly 25, change it to 30.
      "Eval" : 'weapon = Repo.Get( WeaponDef, "PX_AssaultRifle_WeaponDef" );
                let expectedDamage = 25;
                let actualDamage = weapon?.CompareExchangeDamage( "normal", expectedDamage, 30 );
                if ( actualDamage === expectedDamage ) done.push( weapon );
                actualDamage; ', // The last value, this value, is the action result and will be logged.
      // Now, when the damage is changed, by a future game patch or by another mod, our change will not be applied.
   },{
      "Eval" : '// Log a summary. Yes this comment is _in_ the code!
                console.log( `Done! ${done.length} lasers now ON FIRE!` );
                // Alternative logging syntax.
                Log.Info( "Took time: {0}ms", new Date() - startTime );
                // Another way, this time through generic Modnix API.
                Api.Call( "log verbose", done.map( e => e.name ).join( ", " ) ); ',
   }],
      // That's it!  Not too hard, right?  Yeah.  Easy job.  For weapon damage.
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "JavaScript Runtime" : "https://www.nexusmods.com/phoenixpoint/mods/49",
      "Dump Data" : "https://www.nexusmods.com/phoenixpoint/mods/50",
   },
   Copyright: "Public Domain",
}