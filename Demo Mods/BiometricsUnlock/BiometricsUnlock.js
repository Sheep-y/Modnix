({
   Id : "Zy.BiometricUnlocks",
   Name : "Biometric Unlocks",
   Author : "Sheepy",
   Version : "1.0",
   Requires: [{ Id: "Zy.JavaScript", Min: "2.2" }],
   Description : "

Unlock all faces, hairs, and voices.

This is done as a demo of Modnix 3 Scripting,
which allow plain-text JavaScript mod to modify stuffs.

In this mod, the game's solider customisation screen is patched,
to add the new options when you enter the screen.

This mod requires Modnix 3+ and JavaScript Runtime 2.2+.
Tested on Phoenix Point 1.10.

",
   Actions : [{
      "Phase" : "GeoscapeMod",
      "Script"  : "JavaScript",
      // In the game, you can use this JavaScript to get the customisation screen, when you are on any Geoscape screens:
      //   GameUtl.CurrentLevel().GetComponent( GeoscapeView ).GeoscapeModules.CustomizationModule
      "Eval"  : "

// This adds some code after UIModuleCustomization.OnNewCharacter. We call this code a 'Postfix' patch.
Patch.Postfix( UIModuleCustomization, 'OnNewCharacter', ( me ) => {

   // And in the code, we can define reusable local functions. This one finds all 'def' and adds them to a combobox.
   function addOptions ( box, def ) {
      let list = box?.espy( '_possibleValues' ); // Use reflection helper (espy) to get the option list field.
      if ( list == null ) return console.warn( `List ${Espy.create(def).myType.Name} is null` );
      for ( let e of Repo.getAll( def ) )
         if ( list?.Contains( e ) === false ) {
            console.log( 'Adding {0} {1}', e.GetType().Name, e.ResourcePath );
            list.Add( e );
         }
   }

   addOptions( me.HeadCustomization, FaceTagDef );
   addOptions( me.HairCustomization, HairTagDef );
   addOptions( me.BeardCustomization, FacialHairTagDef );
   addOptions( me.VoiceCustomization, VoiceProfileTagDef );
   //Patch.UnpatchAll(); // If your patch only need to run once, you can unpatch after run.

} );

// Just in case.  Since we are not using Include, you must avoid or escape double quotes and slashes, or this json mod will fail to parse.

",
   }],
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "JavaScript Runtime" : "https://www.nexusmods.com/phoenixpoint/mods/49",
   },
   Copyright: "Public Domain",
})