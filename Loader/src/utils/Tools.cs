using System.IO;
using System.Text;

namespace Sheepy.Modnix {
   public static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   
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
