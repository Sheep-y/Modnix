// This file is part of the Laser On Fire mod.
// Before this file is ran, a few variables has already been declared.

// Here we'll mod down the Destiny III's damage,
// but only if it is not already lower than our target.

weapon = Repo.get( WeaponDef, "fd84633b-79a3-82e4-fb53-07ab8c77854" );
                   // same as "PX_LaserArrayPack_WeaponDef", i.e. Destiny III

let newDamage = 60, currentDamage = weapon?.damage(); // No param = get specific damage, in this case the normal (untyped) damage
if ( currentDamage > newDamage ) {
   weapon.damage( newDamage ); // Set normal (untyped) damage
   done.push( weapon );
}
// An alternative is to use weapon.compareExchangeDamage, but it is overkill.
// Threading, race condition, atomic operation and all that.

// The last value, this value, is the action result and will be logged.
currentDamage; 

// These demo mods are just a tip of the iceberg, of course.
// We are talking about _two_ mature, modern, evolving languages.
// 
// You can declare your own functions and reuse them,
// listen to game events, or call on the full might of .Net Framework.
//
// Of couse, it won't be easy.
// Make scripting possible, is about as easy as I can make it to be.
// It makes a huge difference when you can just type in the console
// and see the result in realtime.  Hopefully it's worth the trouble.
