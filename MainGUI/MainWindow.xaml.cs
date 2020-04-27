using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   public partial class MainWindow : Window, IAppGui {
      private readonly AppControl App = AppControl.Instance;
      private readonly object SynGetSet = new object();

      private event Action ModListChanged;
      private Timer GameStatusTimer;

      public MainWindow () { try {
         InitializeComponent();
         GameStatusTimer = new Timer( CheckGameRunning, null, Timeout.Infinite, Timeout.Infinite );
         SetupGUI();
         RefreshGUI();
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void Window_SourceInitialized ( object sender, EventArgs e ) {
         if ( App.ParamSkipStartupCheck ) return;
         SetCollapseState();
         var settings =  App.Settings;
         if ( settings.WindowLeft >= 0 ) Left = settings.WindowLeft;
         if ( settings.WindowTop >= 0  ) Top = settings.WindowTop;
         if ( settings.WindowWidth >= 0 ) Width = settings.WindowWidth;
         if ( settings.WindowHeight >= 0 ) Height = settings.WindowHeight;
         if ( settings.MaximiseWindow ) WindowState = WindowState.Maximized;
      }

      private void Window_Closing ( object sender, System.ComponentModel.CancelEventArgs evt ) {
         if ( AbortByCheckSave() ) evt.Cancel = true;
      }

      private void Window_Closed ( object sender, EventArgs e ) {
         GameStatusTimer.Change( Timeout.Infinite, Timeout.Infinite );
         GameStatusTimer.Dispose();
         var settings =  App.Settings;
         if ( WindowState == WindowState.Maximized ) {
            settings.WindowLeft = RestoreBounds.Left;
            settings.WindowTop = RestoreBounds.Top;
            settings.WindowWidth = RestoreBounds.Width;
            settings.WindowHeight = RestoreBounds.Height;
            settings.MaximiseWindow = true;
         } else {
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.MaximiseWindow = false;
         }
         App.SaveSettings();
      }

      private void SetupGUI () {
         Log( "Setting up GUI" );
         SharedGui.AppStateChanged += this.Dispatcher( RefreshAppInfo );
         SharedGui.AppStateChanged += this.Dispatcher( RefreshAppButtons );
         SharedGui.GamePathChanged += this.Dispatcher( RefreshGameInfo );
         SharedGui.VersionChanged  += this.Dispatcher( RefreshAppInfo );
         SharedGui.VersionChanged  += this.Dispatcher( RefreshGameInfo );
         SharedGui.AppWorkingChanged  += this.Dispatcher( RefreshAppInfo );
         SharedGui.AppWorkingChanged  += this.Dispatcher( RefreshAppButtons );
         SharedGui.GameRunningChanged += this.Dispatcher( RefreshAppButtons );
         ModListChanged += this.Dispatcher( RefreshModList );
         ModListChanged += this.Dispatcher( RefreshAppButtons );
      }

      private void RefreshGUI () { try {
         Log( "Refreshing GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModInfo();
         RefreshAppButtons();
         RefreshUpdateStatus();
      } catch ( Exception ex ) { Log( ex ); } }

      public void SetInfo ( GuiInfo info, object value ) { this.Dispatch( () => { try {
         Log( $"Set {info} = {value}" );
         var txt = value?.ToString();
         switch ( info ) {
            case GuiInfo.VISIBILITY : ShowWindow(); break;
            case GuiInfo.APP_UPDATE : Update = value; UpdateChecked(); RefreshUpdateStatus(); break;
            case GuiInfo.MOD        : SetSelectedMod( ListedMods.First( e => e.Path.Equals( value?.ToString(), StringComparison.Ordinal ) ) ); break;
            case GuiInfo.MOD_LIST   : SetModList( value as IEnumerable<ModInfo> ); break;
            default : SharedGui.SetInfo( info, value ); break;
         }
      } catch ( Exception ex ) { Log( ex ); } } ); }

      private void Window_Activated ( object sender, EventArgs e ) => GameStatusTimer.Change( 100, 3000 );
      private void Window_Deactivated ( object sender, EventArgs e ) => GameStatusTimer.Change( Timeout.Infinite, Timeout.Infinite );

      private void ShowWindow () {
         Log( "Checking app status" );
         App.CheckStatusTask( true );
         if ( ! App.ParamSkipStartupCheck )
            CheckUpdate( false );
         Show();
         // WPF bug - document reset its padding on first render.
         var empty = new Thickness( 0 );
         RichAppInfo.Document.PagePadding = empty;
         RichGameInfo.Document.PagePadding = empty;
         RichModInfo.Document.PagePadding = empty;
      }

      private void CheckGameRunning ( object _ = null ) {
         var IsRunning = AppControl.IsGameRunning();
         SharedGui.IsGameRunning = IsRunning;
         CheckLogRefresh();
      }

      private void SetCollapseState () {
         bool minApp = App.Settings.MinifyLoaderPanel, minGame = App.Settings.MinifyGamePanel;
         ButtonSetup.Visibility = ButtonUserGuide.Visibility = ButtonWiki.Visibility =
            minApp ? Visibility.Collapsed : Visibility.Visible;
         ButtonMinifyLoader.Content = minApp ? "＋" : "—";

         ButtonWebsite.Visibility = ButtonForum.Visibility = ButtonReddit.Visibility =
            ButtonTwitter.Visibility = ButtonCanny.Visibility = ButtonDiscord.Visibility =
            minGame ? Visibility.Collapsed : Visibility.Visible;
         GameButtonGap1.Height = GameButtonGap2.Height = new GridLength( minGame ? 0 : 5, GridUnitType.Pixel );
         ButtonMinifyGame.Content = minGame ? "＋" : "—";
      }

      private void RefreshAppButtons () { try {
         Log( "Refreshing app buttons" );
         ButtonSetup.IsEnabled = ! SharedGui.IsAppWorking && SharedGui.AppState != null;
         ButtonRunOnline.IsEnabled  = ButtonRunOffline.IsEnabled  = SharedGui.CanModify && SharedGui.IsGameFound;
         ButtonRunOnline.Foreground = ButtonRunOffline.Foreground = 
            ButtonRunOnline.IsEnabled && SharedGui.AppState != null && ! SharedGui.IsInjected ? Brushes.Red : Brushes.Black;
         SetCollapseState();

         RefreshConfButtions();

         ButtonAddMod.IsEnabled = SharedGui.CanModify && Directory.Exists( App.ModFolder );
         ButtonModDir.IsEnabled = Directory.Exists( App.ModFolder );
         ButtonRefreshMod.IsEnabled = Directory.Exists( App.ModFolder ) && ! SharedGui.IsAppWorking;

         ButtonModOpenModDir.IsEnabled = CurrentMod != null;
         ButtonModDelete.IsEnabled = SharedGui.CanModify && CurrentMod != null && ! SelectedMods.Any( e => e.Is( ModQuery.IS_CHILD ) );
         var icon = "uncheck";
         if ( IsDisableButton ) {
            icon = "check";
            AccessTextModDisable.Text = "Disable";
            ButtonModDisable.IsEnabled = SelectedMods.Any() && SharedGui.CanModify;
         } else {
            AccessTextModDisable.Text = "Enable";
            ButtonModDisable.IsEnabled = SelectedMods.Any( e => ! e.Is( ModQuery.FORCE_DISABLED ) ) && SharedGui.CanModify;
         }
         IconModDisable.Source = new BitmapImage( new Uri( $"/Resources/img/{icon}.png", UriKind.Relative ) );

         ButtonLoaderLog.IsEnabled = File.Exists( App.LoaderLog );
         ButtonConsoleLog.IsEnabled = File.Exists( App.ConsoleLog );

         if ( SharedGui.IsGameRunning )
            BtnTxtSetup.Text = "Refresh";
         else if ( SharedGui.AppState == "no_game" )
            BtnTxtSetup.Text = "Browse";
         else if ( SharedGui.AppState == "modnix" )
            BtnTxtSetup.Text = "Revert";
         else
            BtnTxtSetup.Text = "Setup";

         LabelModList.Foreground = Brushes.Black;
         if ( SharedGui.IsGameRunning ) {
            LabelModList.Content = "GAME RUNNING";
            LabelModList.Foreground = Brushes.Red;
         } else if ( ModList == null ) {
            LabelModList.Content = "Checking";
         } else if ( SharedGui.AppState != null && ! SharedGui.IsInjected ) {
            LabelModList.Content = "NOT INSTALLED";
            LabelModList.Foreground = Brushes.Red;
         } else {
            int total = ModList.Count(), enabled = ModList.Count( e => e.Is( ModQuery.ENABLED ) );
            if ( total == enabled )
               LabelModList.Content = $"{total} Mods";
            else
               LabelModList.Content = $"{total} Mods, {enabled} On / {total-enabled} Off";
         }
      } catch ( Exception ex ) { Log( ex ); } }

      #region App Info Area
      private void RefreshAppInfo () { try {
         Log( "Refreshing app info" );
         var build = new LoaderSettings().UpdateChannel.Equals( "dev" ) ? " (dev)" : "";
         Title = ( AppControl.LIVE_NAME + " " + SharedGui.AppVer + build ).Trim();
         string txt;
         if ( SharedGui.IsAppWorking || SharedGui.AppState == null )
            txt = "Busy";
         else
            switch ( SharedGui.AppState ) {
               case "ppml"   : txt = "PPML only, need setup"; break;
               case "both"   : txt = "PPML found, can remove"; break;
               case "modnix" : txt = "Injected"; break;
               case "none"   : txt = "Requires Setup"; break;
               case "no_game": txt = "Game not found"; break;
               default: txt = $"Unknown injection state {SharedGui.AppState}"; break;
            }
         var state = new Run( txt );
         if ( SharedGui.AppState != "modnix" ) state.Foreground = Brushes.Red;
         var p = new Paragraph();
         if ( ! App.Settings.MinifyLoaderPanel ) {
            p.Inlines.Add( new Bold( new Run( AppControl.LIVE_NAME ) ) );
            p.Inlines.Add( $"\tVer {SharedGui.AppVer}{build}\r" );
         }
         p.Inlines.Add( "Status: " );
         p.Inlines.Add( state );
         RichAppInfo.Document.Replace( p );
         CheckLogVerbo.IsChecked = ( App.Settings.LogLevel & SourceLevels.Verbose ) == SourceLevels.Verbose;
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonHideLoader_Click ( object sender, RoutedEventArgs e ) {
         App.Settings.MinifyLoaderPanel = ! App.Settings.MinifyLoaderPanel;
         RefreshAppInfo();
         RefreshAppButtons();
      }

      private void ButtonWiki_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "wiki", e );
      private void ButtonGitHub_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "home", e );
      private void ButtonUserGuide_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "guide", e );

      private void ButtonSetup_Click ( object sender, RoutedEventArgs e ) { try {
         Log( "Main action button clicked" );
         if ( e?.Source is UIElement src ) src.Focus();
         if ( SharedGui.IsGameRunning ) { // Refresh
            CheckGameRunning();
            return;
         }
         switch ( SharedGui.AppState ) {
            case "ppml" : case "both" : case "none" :
               DoSetup();
               break;
            case "modnix" :
               if ( MessageBox.Show( "Remove Modnix from Phoenix Point?", "Revert", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No ) == MessageBoxResult.Yes )
                  DoRestore();
               break;
            case "no_game" :
               if ( SharedGui.BrowseGame() ) {
                  App.SaveSettings();
                  App.CheckStatusTask( false );
               }
               break;
            default:
               MessageBox.Show( $"Unknown injection state {SharedGui.AppState}", "Error", MessageBoxButton.OK, MessageBoxImage.Error );
               break;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void DoSetup () {
         Log( "Calling setup" );
         SharedGui.IsAppWorking = true;
         App.DoSetupTask();
      }

      private void DoRestore () {
         Log( "Calling restore" );
         SharedGui.IsAppWorking = true;
         App.DoRestoreTask();
      }

      private void ButtonModDir_Click ( object sender, RoutedEventArgs e ) { try {
         var arg = $"/select, \"{Path.Combine( App.ModFolder, App.ModGuiExe )}\"";
         Log( $"Launching explorer.exe {arg}" );
         Process.Start( "explorer.exe", arg );
      } catch ( Exception ex ) { Log( ex ); } }

      public void Prompt ( AppAction action, PromptFlag flags = PromptFlag.NONE, Exception ex = null ) { this.Dispatch( () => { try {
         Log( $"Prompt {action} {flags}" );
         SharedGui.Prompt( action, flags, ex, () => AppControl.Explore( App.ModGuiExe ) );
         if ( action == AppAction.ADD_MOD || action == AppAction.DEL_MOD )
            ModListChanged.Invoke();
      } catch ( Exception err ) { Log( err ); } } ); }

      private void ButtonNexus_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "nexus", e );
      #endregion

      #region Game Info Area
      private void RefreshGameInfo () { try {
         Log( "Refreshing game info" );
         var p = new Paragraph( new Bold( new Run( "Phoenix Point" ) ) );
         if ( SharedGui.IsGameFound ) {
            if ( SharedGui.GameVer != null )
               p.Inlines.Add( $"\tVer {SharedGui.GameVer}" );
            if ( ! App.Settings.MinifyGamePanel ) {
               var txt = new Run( Path.GetFullPath( SharedGui.GamePath ) ){ Foreground = Brushes.Blue };
               WpfHelper.Linkify( txt, () => AppControl.Explore( Path.Combine( Path.GetFullPath( SharedGui.GamePath ), AppControl.GAME_EXE ) ) );
               p.Inlines.Add( "\r" );
               p.Inlines.Add( txt );
            }
         } else
            p.Inlines.Add( new Run( "\rGame not found" ){ Foreground = Brushes.Red } );
         RichGameInfo.Document.Replace( p );
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonHideGame_Click ( object sender, RoutedEventArgs e ) {
         App.Settings.MinifyGamePanel = ! App.Settings.MinifyGamePanel;
         RefreshGameInfo();
         RefreshAppButtons();
      }

      private void ButtonOnline_Click  ( object sender, RoutedEventArgs e ) {
         if ( AbortByCheckSave() ) return;
         App.LaunchGame( "online" );
         SetInfo( GuiInfo.GAME_RUNNING, true );
         GameStatusTimer.Change( Timeout.Infinite, 10_000 ); // Should be overrode by activate/deactivate, but just in case
      }

      private void ButtonOffline_Click ( object sender, RoutedEventArgs e ) {
         if ( AbortByCheckSave() ) return;
         App.LaunchGame( "offline" );
         SetInfo( GuiInfo.GAME_RUNNING, true );
         GameStatusTimer.Change( Timeout.Infinite, 10_000 ); // Should be overrode by activate/deactivate, but just in case
      }

      private void ButtonCanny_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "canny", e );
      private void ButtonDiscord_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "discord", e );
      private void ButtonForum_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "forum", e );
      private void ButtonManual_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "manual", e );
      private void ButtonReddit_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "reddit", e );
      private void ButtonTwitter_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "twitter", e );
      private void ButtonWebsite_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "www", e );
      #endregion

      #region Mod List
      private IEnumerable<ModInfo> ModList;
      private IEnumerable<ModInfo> ListedMods => GridModList.ItemsSource.OfType<ModInfo>();
      private IEnumerable<ModInfo> SelectedMods => GridModList.SelectedItems.OfType<ModInfo>();
      private HashSet<string> SelectMods;
      private TabItem SelectTab;

      private void SetModList ( IEnumerable<ModInfo> list ) {
         ModList = list;
         RefreshModList();
         RefreshAppButtons();
      }

      private void RefreshModList () { try {
         Log( "Refreshing mod list" );
         lock ( SynGetSet ) if ( SelectMods == null && SelectedMods.Any() ) {
            SelectMods = new HashSet<string>( SelectedMods.Select( e => e.Path ) );
            SelectTab = TabSetModInfo.SelectedItem as TabItem;
         }
         if ( GridModList.ItemsSource != ModList ) {
            Log( "New mod list" );
            GridModList.ItemsSource = ModList;
         }
         GridModList.Items?.Refresh();
         GridModList.UpdateLayout();
         if ( ModList != null ) {
            if ( SelectMods != null && SelectMods.Count > 0 ) {
               foreach ( var mod in ModList.OfType<ModInfo>() ) {
                  if ( SelectMods.Contains( mod.Path ) ) {
                     GridModList.SelectedItem = mod;
                     GridModList.ScrollIntoView( mod );
                     if ( SelectTab != null ) TabSetModInfo.SelectedItem = SelectTab;
                     break;
                  }
               }
            }
            SelectMods = null;
            SelectTab = null;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void SetSelectedMod ( ModInfo mod ) {
         CurrentMod = mod;
         if ( GridModList.SelectedItem == mod ) {
            RefreshModInfo();
            RefreshAppButtons();
         } else
            GridModList.SelectedItem = mod;
      }

      private void ButtonAddMod_Click ( object sender, RoutedEventArgs evt ) {
         if ( AbortByCheckSave() ) return;
         var dialog = new Microsoft.Win32.OpenFileDialog {
            DefaultExt = "*.7z;*.bz2;*.dll;*.js;*.gz;*.xz;*.zip",
            Filter = "All Mods|*.7z;*.bz2;*.dll;*.js;*.gz;*.xz;*.zip|"+
                     "Mod Packages (.7z,.zip)|*.7z;*.xz;*.zip|"+
                     "Compressed Mods (.bz2,.gz)|*.bz2;*.gz|"+
                     "Single File Mods (.dll,.js)|*.dll;*.js|"+
                     "All Files|*.*",
            Multiselect = true,
            Title = "Add Mod",
         };
         if ( ! dialog.ShowDialog().GetValueOrDefault() || dialog.FileNames.Length == 0 ) return;
         if ( dialog.FileNames.Any( e => e.StartsWith( App.ModFolder ) ) ) {
            MessageBox.Show( "Add Mod failed.\rFile is already in mod folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
            return;
         }
         if ( App.CurrentGame != null && dialog.FileNames.Any( e => e.StartsWith( App.CurrentGame.GameDir ) ) ) {
            MessageBox.Show( "Add Mod failed.\rFile is in game folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
            return;
         }
         SharedGui.IsAppWorking = true;
         App.AddModTask( dialog.FileNames ).ContinueWith( task => {
            if ( task.IsFaulted ) Prompt( AppAction.ADD_MOD, PromptFlag.ERROR, task.Exception );
            lock ( SynGetSet ) {
               SelectMods = new HashSet<string>( task.Result.SelectMany( e => e ) );
               SelectTab = null;
            }
            SharedGui.IsAppWorking = false;
            this.Dispatch( () => ButtonRefreshMod_Click() );
         } );
      }

      private async void ButtonRefreshMod_Click ( object sender = null, RoutedEventArgs evt = null ) {
         if ( AbortByCheckSave() ) return;
         SetModList( null );
         if ( SharedGui.IsGameRunning ) CheckGameRunning();
         // Add new mod can happens without visible refresh, and if the mod is not new it'd look like Modnix did nothing. So we need a delay.
         await Task.Delay( 100 ).ConfigureAwait( false );
         App.GetModList();
      }

      private void GridModList_SelectionChanged ( object sender, SelectionChangedEventArgs evt ) {
         Log( $"Selection changed to {GridModList.SelectedItem}, total {GridModList.SelectedItems.Count}" );
         SetSelectedMod( GridModList.SelectedItem as ModInfo );
      }

      private void GridModList_LoadingRow ( object sender, DataGridRowEventArgs e ) {
         var row = e.Row;
         var mod = row.Item as ModInfo;
         if ( mod == null || ! mod.Is( ModQuery.ENABLED ) )
            row.Foreground = Brushes.Gray;
         else if ( mod.Is( ModQuery.ERROR ) )
            row.Foreground = Brushes.OrangeRed;
         else if ( mod.Is( ModQuery.WARNING ) )
            row.Foreground = Brushes.Blue;
         else
            row.Foreground = Brushes.Black;
      }

      private void GridModList_PreviewKeyDown ( object sender, KeyEventArgs e ) {
         switch ( e.Key ) {
            case Key.Insert :
               ButtonAddMod_Click( sender, e ); break;
            case Key.Delete :
               ButtonModDelete_Click( sender, e ); break;
            case Key.Home :
               GridModList.SelectedIndex = 0;
               GridModList.ScrollIntoView( GridModList.SelectedItem );
               break;
            case Key.End :
               GridModList.SelectedIndex = ModList.Count() - 1;
               GridModList.ScrollIntoView( GridModList.SelectedItem );
               break;
            default:
               return;
         }
         e.Handled = true;
      }
      #endregion

      #region Mod Info Area
      private ModInfo CurrentMod;
      private bool UpdatingModInfo;
      private bool IsConfEmpty => RichModInfo.Document.TextRange().Text.Length == 0;
      private bool IsDisableButton => SelectedMods.Any( e => e.Is( ModQuery.ENABLED ) ) || ! SelectedMods.Any();

      private void RefreshModInfo () {
         try {
            UpdatingModInfo = true;
            HideModTabs();
            if ( GridModList.SelectedItems.Count > 1 ) {
               Log( $"Showing mods summary" );
               BkgdModeInfo.Opacity = 0.06;
               if ( TabSetModInfo.SelectedItem != TabModInfo ) TabSetModInfo.SelectedItem = TabModInfo;
               BuildMultiModInfo();
               return;
            }
            if ( CurrentMod == null ) {
               Log( "Clearing mod info" );
               RichModInfo.Document.Replace();
               BkgdModeInfo.Opacity = 0.5;
            } else {
               Log( $"Refreshing mod {CurrentMod}" );
               BkgdModeInfo.Opacity = 0.03;

               TabModConfig.Visibility  = CurrentMod.Is( ModQuery.HAS_CONFIG ) ? Visibility.Visible : Visibility.Collapsed;
               TabModReadme.Visibility  = CurrentMod.Is( ModQuery.HAS_README ) ? Visibility.Visible : Visibility.Collapsed;
               TabModChange.Visibility  = CurrentMod.Is( ModQuery.HAS_CHANGELOG ) ? Visibility.Visible : Visibility.Collapsed;
               TabModLicense.Visibility = CurrentMod.Is( ModQuery.HAS_LICENSE ) ? Visibility.Visible : Visibility.Collapsed;
            }

            if ( ( TabSetModInfo.SelectedItem as UIElement )?.Visibility != Visibility.Visible )
               TabSetModInfo.SelectedItem = TabModInfo;
            var isConfig = TabSetModInfo.SelectedItem == TabModConfig;
            RichModInfo.IsReadOnly = ! isConfig;
            RichModInfo.FontFamily = new FontFamily( isConfig ? "Consolas" : "Segoe UI" );
            PanelConfAction.Visibility = isConfig ? Visibility.Visible : Visibility.Collapsed;

            if ( CurrentMod != null ) {
               if ( TabSetModInfo.SelectedItem == TabModConfig ) {
                  CurrentMod.BuildDocument( ModDoc.CONFIG, RichModInfo.Document );
                  RefreshConfButtions();
               } else if ( TabSetModInfo.SelectedItem == TabModReadme )
                  CurrentMod.BuildDocument( ModDoc.README, RichModInfo.Document );
               else if ( TabSetModInfo.SelectedItem == TabModChange )
                  CurrentMod.BuildDocument( ModDoc.CHANGELOG, RichModInfo.Document );
               else if ( TabSetModInfo.SelectedItem == TabModLicense )
                  CurrentMod.BuildDocument( ModDoc.LICENSE, RichModInfo.Document );
               else
                  CurrentMod.BuildDocument( ModDoc.INFO, RichModInfo.Document );
            }
         } catch ( Exception ex ) {
            Log( ex );
         } finally {
            UpdatingModInfo = false;
         }
      }

      private CancellationTokenSource ConfValidation;

      private void RefreshConfButtions () {
         if ( CurrentMod == null ) return;
         ButtonConfReset.IsEnabled = CurrentMod.Is( ModQuery.EDITING );
         ButtonConfSave.IsEnabled = ButtonConfReset.IsEnabled && SharedGui.CanModify;
         string icon = "floppy";
         if ( IsConfEmpty ) {
            icon = "cross";
            AccessTextConfSave.Text = "Delete";
            ButtonConfSave.IsEnabled = CurrentMod.Is( ModQuery.HAS_CONFIG_FILE );
         } else
            AccessTextConfSave.Text = "Save";
         IconConfSave.Source = new BitmapImage( new Uri( $"/Resources/img/{icon}.png", UriKind.Relative ) );
         
         var txt = RichModInfo.Document.TextRange().Text?.Trim();
         LabelConfNotice.Visibility = Visibility.Collapsed;
         if ( txt?.Length >= 2 && ( txt[ 0 ] == '{' || txt[ txt.Length-1 ] == '}' ) ) {
            var cancel = new CancellationTokenSource();
            lock ( SynGetSet ) {
               if ( ConfValidation?.IsCancellationRequested == false ) ConfValidation.Cancel();
               ConfValidation = cancel;
            }
            Task.Run( () => { try {
               if ( cancel.IsCancellationRequested ) return;
               _ = JsonConvert.DeserializeObject( txt );
            } catch ( JsonException ex ) {
               this.Dispatch( () => {
                  if ( cancel.IsCancellationRequested ) return;
                  var pos = ex.Message.IndexOf( ". Path " );
                  LabelConfNotice.Content = pos > 0 ? "Json error." + ex.Message.Substring( pos + 1 ) : "Not valid json config.";
                  LabelConfNotice.Visibility = Visibility.Visible;
               } );
            } } );
         }
      }

      private void HideModTabs () => TabModConfig.Visibility = TabModReadme.Visibility = TabModChange.Visibility = TabModLicense.Visibility = Visibility.Collapsed;

      private void BuildMultiModInfo () { try {
         var doc = RichModInfo.Document;
         var body = new Paragraph();
         doc.Replace( body );
         foreach ( var mod in SelectedMods )
            mod.BuildDocument( ModDoc.SUMMARY, doc );
         body.Inlines.Add( $"\rTotal {GridModList.SelectedItems.Count} mods" );
      } catch ( Exception ex ) { Log( ex ); } }

      private void TabSetModInfo_SelectionChanged ( object sender, SelectionChangedEventArgs e ) {
         foreach ( var tab in TabSetModInfo.Items )
            if ( tab is TabItem t )
               t.Content = null;
         if ( TabSetModInfo.SelectedItem is TabItem current )
            current.Content = PanelModDocs;
         RefreshModInfo();
      }

      private void RichModInfo_TextChanged ( object sender, TextChangedEventArgs e ) {
         if ( UpdatingModInfo ) return;
         if ( TabSetModInfo.SelectedItem == null || TabSetModInfo.SelectedItem != TabModConfig ) return;
         CurrentMod?.Do( AppAction.EDIT_CONFIG, RichModInfo.Document.TextRange().Text );
         RefreshConfButtions();
      }

      private void RichModInfo_PreviewKeyDown ( object sender, KeyEventArgs e ) { try {
         if ( RichModInfo.IsReadOnly ) return;
         if ( e.Key == Key.S && ( e.KeyboardDevice.IsKeyDown( Key.LeftCtrl ) || e.KeyboardDevice.IsKeyDown( Key.RightCtrl ) ) ) {
            ButtonConfSave_Click( sender, e );
            e.Handled = true;

         } else if ( e.Key == Key.Enter ) {
            var sel = RichModInfo.Selection;
            new TextRange( sel.Start, sel.End ).Text = "\r";
            RichModInfo.CaretPosition = sel.End.GetNextInsertionPosition( LogicalDirection.Forward );
            e.Handled = true;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      // Fix RichTextBox Hyperlink navigation https://stackoverflow.com/a/54472110/893578
      private void Hyperlink_RequestNavigate ( object sender, RequestNavigateEventArgs  e ) =>
         Process.Start( e.Uri.ToString() );

      private void ButtonConfReset_Click ( object sender, RoutedEventArgs e ) {
         CurrentMod?.Do( AppAction.RESET_CONFIG );
         RefreshModInfo();
      }

      private void ButtonConfProfile_Click ( object sender, RoutedEventArgs e ) {
         CurrentMod?.Do( AppAction.SET_CONFIG_PROFILE, "Default" );
         RefreshModInfo();
      }
      
      private void ButtonConfSave_Click ( object sender, RoutedEventArgs e ) {
         CurrentMod?.Do( AppAction.SAVE_CONFIG );
         if ( IsConfEmpty )
            RefreshModInfo();
         else
            RefreshConfButtions();
      }

      private void ButtonModOpenModDir_Click ( object sender, RoutedEventArgs evt ) {
         var count = GridModList.SelectedItems.Count;
         if ( count > 3 &&
            MessageBoxResult.Yes != MessageBox.Show( $"Open {count} file explorer?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel ) )
            return;
         foreach ( var mod in SelectedMods ) {
            var path = mod.Path;
            if ( string.IsNullOrWhiteSpace( path ) ) return;
            AppControl.Explore( path );
         }
      }

      private void ButtonModDisable_Click ( object sender, RoutedEventArgs evt ) {
         var disable = IsDisableButton;
         foreach ( var mod in SelectedMods )
            mod.Do( disable ? AppAction.DISABLE_MOD : AppAction.ENABLE_MOD );
         App.SaveSettings();
         App.GetModList();
      }

      private void ButtonModDelete_Click ( object sender, RoutedEventArgs evt ) {
         if ( AbortByCheckSave() ) return;
         var mods = GridModList.SelectedItems.OfType<ModInfo>();
         var reset = mods.Where( e => e.Is( ModQuery.HAS_CONFIG_FILE ) ).ToList();
         var msg = "\r\r" + string.Join( "\r", mods.Select( e => e.Name + ( e.Is( ModQuery.HAS_CONFIG_FILE ) ? " (Delete config?)" : "" ) ) );
         if ( reset.Count > 0 ) {
            var ans = MessageBox.Show( "Delete Mods and Config?\r(No to delete mods but keep configs)" + msg, "Delete Mod",
                  MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel );
            if ( ans == MessageBoxResult.Cancel ) return;
            if ( ans == MessageBoxResult.Yes ) {
               var delConf = App.ModActionTask( AppAction.DELETE_CONFIG, reset );
               if ( ! delConf.Wait( 30_000 ) || delConf.IsFaulted ) {
                  Prompt( AppAction.DELETE_CONFIG, PromptFlag.ERROR, (Exception) delConf.Exception ?? new TimeoutException() );
                  return;
               }
            }
         } else {
            var ans = MessageBox.Show( "Delete Mods?" + msg, "Delete Mod",
                  MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel );
            if ( ans == MessageBoxResult.Cancel ) return;
         }
         SharedGui.IsAppWorking = true;
         App.ModActionTask( AppAction.DEL_MOD, mods ).ContinueWith( result => {
            SharedGui.IsAppWorking = false;
            if ( result.IsFaulted ) Prompt( AppAction.DEL_MOD, PromptFlag.ERROR, result.Exception );
            this.Dispatch( () => ButtonRefreshMod_Click( sender, evt ) );
         } );
      }

      private bool AbortByCheckSave () {
         if ( ModList == null ) return false;
         var unsaved = ModList.Where( e => e.Is( ModQuery.EDITING ) );
         if ( ! unsaved.Any() ) return false;
         Log( "Has unsaved mod. Prompting user." );
         var result = MessageBox.Show( "Save config first?\n\n" + string.Join( "\n", unsaved.Select( e => e.Name ) ), "Save Configs",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel );
         if ( result == MessageBoxResult.Cancel ) return true;
         if ( result == MessageBoxResult.No ) return false;
         try {
            Log( "Batch saving all unsaved mods." );
            Task.WaitAll( unsaved.Select( e => Task.Run( () => e.Do( AppAction.SAVE_CONFIG ) ) ).ToArray() );
            return false;
         } catch ( AggregateException ex ) {
            Prompt( AppAction.SAVE_CONFIG, PromptFlag.ERROR, ex );
            return true;
         }
      }
      #endregion

      #region Updater
      private object Update;

      private void CheckUpdate ( bool manual ) { try {
         if ( ! manual ) {
            DateTime? lastCheck;
            if ( ! App.Settings.CheckUpdate ) return;
            lastCheck = App.Settings.LastCheckUpdate;
            if ( ! lastCheck.HasValue )
               Log( "Last update check was never" );
            else {
               Log( $"Last update check was {lastCheck}" );
               if ( lastCheck != null && ( DateTime.Now - lastCheck.Value ).TotalDays < 7 ) return;
            }
         }
         Log( "Checking update" );
         Update = "checking";
         RefreshUpdateStatus();
         App.CheckUpdateTask().ContinueWith( task => {
            if ( task.IsFaulted ) {
               if ( manual ) Prompt( AppAction.CHECK_UPDATE, PromptFlag.ERROR, task.Exception );
               return;
            }
            SetInfo( GuiInfo.APP_UPDATE, task.Result );
            if ( task.Result == null && manual )
               MessageBox.Show( "No update.", "Check Update", MessageBoxButton.OK, MessageBoxImage.Information );
         } );
      } catch ( Exception ex ) { Log( ex ); } }

      private void UpdateChecked () { try {
         Log( $"Updating last update check time." );
         App.Settings.LastCheckUpdate = DateTime.Now;
         App.ModBridge.SaveSettings();
         ButtonCheckUpdate.IsEnabled = true;
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonCheckUpdate_Click ( object sender, RoutedEventArgs e ) => CheckUpdate( true );

      private void RefreshUpdateStatus () { try {
         if ( Update != null )
            Log( $"Update is {Update}" );
         if ( Object.Equals( "checking", Update ) ) {
            ButtonCheckUpdate.IsEnabled = false;
            BtnTextCheckUpdate.Text = "Checking...";
            return;
         }
         ButtonCheckUpdate.IsEnabled = true;
         BtnTextCheckUpdate.Text = "Check Update";
         if ( App.Settings?.UpdateChannel == "dev" ) BtnTextCheckUpdate.Text += " (dev)";
         var release = Update as GithubRelease;
         if ( release == null ) return;

         var result = MessageBox.Show( $"Update {release.Tag_Name} released.\nOpen download page?", "Updater", MessageBoxButton.YesNo );
         if ( result == MessageBoxResult.No ) return;
         if ( ! String.IsNullOrWhiteSpace( release.Html_Url ) )
            Process.Start( release.Html_Url );
      } catch ( Exception ex ) { Log( ex ); } }
      #endregion

      #region Logging
      public void Log ( object message ) {
         var txt = ( Dispatcher.CheckAccess() ? "GUI┊" : "" ) + message?.ToString();
         Console.WriteLine( txt );
         var time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         this.Dispatch( () => { try {
            TextLog.AppendText( time + txt + "\n" );
            TextLog.ScrollToEnd();
            ButtonLogSave.IsEnabled = true;
         } catch ( Exception ex ) { Console.WriteLine( ex ); } } );
      }

      private void CheckLogVerbo_Checked ( object sender, RoutedEventArgs e ) {
         App.SetLogLevel( CheckLogVerbo.IsChecked == true ? SourceLevels.Verbose : SourceLevels.Information );
      }

      private void ButtonLogSave_Click ( object sender, RoutedEventArgs e ) { try {
         var name =  ButtonLicense.IsChecked == true
               ? AppControl.LIVE_NAME + $" License"
               : AppControl.LIVE_NAME + $" Log " + DateTime.Now.ToString( "u", InvariantCulture ).Replace( ':', '-' );
         var dialog = new Microsoft.Win32.SaveFileDialog {
            FileName = name,
            DefaultExt = ".txt",
            Filter = "Log Files (.txt .log)|*.txt;*.log|All Files|*.*"
         };
         if ( dialog.ShowDialog().GetValueOrDefault() ) {
            File.WriteAllText( dialog.FileName, ButtonLoaderLog.IsChecked == true || ButtonLicense.IsChecked == true ? TextLicense.Text : TextLog.Text );
            AppControl.Explore( dialog.FileName );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonLogClear_Click ( object sender, RoutedEventArgs e ) {
         TextLog.Clear();
         ButtonLogSave.IsEnabled = false;
      }

      private bool AnyLogChecked => ButtonLoaderLog.IsChecked == true ||
         ButtonConsoleLog.IsChecked == true || ButtonLicense.IsChecked == true;

      private void ButtonLoaderLog_Checked ( object sender, RoutedEventArgs e ) {
         if ( ButtonLoaderLog.IsChecked == true ) ShowLog( "loader" );
         else ResetLogPanel();
      }

      private void ButtonConsoleLog_Checked ( object sender, RoutedEventArgs e ) {
         if ( ButtonConsoleLog.IsChecked == true ) ShowLog( "console" );
         else ResetLogPanel();
      }

      private void ButtonChangeLog_Checked ( object sender, RoutedEventArgs e ) {
         if ( ButtonChangeLog.IsChecked == true ) ShowLog( "change" );
         else ResetLogPanel();
      }

      private void ButtonLicense_Checked ( object sender, RoutedEventArgs e ) {
         if ( ButtonLicense.IsChecked == true ) ShowLog( "license" );
         else ResetLogPanel();
      }

      private void ResetLogPanel () {
         if ( ! AnyLogChecked ) ShowLog( "gui" );
      }

      private DateTime? ConsoleLogTime;
      private DateTime? LoaderLogTime;
      private string CurrentLog = "gui";

      private void ShowLog ( string type ) { try {
         CurrentLog = type;
         bool isGui = false, isLoader = false, isConsole = false, isChange = false, isLicense = false;
         switch ( type ) {
            case "gui"    : LabelLogTitle.Content = "Manager Log"; isGui = true; break;
            case "console": LabelLogTitle.Content = "Console Log"; isConsole = true; break;
            case "change" : LabelLogTitle.Content = "Change Log"; isChange = true; break;
            case "loader" : LabelLogTitle.Content = "Loader Log"; isLoader = true; break;
            case "license": LabelLogTitle.Content = "License"; isLicense = true; break;
         }
         TextLog.Visibility = isGui ? Visibility.Visible : Visibility.Collapsed;
         TextLicense.Visibility = isGui ? Visibility.Collapsed : Visibility.Visible;
         CheckLogVerbo.Visibility = isGui ? Visibility.Visible : Visibility.Collapsed;
         ButtonLogClear.Visibility = isGui ? Visibility.Visible : Visibility.Collapsed;
         LabelLogFilter.Visibility = TextLogFilter.Visibility = isGui || isChange || isLicense ? Visibility.Collapsed : Visibility.Visible;
         ButtonLoaderLog.IsChecked = isLoader;
         ButtonConsoleLog.IsChecked = isConsole;
         ButtonChangeLog.IsChecked = isChange;
         ButtonLicense.IsChecked = isLicense;
         if ( isGui ) TextLicense.Text = "";
         else if ( isChange  ) TextLicense.Text = ApplyLogFilter( ModMetaJson.ReadAsText( AssemblyLoader.GetResourceStream( "Changelog.md" ) ) );
         else if ( isLicense ) TextLicense.Text = ApplyLogFilter( ModMetaJson.ReadAsText( AssemblyLoader.GetResourceStream( "License.txt" ) ) );
         else if ( isLoader || isConsole ) {
            try {
               TextLicense.Text = ApplyLogFilter( Utils.ReadFile( isLoader ? App.LoaderLog : App.ConsoleLog ) );
            } catch ( SystemException ex ) {
               TextLicense.Text = ex.ToString();
            }
            ConsoleLogTime = new FileInfo( App.ConsoleLog ).LastWriteTime;
            LoaderLogTime  = new FileInfo( App.LoaderLog  ).LastWriteTime;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void TextLogFilter_TextChanged ( object sender, TextChangedEventArgs e ) => ShowLog( CurrentLog );

      private static Regex Linebreaks = new Regex( "\\r?\\n(\\s*\\r?\\n)*", RegexOptions.Compiled );

      private string ApplyLogFilter ( string log ) {
         var filter = TextLogFilter.Text;
         if ( string.IsNullOrWhiteSpace( filter ) ) return log;
         return string.Join( "\r", Linebreaks.Split( log ).Where( e => e.IndexOf( filter, StringComparison.OrdinalIgnoreCase ) >= 0 ) );
      }

      private void CheckLogRefresh () { this.Dispatch( () => {
         var isLoader  = ButtonLoaderLog.IsChecked  == true;
         var isConsole = ButtonConsoleLog.IsChecked == true;
         var file = isLoader ? App.LoaderLog : isConsole ? App.ConsoleLog : null;
         var isLoaderLogUpdated =  File.Exists( App.LoaderLog ) && new FileInfo( App.LoaderLog ).LastAccessTime > LoaderLogTime.GetValueOrDefault() ;
         if ( file != null ) {
            if ( ! File.Exists( file ) ) ShowLog( "gui" );
            if ( isLoader ) {
               if ( isLoaderLogUpdated ) ShowLog( "loader" );
            } else if ( new FileInfo( file ).LastAccessTime > ConsoleLogTime.GetValueOrDefault() )
               ShowLog( isLoader ? "loader" : "console" );
         }
         if ( isLoaderLogUpdated ) {
            LoaderLogTime = new FileInfo( App.LoaderLog ).LastAccessTime;
            App.GetModList();
         }
      } ); }
      #endregion

      private void OpenUrl ( string type, RoutedEventArgs e = null ) {
         Log( $"OpenUrl {type}" );
         if ( e?.Source is UIElement src ) src.Focus();
         string url;
         switch ( type ) {
            case "canny"  : url = "https://phoenixpoint.canny.io/feedback?sort=trending"; break;
            case "discord": url = "https://discordapp.com/invite/phoenixpoint"; break;
            case "forum"  : url = "https://forums.snapshotgames.com/c/phoenix-point"; break;
            case "guide"  : url = "https://github.com/Sheep-y/Modnix/wiki/"; break;
            case "home"   : url = "https://github.com/Sheep-y/Modnix"; break;
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "nexus"  : url = "https://www.nexusmods.com/phoenixpoint/mods/?BH=0"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "twitter": url = "https://twitter.com/Phoenix_Point"; break;
            case "wiki"   : url = "https://phoenixpoint.fandom.com/wiki/"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         Log( $"Opening {url}" );
         Process.Start( url );
      }
   }
}