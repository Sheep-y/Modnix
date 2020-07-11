using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sheepy.Modnix {
   public static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );

      internal static bool IsSafePath ( string path ) {
         if ( string.IsNullOrWhiteSpace( path ) ) return false;
         if ( path.Trim().Length != path.Length ) return false;
         if ( path.IndexOf( ".." ) >= 0 ) return false;
         if ( path.IndexOfAny( new char[]{ '/', '\\', ':' } ) == 0 ) return false;
         if ( path.Length >= 2 && path[1] == ':' ) return false;
         foreach ( var chr in Path.GetInvalidFileNameChars() ) {
            if ( chr == '/' || chr == '\\' ) continue;
            if ( path.IndexOf( chr ) >= 0 ) return false;
         }
         return true;
      }

      private static StreamReader Read ( string file ) =>
         new StreamReader( new FileStream( file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ), Encoding.UTF8, true );

      internal static string ReadText ( string file ) { using ( var reader = Read( file ) ) return reader.ReadToEnd(); }
      internal static string ReadLine ( string file ) { using ( var reader = Read( file ) ) return reader.ReadLine(); }

      public static string ReadText ( Stream input ) {
         using ( var stream = new StreamReader( input, Encoding.UTF8, true ) ) {
            return stream.ReadToEnd();
         }
      }

      private static readonly Dictionary< string, HashSet< string > > StrLists = new Dictionary<string, HashSet<string>>();
      private static readonly char[] ListSeparator = new char[]{ ',', ';' };

      internal static bool InList ( string[] list, string val ) => InList( string.Join( ",", list ), val );

      internal static bool InList ( string list, string val ) {
         if ( string.IsNullOrWhiteSpace( list ) ) return false;
         list = list.Trim().ToLowerInvariant();
         HashSet<string> parsed;
         lock ( StrLists ) {
            if ( ! StrLists.TryGetValue( list, out parsed ) ) {
               parsed = new HashSet<string>( list.Split( ListSeparator, StringSplitOptions.RemoveEmptyEntries ).Select( e => e.Trim() ) );
               if ( parsed.Count == 0 ) parsed = null;
               StrLists.Add( list, parsed );
            }
         }
         return parsed?.Contains( val ) == true;
      }
   }
}
