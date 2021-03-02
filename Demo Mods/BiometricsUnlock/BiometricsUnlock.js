({
   Id : "Zy.BiometricUnlocks",
   Name : "Biometric Unlocks",
   Author : "Sheepy",
   Version : "1.1",
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

// Adds code to the *end* of UIModuleCustomization.OnNewCharacter.  We call this a 'Postfix' patch.
Patch.Postfix( UIModuleCustomization, 'OnNewCharacter', function addBiometrics () {

   // Finds all 'def' and adds them to a combobox.
   function addOptions ( box, def ) {
      let list = box?.espy( '_possibleValues' ); // Use reflection helper (espy) to get the option list field.
      for ( let e of Repo.getAll( def ) ) {
         if ( list?.Contains( e ) === false ) {
            console.log( 'Adding {0} {1}', e.GetType().Name, e.ResourcePath );
            list.Add( e );
         }
      }
   }

   // 'this' is the UIModuleCustomization object
   addOptions( this.HeadCustomization, FaceTagDef );
   addOptions( this.HairCustomization, HairTagDef );
   addOptions( this.BeardCustomization, FacialHairTagDef );
   addOptions( this.VoiceCustomization, VoiceProfileTagDef );
   addOptions( this.ArmorColorCustomization, CustomizationPrimaryColorTagDef );
   addOptions( this.ArmorColorCustomizationSecondary, CustomizationSecondaryColorTagDef );
   addOptions( this.PatternCustomization, CustomizationPatternTagDef );

} );

// Since we are not using Include, please avoid or escape double quotes and slashes, or this json will fail to parse.
",
   }],
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "JavaScript Runtime" : "https://www.nexusmods.com/phoenixpoint/mods/49",
   },
   Copyright: "Public Domain",
})