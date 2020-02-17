using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix {

   [ JsonObject( MemberSerialization.OptIn ) ]
   public class Mod {
      public ModMeta Metadata { get; set; }
      public List<Mod> Children { get; set; }

      [ JsonProperty ]
      public bool Disabled { get; set; }
      [ JsonProperty ]
      public bool NoPingback { get; set; }

      public Mod ( ModMeta metadata ) {
         Metadata = metadata;
      }
   }

   public class ModMeta {
      public string Id;
      public string Version;
      public string Phase;

      public object Name;
      public string[] Langs;
      public object Description;
      public object Author;
      public object Url;
      public string Pingback;
      public object Contact;

      public AppVer AppVer;
      public AppVer[] Requires;
      public AppVer[] Conflicts;
      public AppVer[] LoadsAfter;
      public AppVer[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;
   }

   public class AppVer {
      public string Id;
      public string Min;
      public string Max;
   }

   public class DllMeta {
      public string Path;
      public string Method;
   }

   public class ModMetaJson {
      public readonly static LoggerProxy JsonLogger = new JsonTraceLogger();
      public readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings() {
         Converters = new JsonConverter[]{ new ModMetaReader() }.ToList(),
         DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
         ReferenceLoopHandling = ReferenceLoopHandling.Error,
         Error = ( sender, err ) => JsonLogger.Error( err ),
         MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
         MissingMemberHandling = MissingMemberHandling.Ignore,
         NullValueHandling = NullValueHandling.Ignore,
         ObjectCreationHandling = ObjectCreationHandling.Replace,
         TraceWriter = JsonLogger as JsonTraceLogger,
         TypeNameHandling = TypeNameHandling.None
      };

      public static T Parse<T> ( string json ) => JsonConvert.DeserializeObject<T>( json, JsonOptions );
      public static ModMeta ParseMod ( string json ) => Parse<ModMeta>( json );
   }

   internal class ModMetaReader : JsonConverter {
      public override bool CanWrite => false;
      private JsonReader Reader;

      public override bool CanConvert ( Type objectType ) {
         if ( objectType == typeof( AppVer ) ) return true;
         if ( objectType == typeof( AppVer[] ) ) return true;
         if ( objectType == typeof( DllMeta ) ) return true;
         if ( objectType == typeof( DllMeta[] ) ) return true;
         return false;
      }

      public override object ReadJson ( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer ) {
         Reader = reader;
         if ( objectType == typeof( AppVer ) ) return ParseAppVer();
         if ( objectType == typeof( AppVer[] ) ) return true;
         if ( objectType == typeof( DllMeta ) ) return true;
         if ( objectType == typeof( DllMeta[] ) ) return true;
         throw new InvalidOperationException();
      }

      private void SkipComment ( bool consumeCurrent = false ) {
         if ( consumeCurrent ) Reader.Read();
         while ( Reader.TokenType == JsonToken.Comment && Reader.Read() );
      }

      private AppVer ParseAppVer () {
         //SkipComment();
         if ( Reader.TokenType == JsonToken.Null ) return null;
         AppVer result = new AppVer();
         if ( Reader.TokenType == JsonToken.String ) {
            result.Id = Reader.Value.ToString();
         } else if ( Reader.TokenType == JsonToken.StartObject ) {
            Reader.Read();
            do {
               //SkipComment();
               if ( Reader.TokenType == JsonToken.EndObject )
                  break;
               else if ( Reader.TokenType == JsonToken.PropertyName ) {
                  string prop = Reader.Value?.ToString()?.ToLowerInvariant();
                  switch ( prop ) {
                     case "id" : result.Id  = Reader.ReadAsString(); break;
                     case "min": result.Min = Reader.ReadAsString(); break;
                     case "max": result.Max = Reader.ReadAsString(); break;
                  }
                  Reader.Read();
                  // SkipComment();
               } else
                  throw new JsonException( $"Expected TokenType.{Reader.TokenType} when parsing AppVerMeta" );
            } while ( true );
            Reader.Read();
         } else
            throw new JsonException( "String or object expected for AppVerMeta" );
         Reader = null;
         return result;
      }

      public override void WriteJson ( JsonWriter writer, object value, JsonSerializer serializer ) {
         throw new InvalidOperationException();
      }

   }

   internal class JsonTraceLogger : LoggerProxy, ITraceWriter {
      internal JsonTraceLogger ( params Logger[] Masters ) : base( false, Masters ) { }
      public TraceLevel LevelFilter { get; set; }
      public void Trace ( TraceLevel level, string message, Exception ex ) {
         if ( level > LevelFilter ) return;
         SourceLevels logLevel;
         switch ( level ) {
            case TraceLevel.Error   : logLevel = SourceLevels.Critical; break;
            case TraceLevel.Warning : logLevel = SourceLevels.Warning; break;
            case TraceLevel.Info    : logLevel = SourceLevels.Information; break;
            case TraceLevel.Verbose : logLevel = SourceLevels.Verbose; break;
            default: return;
         }
         Log( logLevel, message );
      }
   }

}
