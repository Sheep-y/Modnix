using System.IO;
using System.Runtime.Remoting.Messaging;
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
   }
}
