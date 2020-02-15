using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Sheepy.Modnix.MainGUI {

   #pragma warning disable CA1051 // Do not declare visible instance fields
   public class GithubRelease {
      public string tag_name;
      public string html_url;
      public bool prerelease = true;
      public GithubAsset[] assets;
   }

   public class GithubAsset {
      public string name;
      public string state;
      public long   size;
      public string browser_download_url;
   }
   #pragma warning restore CA1051 // Do not declare visible instance fields

   internal class Updater {
      private const string RELEASE  = "https://api.github.com/repos/Sheep-y/Modnix/releases";

      private readonly AppControl App;
      private JsonSerializerSettings jsonOptions;

      internal Updater ( AppControl app ) {
         App = app;
         ServicePointManager.Expect100Continue = true;
         ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
         jsonOptions = new JsonSerializerSettings() {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            Error = ( sender, err ) => App.Log( err ),
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TraceWriter = new JsonLogger() { App = App, LevelFilter = TraceLevel.Info }
         };
      }

      internal GithubRelease FindUpdate ( Version update_from ) { try {
         App.Log( $"Checking update from {RELEASE}" );
         HttpWebRequest request = WebRequest.Create( new Uri( RELEASE ) ) as HttpWebRequest;
         if ( request == null )
            return App.Log<GithubRelease>( "WebRequest is not HttpWebRequest", null );
         request.Credentials = CredentialCache.DefaultCredentials;
         request.UserAgent = $"{AppControl.LIVE_NAME}-Updater/{App.CheckAppVer()}";
         request.Accept = "application/vnd.github.v3+json";

         string json = null;
         try {
            using ( WebResponse response = request.GetResponse() ) {
               json = ReadAsString( request.GetResponse() );
               App.Log( json );
            }
         } catch ( WebException wex ) {
            App.Log( wex );
            return App.Log<GithubRelease>( ReadAsString( wex.Response ), null );
         }

         GithubRelease[] releases = JsonConvert.DeserializeObject<GithubRelease[]>( json, jsonOptions );
         App.Log( $"Found {releases?.Length} releases." );
         if ( RELEASE == null || releases.Length <= 0 ) return null;
         foreach ( var e in releases ) try {
            App.Log( $"{e.tag_name} ({(e.prerelease?"Prerelease":"Production")}) {e.assets?.Length??0} asset(s)" );
            if ( String.IsNullOrWhiteSpace( e.tag_name ) || e.tag_name[0] != 'v' ) continue;
            if ( e.assets == null || e.assets.Length <= 0 ) continue;
            if ( ! Object.Equals( MainGUI.Properties.Settings.Default.Update_Branch, "dev" ) && e.prerelease ) continue;
            Version eVer = Version.Parse( e.tag_name.Substring( 1 ) );
            if ( eVer <= update_from ) continue;
            foreach ( var a in e.assets ) {
               App.Log( $"{a.name} {a.state} {a.size} bytes {a.browser_download_url}" );
               if ( a.state == "uploaded" && a.name.EndsWith( ".exe", StringComparison.InvariantCultureIgnoreCase ) ) {
                  e.assets = new GithubAsset[] { a };
                  return e;
               }
            }
         } catch ( Exception ex ) { App.Log( ex ); } 
         return null;
      } catch ( Exception ex ) { return App.Log<GithubRelease>( ex, null ); } }

      private string ReadAsString ( WebResponse response ) {
         using ( StreamReader reader = new StreamReader( response.GetResponseStream() ) ) {
            return reader.ReadToEnd ();
         }
      }
   }

   internal class JsonLogger : ITraceWriter {
      internal AppControl App;
      public TraceLevel LevelFilter { get; set; }
      public void Trace ( TraceLevel level, string message, Exception ex ) {
         if ( level > LevelFilter ) return;
         App.Log( message ?? ex?.ToString() );
      }
   }
}
