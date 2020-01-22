using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Console;

namespace ModnixPoint {
   internal static class Injector {
      // return codes
      private const int RC_NORMAL = 0;
      private const int RC_UNHANDLED_STATE = 1;
      private const int RC_BAD_OPTIONS = 2;
      private const int RC_MISSING_BACKUP_FILE = 3;
      private const int RC_BACKUP_FILE_INJECTED = 4;
      private const int RC_BAD_MANAGED_DIRECTORY_PROVIDED = 5;
      private const int RC_MISSING_MOD_LOADER_ASSEMBLY = 6;
      private const int RC_REQUIRED_GAME_VERSION_MISMATCH = 7;

      private const string MOD_LOADER_NAME = "Modnix Point";
      private const string MOD_INJECTOR_EXE_FILE_NAME = "ModnixPointInjector.exe";
      private const string MOD_LOADER_DLL_FILE_NAME = "ModnixPoint.dll";
      private const string GAME_DLL_FILE_NAME = "Assembly-CSharp.dll";
      private const string BACKUP_FILE_EXT = ".orig";

      private const string HOOK_TYPE     = "PhoenixPoint.Common.Game.PhoenixGame";
      private const string HOOK_METHOD   = "BootCrt";
      private const string INJECT_TYPE   = "ModnixPoint.ModLoader";
      private const string INJECT_METHOD = "Init";
      private const string INJECT_CALL   = "MenuCrt";

      private const string GAME_VERSION_TYPE  = "VersionInfo";
      private const string GAME_VERSION_CONST = "CURRENT_VERSION_NUMBER";

      private static readonly ReceivedOptions OptionsIn = new ReceivedOptions();

      private static readonly OptionSet Options = new OptionSet {
         {
            "d|detect", "Detect if the game assembly is already injected",
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
            "manageddir=", "specify managed dir where game's Assembly-CSharp.dll is located",
            v => OptionsIn.ManagedDir = v
         },
         {
            "y|nokeypress", "Anwser prompts affirmatively",
            v => OptionsIn.RequireKeyPress = v == null
         },
         {
            "reqmismatchmsg=", "Print msg if required version check fails",
            v => OptionsIn.RequiredGameVersionMismatchMessage = v
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
            if ( ParseOptionsTestAbort( args, out int returnCode ) ) return returnCode;

            string managedDirectory = null;
            if ( ! string.IsNullOrEmpty( OptionsIn.ManagedDir ) ) {
               if ( ! Directory.Exists( OptionsIn.ManagedDir ) )
                  return SayManagedDirMissingError( OptionsIn.ManagedDir );
               managedDirectory = Path.GetFullPath( OptionsIn.ManagedDir );
            } else
               managedDirectory = Directory.GetCurrentDirectory();

            var gameDllPath       = Path.Combine( managedDirectory, GAME_DLL_FILE_NAME );
            var gameDllBackupPath = Path.Combine( managedDirectory, GAME_DLL_FILE_NAME + BACKUP_FILE_EXT );
            var modLoaderDllPath  = Path.Combine( managedDirectory, MOD_LOADER_DLL_FILE_NAME );

            if ( ! File.Exists( gameDllPath ) )
               return SayGameAssemblyMissingError( OptionsIn.ManagedDir );

            if ( ! File.Exists( modLoaderDllPath ) )
               return SayModLoaderAssemblyMissingError( modLoaderDllPath );

            var injected = IsInjected( gameDllPath, out var isCurrentInjection, out var gameVersion );

            if ( OptionsIn.GameVersion )
               return SayGameVersion( gameVersion );

            if ( ! string.IsNullOrEmpty( OptionsIn.RequiredGameVersion ) && OptionsIn.RequiredGameVersion != gameVersion ) {
               SayRequiredGameVersion( gameVersion, OptionsIn.RequiredGameVersion );
               SayRequiredGameVersionMismatchMessage( OptionsIn.RequiredGameVersionMismatchMessage );
               return PromptForKey( OptionsIn.RequireKeyPress, RC_REQUIRED_GAME_VERSION_MISMATCH );
            }

            if ( OptionsIn.Detecting )
               return SayInjectedStatus( injected );

            SayHeader();

            if ( OptionsIn.Restoring ) {
               if ( injected )
                  Restore( gameDllPath, gameDllBackupPath );
               else
                  SayAlreadyRestored();
               return PromptForKey( OptionsIn.RequireKeyPress );
            }

            if ( OptionsIn.Installing ) {
               if ( ! injected ) {
                  Backup( gameDllPath, gameDllBackupPath );
                  Inject( gameDllPath, modLoaderDllPath );
               } else {
                  SayAlreadyInjected( isCurrentInjection );
               }
               return PromptForKey( OptionsIn.RequireKeyPress );
            }

         } catch ( BackupFileError e ) {
            SayException( e );
            return SayHowToRecoverMissingBackup( e.BackupFileName );

         } catch ( Exception e ) {
            SayException( e );
         }

         return RC_UNHANDLED_STATE;
      }

      private static bool ParseOptionsTestAbort ( string[] args, out int returnCode ) {
         returnCode = RC_UNHANDLED_STATE;

         try {
            Options.Parse( args );
         } catch ( OptionException e ) {
            returnCode = SayOptionException( e );
            return true;
         }

         if ( OptionsIn.Helping ) {
            returnCode = SayHelp( Options );
            return true;
         }

         if ( OptionsIn.Versioning ) {
            returnCode = SayVersion();
            return true;
         }

         return false;
      }

      private static int SayInjectedStatus ( bool injected ) {
         WriteLine( injected.ToString().ToLower() );
         return RC_NORMAL;
      }

      private static void Backup ( string filePath, string backupFilePath ) {
         File.Copy( filePath, backupFilePath, true );
         WriteLine( $"{Path.GetFileName( filePath )} backed up to {Path.GetFileName( backupFilePath )}" );
      }

      private static void Restore ( string filePath, string backupFilePath ) {
         if ( ! File.Exists( backupFilePath ) )
            throw new BackupFileNotFound();

         if ( IsInjected( backupFilePath ) )
            throw new BackupFileInjected();

         File.Copy( backupFilePath, filePath, true );
         WriteLine( $"{Path.GetFileName( backupFilePath )} restored to {Path.GetFileName( filePath )}" );
      }

      private static void Inject ( string hookFilePath, string injectFilePath ) {
         WriteLine( $"Injecting {Path.GetFileName( hookFilePath )} with {INJECT_TYPE}.{INJECT_METHOD} at {HOOK_TYPE}.{HOOK_METHOD}" );

         using ( var game = ModuleDefinition.ReadModule( hookFilePath, new ReaderParameters { ReadWrite = true } ) )
         using ( var injecting = ModuleDefinition.ReadModule( injectFilePath ) ) {
            var success = InjectModHookPoint(game, injecting);

            if ( success )
               success &= WriteNewAssembly( hookFilePath, game );

            if ( !success )
               WriteLine( "Failed to inject the game assembly." );
         }
      }

      private static bool WriteNewAssembly ( string hookFilePath, ModuleDefinition game ) {
         // save the modified assembly
         WriteLine( $"Writing back to {Path.GetFileName( hookFilePath )}..." );
         game.Write();
         WriteLine( "Injection complete!" );
         return true;
      }

      private static bool InjectModHookPoint ( ModuleDefinition game, ModuleDefinition injecting ) {
         // get the methods that we're hooking and injecting
         var injectedMethod = injecting.GetType(INJECT_TYPE).Methods.Single(x => x.Name == INJECT_METHOD);
         var hookedMethod = game.GetType(HOOK_TYPE).Methods.First(x => x.Name == HOOK_METHOD);

         // If the return type is an iterator -- need to go searching for its MoveNext method which contains the actual code you'll want to inject
         if ( hookedMethod.ReturnType.Name.Contains( "IEnumerator" ) ) {
            var nestedIterator = game.GetType(HOOK_TYPE).NestedTypes.First(x => x.Name.Contains(HOOK_METHOD));
            hookedMethod = nestedIterator.Methods.First( x => x.Name.Equals( "MoveNext" ) );
         }

         // As of Phoenix Point v1.0.54973 the BootCrt() iterator method of PhoenixGame has this at the end
         //
         //  ...
         //
         //    IEnumerator<NextUpdate> coroutine = this.MenuCrt(null, MenuEnterReason.None);
         //    Func<IUpdateable, Exception, UpdateableExceptionAction> catchException = null;
         //    ModLoader.Init();
         //    yield return timing.Call(coroutine, catchException);
         //    yield break;
         //
         //  }
         //
         // We want to inject after the MenuCrt call -- so search for that call in the CIL

         var targetInstruction = -1;
         WriteLine( $"Scanning for {INJECT_CALL} call in {HOOK_METHOD}." );

         WriteLine( $"Total {hookedMethod.Body.Instructions.Count} IL instructions." );
         for ( var i = hookedMethod.Body.Instructions.Count - 1 ; i >= 0 ; i-- ) {
            var instruction = hookedMethod.Body.Instructions[i];

            if ( instruction.OpCode.Code.Equals( Code.Call ) && instruction.OpCode.OperandType.Equals( OperandType.InlineMethod ) ) {
               var methodReference = instruction.Operand as MethodReference;
               if ( methodReference != null && methodReference.Name.Contains( INJECT_CALL ) ) {
                  WriteLine( $"CALL {methodReference.Name}" );
                  targetInstruction = i + 1; // hack - we want to run after that instruction has been fully processed, not in the middle of it.
                  break;
               }
            }

         }

         if ( targetInstruction < 0 ) {
            WriteLine( "Call not found." );
            return false;
         }

         WriteLine( "Call found. Inserting mod loader after call." );
         hookedMethod.Body.GetILProcessor().InsertAfter( hookedMethod.Body.Instructions[ targetInstruction ],
             Instruction.Create( OpCodes.Call, game.ImportReference( injectedMethod ) ) );
         WriteLine( "Insertion done." );

         return true;
      }

      private static bool IsInjected ( string dllPath ) => IsInjected( dllPath, out _, out _ );

      private static bool IsInjected ( string dllPath, out bool isCurrentInjection, out string gameVersion ) {
         isCurrentInjection = false;
         gameVersion = "";
         var detectedInject = false;
         using ( var dll = ModuleDefinition.ReadModule( dllPath ) ) {
            foreach ( var type in dll.Types ) {
               // Standard methods
               foreach ( var methodDefinition in type.Methods ) {
                  if ( IsHookInstalled( methodDefinition, out isCurrentInjection ) )
                     detectedInject = true;
               }

               // Also have to check in places like IEnumerator generated methods (Nested)
               foreach ( var nestedType in type.NestedTypes )
                  foreach ( var methodDefinition in nestedType.Methods ) {
                     if ( IsHookInstalled( methodDefinition, out isCurrentInjection ) )
                        detectedInject = true;
                  }

               if ( type.FullName == GAME_VERSION_TYPE ) {
                  var fieldInfo = type.Fields.First(x => x.IsLiteral && !x.IsInitOnly && x.Name == GAME_VERSION_CONST);
                  if ( null != fieldInfo )
                     gameVersion = fieldInfo.Constant.ToString();
               }

               if ( detectedInject && !string.IsNullOrEmpty( gameVersion ) )
                  return true;
            }
         }

         return detectedInject;
      }

      private static bool IsHookInstalled ( MethodDefinition methodDefinition, out bool isCurrentInjection ) {
         isCurrentInjection = false;

         if ( methodDefinition.Body == null )
            return false;

         foreach ( var instruction in methodDefinition.Body.Instructions ) {
            if ( instruction.OpCode.Equals( OpCodes.Call ) &&
                instruction.Operand.ToString().Equals( $"System.Void {INJECT_TYPE}::{INJECT_METHOD}()" ) ) {
               isCurrentInjection =
                   methodDefinition.FullName.Contains( HOOK_TYPE ) &&
                   methodDefinition.FullName.Contains( HOOK_METHOD );
               return true;
            }
         }

         return false;
      }

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
         WriteLine( $"ERROR: We could not find the game assembly {GAME_DLL_FILE_NAME} in directory '{givenManagedDir}'.\n" +
             "Are you sure that is the correct directory?" );
         return RC_BAD_MANAGED_DIRECTORY_PROVIDED;
      }

      private static int SayModLoaderAssemblyMissingError ( string expectedModLoaderAssemblyPath ) {
         SayHeader();
         WriteLine( $"ERROR: We could not find the loader assembly {MOD_LOADER_DLL_FILE_NAME} at '{expectedModLoaderAssemblyPath}'.\n" +
             $"Is {MOD_LOADER_DLL_FILE_NAME} in the correct place? It should be in the same directory as this injector executable." );
         return RC_MISSING_MOD_LOADER_ASSEMBLY;
      }

      private static int SayGameVersion ( string version ) {
         WriteLine( version );
         return RC_NORMAL;
      }

      private static void SayRequiredGameVersion ( string version, string expectedVersion ) {
         WriteLine( $"Expected game v{expectedVersion}" );
         WriteLine( $"Actual game v{version}" );
      }

      private static void SayRequiredGameVersionMismatchMessage ( string msg ) {
         if ( !string.IsNullOrEmpty( msg ) )
            WriteLine( msg );
      }

      private static string GetProductVersion () {
         var assembly = Assembly.GetExecutingAssembly();
         var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
         return fvi.FileVersion;
      }

      private static void SayHeader () {
         WriteLine( $"{MOD_LOADER_NAME} Injector" );
         WriteLine( "----------------------------" );
      }

      private static int SayHowToRecoverMissingBackup ( string backupFileName ) {
         WriteLine( "----------------------------" );
         WriteLine( $"The backup game assembly file must be in the directory with the injector for /restore to work. The backup file should be named \"{backupFileName}\"." );
         WriteLine( "You may need to reinstall or use Steam/GOG's file verification function if you have no other backup." );
         return RC_MISSING_BACKUP_FILE;
      }

      private static void SayHowToRecoverInjectedBackup ( string backupFileName ) {
         WriteLine( "----------------------------" );
         WriteLine( $"The backup game assembly file named \"{backupFileName}\" was already injected. Something has gone wrong." );
         WriteLine( "You may need to reinstall or use Steam/GOG's file verification function if you have no other backup." );
      }

      private static void SayAlreadyInjected ( bool isCurrentInjection ) => WriteLine( isCurrentInjection
              ? $"ERROR: {GAME_DLL_FILE_NAME} already injected at {INJECT_TYPE}.{INJECT_METHOD}."
              : $"ERROR: {GAME_DLL_FILE_NAME} already injected with an older injector.  Please revert the file and re-run injector!" );

      private static void SayAlreadyRestored () => WriteLine( $"{GAME_DLL_FILE_NAME} already restored." );

      private static void SayException ( Exception e ) => WriteLine( $"ERROR: An exception occured: {e}" );

      private static int PromptForKey ( bool requireKeyPress, int returnCode = RC_NORMAL ) {
         if ( requireKeyPress ) {
            WriteLine( "Press any key to continue." );
            ReadKey();
         }
         return returnCode;
      }
   }

   public class BackupFileError : Exception {
      public BackupFileError ( string backupFileName, string message ) : base( message ) { BackupFileName = backupFileName; }
      public readonly string BackupFileName;
   }

   public class BackupFileInjected : BackupFileError {
      public BackupFileInjected ( string backupFileName = "Assembly-CSharp.dll.orig" ) :
         base( backupFileName, $"The backup file \"{backupFileName}\" was injected." ) { }
   }

   public class BackupFileNotFound : BackupFileError {
      public BackupFileNotFound ( string backupFileName = "Assembly-CSharp.dll.orig" ) :
         base( backupFileName, $"The backup file \"{backupFileName}\" could not be found." ) { }
   }

   // Values passed by the user into the program via command line.
   internal class ReceivedOptions {
      public bool RequireKeyPress = true;
      public bool Detecting = false;
      public string RequiredGameVersion = string.Empty;
      public string RequiredGameVersionMismatchMessage = string.Empty;
      public string ManagedDir = string.Empty;
      public bool GameVersion = false;
      public bool Helping = false;
      public bool Installing = true;
      public bool Restoring = false;
      public bool Versioning = false;
   }
}