using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sheepy.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sheepy.Modnix {
   /// <summary>
   /// A collection of objects and methods to make parsing mod json easier.
   /// </summary>
   public static class Json {
      public readonly static LoggerProxy JsonLogger = new JsonTraceLogger();
      public readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings{
         Converters = new JsonConverter[]{ new ModMetaReader() }.ToList(),
         ContractResolver = new DefaultContractResolver(),
         DefaultValueHandling = DefaultValueHandling.Include,
         ReferenceLoopHandling = ReferenceLoopHandling.Error,
         Error = ( sender, err ) => JsonLogger.Error( err ),
         MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
         MissingMemberHandling = MissingMemberHandling.Ignore,
         NullValueHandling = NullValueHandling.Include,
         ObjectCreationHandling = ObjectCreationHandling.Replace,
         TraceWriter = JsonLogger as JsonTraceLogger,
         TypeNameHandling = TypeNameHandling.None
      };

      public static Regex RegxVerTrim = new Regex( "(\\.0){1,2}$", RegexOptions.Compiled );
      public static string TrimVersion ( Version ver ) => TrimVersion( ver.ToString() );
      public static string TrimVersion ( string ver ) => RegxVerTrim.Replace( ver, "" );

      public static bool ParseVersion ( string txt, out Version result ) {
         result = null;
         if ( string.IsNullOrWhiteSpace( txt ) ) return false;
         var nums = new int[]{ 0, 0, 0, 0 };
         var parts = txt.Trim().Split( new char[]{ '.' }, 4 );
         for ( int i = 0 ; i < 4 ; i++ )
            if ( parts.Length > i )
               if ( !int.TryParse( parts[ i ], out nums[ i ] ) )
                  return false;
         result = new Version( nums[ 0 ], nums[ 1 ], nums[ 2 ], nums[ 3 ] );
         return true;
      }

      public static T Parse<T> ( string json ) => JsonConvert.DeserializeObject<T>( json, JsonOptions );
      public static object Parse ( string json, Type type ) => JsonConvert.DeserializeObject( json, type, JsonOptions );
      public static string Stringify ( object val ) => JsonConvert.SerializeObject( val, Formatting.Indented, JsonOptions );
      public static ModMeta ParseMod ( string json ) => Parse<ModMeta>( json );

      internal static JsonToken ReadAndSkipComment ( this JsonReader r ) {
         r.Read();
         return SkipComment( r );
      }
      internal static JsonToken SkipComment ( this JsonReader r ) {
         while ( r.TokenType == JsonToken.Comment && r.Read() ) ;
         return r.TokenType;
      }
   }

}
