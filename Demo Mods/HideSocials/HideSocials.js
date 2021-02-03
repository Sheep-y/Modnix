({
   Id : "Zy.HideSocials",
   Name : "Hide Socials",
   Author : "Sheepy",
   Version : "1.0",
   Requires: [
      { Id: "Zy.JavaScript", Min: "2.1", Name: "JavaScript Runtime 2.1+", Url: "https://www.nexusmods.com/phoenixpoint/mods/49" },
   ],
   Description : "

Hide the social icons and version text on the game's home screen.
You can still find game version from in-game Patch Notes and from Modnix.

This is done as a simple demo of Modnix 3 Scripting,
which allow plain-text JavaScript mod to modify stuffs.

In this mod, the game's User Interface is modified.

A full step by step tutorial will be added to the wiki in the future.

This mod requires Modnix 3+ and JavaScript Runtime.
Tested on Phoenix Point 1.9.3.

",
   // Different mods support different type of actions, and they can be mixed in the same mod.
   Actions : [{
      // HomeOnShow is triggered whenever the game shows the home screen
      Phase : "HomeOnShow",
      // An Action with "Script":"JavaScript" and "Eval":"(code)" will be handled by JavaScript Runtime.
      Script : "JavaScript",
      Eval : "

// This code finds the social bar (called Socials),
GameObject.Find( 'Socials' )
   .GetComponent( BasicTween ) // This line find its animation (BasicTween)
   .enabled = false;           // And then disable the animation

// This code find the version text, and disables it.
GameObject.Find( 'RevisionText' ).SetActive( false );

"
   }],
   Url : {
      "GitHub" : "https://github.com/Sheep-y/Modnix/tree/master/Demo%20Mods#readme",
      "Modnix": "https://github.com/Sheep-y/Modnix/wiki/",
      "JavaScript Runtime" : "https://www.nexusmods.com/phoenixpoint/mods/49",
   },
   Copyright: "Public Domain",
})