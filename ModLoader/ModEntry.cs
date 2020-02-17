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
   public class ModEntry {
      public ModMeta Metadata { get; set; }
      public List<ModEntry> Children { get; set; }

      [ JsonProperty ]
      public bool Disabled { get; set; }
      [ JsonProperty ]
      public bool NoPingback { get; set; }

      public ModEntry ( ModMeta metadata ) {
         Metadata = metadata;
      }
   }

   public class ModMeta {
      public string Id;
      public string Version;
      public string Phase;

      public L10nText Name;
      public string[] Langs;
      public L10nText Description;
      public L10nText Author;
      public L10nText[] Url;
      public L10nText[] Contact;
      public string Pingback;

      public AppVer AppVer;
      public AppVer[] Requires;
      public AppVer[] Conflicts;
      public AppVer[] LoadsAfter;
      public AppVer[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;
   }

   public class L10nText {
      public static string CurrentLang = "en";

      public string Default;
      public Dictionary<string, string> Localised;
      public override string ToString () {
         if ( Localised != null ) {
            if ( Localised.TryGetValue( CurrentLang, out string txt ) ) return txt;
            if ( Localised.TryGetValue( "en", out string eng ) ) return eng;
         }
         return Default;
      }
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
         SkipComment();
         if ( Reader.TokenType == JsonToken.Null ) return null;
         AppVer result = new AppVer();
         if ( Reader.TokenType == JsonToken.String ) {
            result.Id = Reader.Value.ToString();
         } else if ( Reader.TokenType == JsonToken.StartObject ) {
            do {
               SkipComment( true );
               if ( Reader.TokenType == JsonToken.EndObject )
                  break;
               else if ( Reader.TokenType == JsonToken.PropertyName ) {
                  string prop = Reader.Value?.ToString()?.ToLowerInvariant();
                  SkipComment( true );
                  string val = Reader.Value?.ToString();
                  switch ( prop ) {
                     case "id" : result.Id  = val; break;
                     case "min": result.Min = val; break;
                     case "max": result.Max = val; break;
                  }
               } else
                  throw new JsonException( $"Unexpected TokenType.{Reader.TokenType} when parsing AppVerMeta" );
            } while ( true );
         } else
            throw new JsonException( "String or object expected for AppVerMeta" );
         SkipComment( true );
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
