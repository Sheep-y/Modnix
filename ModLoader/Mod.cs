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

   [JsonObject(MemberSerialization.OptIn)]
   public class Mod {
      public ModMeta Metadata { get; set; }
      public List<Mod> Children { get; set; }

      [JsonProperty]
      public bool Disabled { get; set; }
      [JsonProperty]
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

      public AppVerMeta AppVer;
      public AppVerMeta[] Requires;
      public AppVerMeta[] Conflicts;
      public AppVerMeta[] LoadsAfter;
      public AppVerMeta[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;
   }

   public class AppVerMeta {
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
      private readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings() {
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
