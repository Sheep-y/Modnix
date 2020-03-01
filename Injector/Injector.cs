using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static System.Console;

namespace Sheepy.Modnix {
   internal static class Injector {
      // return codes
      private const int RC_NORMAL = 0;
      private const int RC_UNHANDLED_STATE = 1;
      private const int RC_BAD_OPTIONS = 2;
      private const int RC_BACKUP_FILE_ERROR = 3;
      //private const int RC_BACKUP_FILE_INJECTED = 4;
      private const int RC_BAD_MANAGED_DIRECTORY_PROVIDED = 5;
      private const int RC_MISSING_MOD_LOADER_ASSEMBLY = 6;
      private const int RC_REQUIRED_GAME_VERSION_MISMATCH = 7;
      private const int RC_INJECTION_FAILED = 8;

      internal const string MOD_LOADER_NAME = "Modnix";
      internal const string MOD_INJECTOR_EXE_FILE_NAME = "ModnixInjector.exe";
      internal const string MOD_LOADER_DLL_FILE_NAME = "ModnixLoader.dll";
      internal const string INJECT_TO_DLL_FILE_NAME = "Cinemachine.dll";
      internal const string GAME_DLL_FILE_NAME = "Assembly-CSharp.dll";
      internal const string BACKUP_FILE_EXT = ".orig";

      // PPML Late injection goes here
      //internal const string HOOK_TYPE     = "PhoenixPoint.Common.Game.PhoenixGame";
      //internal const string HOOK_METHOD   = "BootCrt";
      internal const string HOOK_TYPE     = "Cinemachine.CinemachineBrain";
      internal const string HOOK_METHOD   = "OnEnable";
      internal const string INJECT_TYPE   = "Sheepy.Modnix.ModLoader";
      internal const string INJECT_METHOD = "Init";
      //internal const string INJECT_CALL   = "MenuCrt";

      internal const string PPML_INJECTOR_EXE      = "PhoenixPointModLoaderInjector.exe";
      internal const string PPML01_INJECTOR_TYPE   = "PhoenixPointModLoader.PPModLoader";
      internal const string PPML01_INJECTOR_METHOD = "Init";
      internal const string PPML02_INJECTOR_TYPE   = "PhoenixPointModLoader.PhoenixPointModLoader";
      internal const string PPML02_INJECTOR_METHOD = "Initialize";

      internal const string GAME_VERSION_TYPE   = "Base.Build.RuntimeBuildInfo";
      internal const string GAME_VERSION_METHOD = "get_Version";

      private static readonly AppState State = new AppState();
      private static readonly ReceivedOptions OptionsIn = new ReceivedOptions();
      private static readonly OptionSet Options = new OptionSet {
         {
            "d|detect", "Detect game assembly's injection state: none, modnix, ppml",
            v => OptionsIn.Detecting = v != null
         },
         {
            "g|gameversion", "Print game version",
            v => OptionsIn.GameVersion = v != null
         },
         {
            "h|?|help", "Print this useful help message",
            v => OptionsIn.Helping = v != null
         },
         {
            "i|install", "Install the Mod (this is the default behavior)",
            v => OptionsIn.Installing = v != null
         },
         {
            "manageddir=", "Specify managed dir where game's Assembly-CSharp.dll is located",
            v => OptionsIn.ManagedDir = v
         },
         {
            "y|nokeypress", "Anwser prompts affirmatively",
            v => OptionsIn.RequireKeyPress = v == null
         },
         {
            "requiredversion=", "Don't continue with /install, /restore, etc. if game version does not match given argument",
            v => OptionsIn.RequiredGameVersion = v
         },
         {
            "r|restore", "Restore pristine backup game assembly to folder",
            v => OptionsIn.Restoring = v != null
         },
         {
            "v|version", "Print injector version number",
            v => OptionsIn.Versioning = v != null
         }
      };

      private static int Main ( string[] args ) {
         try {
            ParseOptions( args );
            LoadGameAssembly();

            SayHeader();

            if ( OptionsIn.Restoring ) {
               if ( ( State.ppmlDll.Status | State.modxDll.Status ) == InjectionState.NONE )
                  SayAlreadyRestored();
               else {
                  if ( State.ppmlDll.Status > InjectionState.NONE )
                     State.ppmlDll.Restore();
                  if ( State.modxDll.Status > InjectionState.NONE )
                     State.modxDll.Restore();
               }
               return PromptForKey();
            }

            if ( OptionsIn.Installing ) {
               bool injected = false;
               try {
                  if ( State.ppmlDll.Status > InjectionState.NONE ) {
                     SayPpmlMigrate();
                     State.ppmlDll.Restore();
                  }
                  if ( State.modxDll.Status == InjectionState.NONE ) {
                     State.modxDll.Backup();
                     injected = State.modxDll.Inject( State.modLoaderDllPath );
                  } else if ( State.modxDll.Status == InjectionState.MODNIX ) {
                     SayAlreadyInjected();
                     injected = true;
                  } else {
                     throw new InvalidOperationException( $"Unexpected injection status: {State.modxDll.Status}" );
                  }
               } catch ( Exception ex ) {
                  SayException( ex );
               }
               if ( injected )
                  SayPpmlWarning();
               return PromptForKey( injected ? RC_NORMAL : RC_INJECTION_FAILED );
            }

         } catch ( BackupFileError e ) {
            SayException( e );
            return SayHowToRecoverMissingBackup( e.BackupFileName );

         } catch ( Exception e ) {
            SayException( e );
         }

         return RC_UNHANDLED_STATE;
      }

      private static void Exit ( int exitCode ) => Environment.Exit( exitCode );

      private static void ParseOptions ( string[] args ) {
         try {
            Options.Parse( args );
         } catch ( OptionException e ) {
            Exit( SayOptionException( e ) );
         }

         if ( OptionsIn.Helping )
            Exit( SayHelp( Options ) );

         if ( OptionsIn.Versioning )
            Exit( SayVersion() );
      }

      private static void LoadGameAssembly () {
         if ( ! string.IsNullOrEmpty( OptionsIn.ManagedDir ) ) {
            if ( ! Directory.Exists( OptionsIn.ManagedDir ) )
               Exit( SayManagedDirMissingError( OptionsIn.ManagedDir ) );
            State.managedDirectory = Path.GetFullPath( OptionsIn.ManagedDir );
         } else
            State.managedDirectory = Directory.GetCurrentDirectory();

         State.modLoaderDllPath    = Path.Combine( State.managedDirectory, MOD_LOADER_DLL_FILE_NAME );
         State.ppmlInjectorPath    = Path.Combine( State.managedDirectory, PPML_INJECTOR_EXE );
         State.modxDll = new TargetFile( State.managedDirectory, INJECT_TO_DLL_FILE_NAME );
         State.ppmlDll = new TargetFile( State.managedDirectory, GAME_DLL_FILE_NAME );

         if ( ! File.Exists( State.modxDll.Target ) )
            Exit( PromptForKey( SayGameAssemblyMissingError( OptionsIn.ManagedDir ) ) );

         if ( ! File.Exists( State.modLoaderDllPath ) )
            Exit( PromptForKey( SayModLoaderAssemblyMissingError( State.modLoaderDllPath ) ) );

         State.gameVersion = State.ppmlDll.ReadVersion();
         if ( OptionsIn.GameVersion )
            Exit( SayGameVersion( State.gameVersion ) );

         if ( ! string.IsNullOrEmpty( OptionsIn.RequiredGameVersion ) && OptionsIn.RequiredGameVersion != State.gameVersion )
            Exit( PromptForKey( SayRequiredGameVersionMismatchMessage( State.gameVersion, OptionsIn.RequiredGameVersion ) ) );

         Task.WaitAll(
            Task.Run( State.ppmlDll.CheckInjection ),
            Task.Run( State.modxDll.CheckInjection ) );
         var state = State.ppmlDll.Status | State.modxDll.Status;
         if ( OptionsIn.Detecting )
            Exit( SayInjectedStatus( state ) );
      }

      private static int SayInjectedStatus ( InjectionState injected ) {
         WriteLine( injected.ToString().ToLower() );
         return RC_NORMAL;
      }

      #region Console output
      private static int SayOptionException ( OptionException e ) {
         SayHeader();
         Write( $"{MOD_INJECTOR_EXE_FILE_NAME}: {e.Message}" );
         WriteLine( $"Try '{MOD_INJECTOR_EXE_FILE_NAME} --help' for more information." );
         return RC_BAD_OPTIONS;
      }

      private static int SayHelp ( OptionSet p ) {
         SayHeader();
         WriteLine( $"Usage: {MOD_INJECTOR_EXE_FILE_NAME} [OPTIONS]+" );
         WriteLine( "Inject the Phoenix Point game assembly with an entry point for mod loading." );
         WriteLine( "If no options are specified, the program assumes you want to /install." );
         WriteLine();
         WriteLine( "Options:" );
         p.WriteOptionDescriptions( Out );
         return RC_NORMAL;
      }

      private static int SayVersion () {
         WriteLine( GetProductVersion() );
         return RC_NORMAL;
      }

      private static int SayManagedDirMissingError ( string givenManagedDir ) {
         SayHeader();
         WriteLine( $"ERROR: We could not find the directory '{givenManagedDir}'. Are you sure it exists?" );
         return RC_BAD_MANAGED_DIRECTORY_PROVIDED;
      }

      private static int SayGameAssemblyMissingError ( string givenManagedDir ) {
         SayHeader();
         WriteLine( $"ERROR: We could not find target assembly {INJECT_TO_DLL_FILE_NAME} in directory '{givenManagedDir}'.\n" +
             "Are you sure that is the correct directory?" );
         return RC_BAD_MANAGED_DIRECTORY_PROVIDED;
      }

      private static int SayModLoaderAssemblyMissingError ( string expectedModLoaderAssemblyPath ) {
         SayHeader();
         WriteLine( $"ERROR: We could not find the loader assembly {MOD_LOADER_DLL_FILE_NAME} at '{expectedModLoaderAssemblyPath}'.\n" +
             $"Is {MOD_LOADER_DLL_FILE_NAME} in the correct place?  It should be in the same directory as this injector executable." );
         return RC_MISSING_MOD_LOADER_ASSEMBLY;
      }

      private static int SayGameVersion ( string version ) {
         WriteLine( version );
         return RC_NORMAL;
      }

      private static int SayRequiredGameVersionMismatchMessage ( string version, string expectedVersion ) {
         WriteLine( $"Expected game v{expectedVersion}" );
         WriteLine( $"Actual game v{version}" );
         WriteLine( "Game version mismatch" );
         return RC_REQUIRED_GAME_VERSION_MISMATCH;
      }

      private static string GetProductVersion () {
         var assembly = Assembly.GetExecutingAssembly();
         var fvi = FileVersionInfo.GetVersionInfo( assembly.Location );
         return fvi.FileVersion;
      }

      private static void SayHeader () {
         WriteLine( $"{MOD_LOADER_NAME} Injector {GetProductVersion()}" );
         WriteLine( "----------------------------" );
      }

      private static int SayHowToRecoverMissingBackup ( string backupFileName ) {
         WriteLine( "----------------------------" );
         WriteLine( $"The backup game assembly file must be in the directory with the injector for /restore to work.  The backup file should be named \"{backupFileName}\"." );
         WriteLine( "You may need to reinstall or use Steam/GOG's file verification function if you have no other backup." );
         return RC_BACKUP_FILE_ERROR;
      }

      private static void SayHowToRecoverInjectedBackup ( string backupFileName ) {
         WriteLine( "----------------------------" );
         WriteLine( $"The backup game assembly file named \"{backupFileName}\" was already injected.  Something has gone wrong." );
         WriteLine( "You may need to reinstall or use Steam/GOG's file verification function if you have no other backup." );
      }

      private static void SayPpmlMigrate () => WriteLine( $"{GAME_DLL_FILE_NAME} already injected by PPML.  Reverting the file and migrate to Modnix." );

      private static void SayAlreadyInjected () => WriteLine( $"{INJECT_TO_DLL_FILE_NAME} already injected with {INJECT_TYPE}.{INJECT_METHOD}." );

      private static void SayAlreadyRestored () => WriteLine( "Game DLLs already clean.  No injection to revert." );

      private static void SayException ( Exception e ) => WriteLine( $"ERROR: An exception occured: {e}" );

      private static void SayPpmlWarning () {
         if ( File.Exists( State.ppmlInjectorPath ) )
            WriteLine( $"!!! {PPML_INJECTOR_EXE} found.  Deletion is advised as PPML is incompaible with Modnix. !!!" );
      }

      private static int PromptForKey ( int returnCode = RC_NORMAL ) {
         if ( OptionsIn.RequireKeyPress ) {
            WriteLine( "Press any key to continue." );
            ReadKey();
         }
         return returnCode;
      }
      #endregion
   }

   public class BackupFileError : Exception {
      public BackupFileError ( string backupFileName, string message ) : base( message ) {
         BackupFileName = backupFileName;
         message = $"The backup file \"{backupFileName}\" ${message}.";
      }
      public readonly string BackupFileName;
   }

   // Values passed by the user into the program via command line.
   internal class ReceivedOptions {
      public bool RequireKeyPress = true;
      public bool Detecting = false;
      public string RequiredGameVersion = string.Empty;
      public string ManagedDir = string.Empty;
      public bool GameVersion = false;
      public bool Helping = false;
      public bool Installing = true;
      public bool Restoring = false;
      public bool Versioning = false;
   }

   // TODO: Make Injector non-static and merge this as fields.
   internal class AppState {
      internal string managedDirectory;
      internal string gameVersion;
      internal string modLoaderDllPath;
      internal string ppmlInjectorPath;
      internal TargetFile modxDll;
      internal TargetFile ppmlDll;
   }

   internal enum InjectionState { NONE = 0, MODNIX = 1, PPML = 2, BOTH = 3 }

   internal class TargetFile {
      internal readonly string Directory;
      internal readonly string Filename;
      internal readonly string Target;
      internal readonly string BackupFile;
      internal InjectionState Status;

      internal TargetFile ( string directory, string filename ) {
         Directory = directory;
         Filename = filename;
         Target = Path.Combine( Directory, Filename );
         BackupFile = Target + Injector.BACKUP_FILE_EXT;
      }

      internal void Backup () {
         File.Copy( Target, BackupFile, true );
         if ( ! File.Exists( BackupFile ) )
            throw new BackupFileError( BackupFile, "could not be made" );
         WriteLine( $"{Path.GetFileName( Target )} backed up to {Path.GetFileName( BackupFile )}" );
      }

      internal void Restore () {
         if ( ! File.Exists( BackupFile ) )
            throw new BackupFileError( BackupFile, "could not be found" );

         if ( CheckInjectionOf( BackupFile ) != InjectionState.NONE )
            throw new BackupFileError( BackupFile, "was injected" );

         File.Copy( BackupFile, Target, true );
         WriteLine( $"{Path.GetFileName( BackupFile )} restored to {Path.GetFileName( Target )}" );
      }

      internal InjectionState CheckInjection () => CheckInjectionOf( Target );
      
      internal InjectionState CheckInjectionOf ( string target ) {
         using ( var dll = ModuleDefinition.ReadModule( target ) ) {
            foreach ( var type in dll.Types ) {
               if ( type.IsNotPublic ) continue;
               var result = CheckInjection( type );
               if ( result != InjectionState.NONE ) {
                  if ( Target.Equals( target ) )
                     Status = result;
                  return result;
               }
            }
         }
         return InjectionState.NONE;
      }

      private InjectionState CheckInjection ( TypeDefinition typeDefinition ) {
         // Check standard methods, then in places like IEnumerator generated methods (Nested)
         var result = typeDefinition.Methods.Select( CheckInjection ).FirstOrDefault( e => e != InjectionState.NONE );
         if ( result != InjectionState.NONE ) return result;
         return typeDefinition.NestedTypes.Select( CheckInjection ).FirstOrDefault( e => e != InjectionState.NONE );
      }

      private static readonly string ModnixInjectCheck = $"System.Void {Injector.INJECT_TYPE}::{Injector.INJECT_METHOD}(";
      private static readonly string PPML01InjectCheck = $"System.Void {Injector.PPML01_INJECTOR_TYPE}::{Injector.PPML01_INJECTOR_METHOD}(";
      private static readonly string PPML02InjectCheck = $"System.Void {Injector.PPML02_INJECTOR_TYPE}::{Injector.PPML02_INJECTOR_METHOD}(";

      private InjectionState CheckInjection ( MethodDefinition methodDefinition ) {
         if ( methodDefinition.Body == null )
            return InjectionState.NONE;
         foreach ( var instruction in methodDefinition.Body.Instructions ) {
            if ( ! instruction.OpCode.Equals( OpCodes.Call ) ) continue;
            string op = instruction.Operand.ToString();
            if ( op.StartsWith( ModnixInjectCheck ) )
               return InjectionState.MODNIX;
            else if ( op.StartsWith( PPML01InjectCheck ) || op.StartsWith( PPML02InjectCheck ) )
               return InjectionState.PPML;
         }
         return InjectionState.NONE;
      }

      internal bool Inject ( string injectFilePath ) {
         WriteLine( $"Injecting {Path.GetFileName(Target)} with {Injector.INJECT_TYPE}.{Injector.INJECT_METHOD} at {Injector.HOOK_TYPE}.{Injector.HOOK_METHOD}" );
         using ( var game = ModuleDefinition.ReadModule( Target, new ReaderParameters { ReadWrite = true } ) )
         using ( var injecting = ModuleDefinition.ReadModule( injectFilePath ) ) {
            var success = InjectModHookPoint( game, injecting );
            if ( success )
               success &= SaveAssembly( game );
            if ( ! success )
               WriteLine( "Injection failed." );
            return success;
         }
      }

      private bool InjectModHookPoint ( ModuleDefinition game, ModuleDefinition injecting ) {
         // get the methods that we're hooking and injecting
         var injectedMethod = injecting.GetType( Injector.INJECT_TYPE ).Methods.Single( x => x.Name == Injector.INJECT_METHOD );
         var hookedMethod = game.GetType( Injector.HOOK_TYPE ).Methods.First( x => x.Name == Injector.HOOK_METHOD );
         /*
         PPML injection code for late-injection (after logos and game splash)

         // Since the return type is an iterator -- need to go searching for its MoveNext method which contains the actual code you'll want to inject
         if ( hookedMethod.ReturnType.Name.Contains( "IEnumerator" ) ) {
            var nestedIterator = game.GetType( HOOK_TYPE ).NestedTypes.First( x => x.Name.Contains( HOOK_METHOD ) );
            hookedMethod = nestedIterator.Methods.First( x => x.Name.Equals( "MoveNext" ) );
         }

         // As of Phoenix Point v1.0.54973 the BootCrt() iterator method of PhoenixGame has this at the end
         //
         //  ...
         //    IEnumerator<NextUpdate> coroutine = this.MenuCrt(null, MenuEnterReason.None);
         //    yield return timing.Call(coroutine, null);
         //    yield break;
         //  }
         //
         // We want to inject after the MenuCrt call -- so search for that call in the CIL

         WriteLine( $"Scanning for {INJECT_CALL} call in {HOOK_METHOD}." );

         var body = hookedMethod.Body;
         var code = body.Instructions;
         WriteLine( $"Total {code.Count} IL instructions." );
         for ( var i = code.Count - 1 ; i >= 0 ; i-- ) {
            var instruction = code[i];
            var opcode = instruction.OpCode;
            if ( ! opcode.Code.Equals( Code.Call ) ) continue;
            if ( ! opcode.OperandType.Equals( OperandType.InlineMethod ) ) continue;
            var methodReference = instruction.Operand as MethodReference;
            if ( methodReference != null && methodReference.Name.Contains( INJECT_CALL ) ) {
               WriteLine( $"Call found. Inserting mod loader after call." );
               body.GetILProcessor().InsertAfter( instruction, Instruction.Create( OpCodes.Call, game.ImportReference( injectedMethod ) ) );
               WriteLine( "Insertion done." );
               return true;
            }
         }
         */

         var body = hookedMethod.Body;
         var len = body.Instructions.Count;
         var target = body.Instructions[ len - 1 ];
         WriteLine( $"Found {len} IL instructions in {Injector.HOOK_METHOD}." );
         if ( target.OpCode.Code.Equals( Code.Ret ) ) {
            WriteLine( $"Injecting before last op {target}" );
            body.GetILProcessor().InsertBefore( target, Instruction.Create( OpCodes.Call, game.ImportReference( injectedMethod ) ) );
            return true;
         }

         WriteLine( $"Injection mark not found. Found {target.OpCode} instead." );
         return false;
      }

      private bool SaveAssembly ( ModuleDefinition game ) {
         WriteLine( $"Writing back to {Path.GetFileName( Target )}..." );
         game.Write();
         WriteLine( "Injection complete!" );
         return true;
      }

      internal string ReadVersion () {
         using ( var dll = ModuleDefinition.ReadModule( Target ) ) {
            foreach ( var type in dll.Types ) {
               if ( type.FullName == Injector.GAME_VERSION_TYPE )
                  return FindGameVersion( type );
            }
         }
         return "ERR ver type not found";
      }

      private static string FindGameVersion ( TypeDefinition type ) {
         var method = type.Methods.FirstOrDefault( e => e.Name == Injector.GAME_VERSION_METHOD );
         if ( method == null || ! method.HasBody ) return "ERR ver method not found";

         try {
            int[] version = new int[2];
            int ldcCount = 0;
            foreach ( var code in method.Body.Instructions ) {
               string op = code.OpCode.ToString();
               if ( ! op.StartsWith( "ldc.i4" ) ) continue;
               if ( ldcCount >= 2 ) return "ERR too many vers";

               int ver = 0;
               if ( code.Operand is int num ) ver = num;
               else if ( code.Operand is sbyte num2 ) ver = num2;
               else if ( code.OpCode.Code.Equals( Code.Ldc_I4_M1 ) ) ver = -1;
               else ver = int.Parse( op.Substring( 7 ) );

               version[ ldcCount ] = ver;
               ++ldcCount;
            }
            if ( ldcCount < 2 ) return "ERR too few vers";
            return version[0].ToString() + '.' + version[1];
         } catch ( Exception e ) {
            return $"ERR {e}";
         }
      }
   }
}