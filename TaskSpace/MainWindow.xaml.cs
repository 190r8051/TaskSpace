using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ManagedWinapi;
using ManagedWinapi.Windows;
using TaskSpace.Core;
using TaskSpace.Core.Matchers;
using TaskSpace.Properties;
using static System.Drawing.Printing.PrinterSettings;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem; // #note MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see "https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls".
//using MenuItem = System.Windows.Forms.MenuItem; // #warning .NET Framework 4.8- only.
using MessageBox = System.Windows.MessageBox;

namespace TaskSpace {
    /// <summary>
    /// The MainWindow class.
    /// #notes
    /// Modify XAML ```<Window.InputBindings>``` for hardcoded hotkeys, i.e. CTRL+W mapped to CloseWindowCommand. #old?
    /// #todo Map CTRL+Enter to focus-and-maximize.
    /// #todo Should pre-map all digit and F-key combos, e.g. CTRL+1, CTRL+F1, etc.
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IDisposable {
        #region Enums
        private enum InitialFocus {
            PreviousItem
            , CurrentItem
            , NextItem
        }
        #endregion Enums

        #region Const
        /// <summary>
        /// If set to true, ALT+Space,ALT+Space (without releasing ALT) acts like ALT+Tab.
        /// </summary>
        public const bool IS_ALT_SPACE_ALT_SPACE_ENABLED = true;

        /// <summary>
        /// If set to true, ALT+Space,Space (with releasing ALT) acts like ALT+Tab.
        /// </summary>
        public const bool IS_ALT_SPACE_SPACE_ENABLED = true;

        //public const int SHOW_TIMEOUT_MS = 200;
        public const int SHOW_TIMEOUT_MS = 500;
        //public const int SHOW_TIMEOUT_MS = 900;

        public static TimeSpan SHOW_TIMEOUT = TimeSpan.FromMilliseconds(SHOW_TIMEOUT_MS);
        #endregion Const

        #region Externs
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        #endregion Externs

        #region Properties
        private bool _isLocked = false; // To prevent auto-switch (without user selection) on startup or after long periods of inactivity.

        private bool _isDisposed = false; // To detect redundant calls.

        private Key _lastKeyPressed;

        /// <summary>
        /// The SettingsCharToAppList value loaded once on startup with the following schema:
        /// - Dictionary key :: The char mapped to apps, e.g. 'V' is mapped to "Visual Studio".
        /// - Dictionary value :: The list of AppWindowViewModel objects, with at least the following initialized:
        ///     - AppFileNameWithExt, e.g. "app.exe".
        ///     - AppFilePath, e.g. "C:\app.exe" (or empty string if full file path was not found).
        ///     - IsLaunchCommand, e.g. true. Important, since then the backing fields will be used (instead of properties with extra logic).
        ///     #todo Add a field IsStatic, similar to IsLaunchCommand, but with the purpose of indicating these are static apps loaded from the settings.
        /// </summary>
        public Dictionary<char, List<AppWindowViewModel>> SettingsCharToAppList { get; private set; } = [];

        /// <summary>
        /// Contains the value when the main window has been activated.
        /// Used for better detection if Space key was part of the original trigger (even if ALT is claimed not to be held anymore).
        /// </summary>
        private DateTime? _mainWindowActiveDateTime = null;

        /// <summary>
        /// Set to true when the main window has been activated.
        /// </summary>
        private bool _isMainWindowActive = false;

        /// <summary>
        /// The is-search-enabled value.
        /// </summary>
        private bool _isSearchEnabled = false;

        /// <summary>
        /// The is-auto-mapping-enabled value.
        /// #future Move to settings (settable in UI).
        /// </summary>
        private bool _isAutoMappingEnabled = true;

        //private int? _lastSelectedIndex = null; // #cut

        private string _launchCommand = string.Empty;

        private readonly List<List<string>> _folderFiles = [];

        /// <summary>
        /// The search-filtered window list. #note This is used in the text-search mode.
        /// This collection represents the subset of windows that match the current search filter applied by the user.
        /// It is used to display only those windows that meet the filtering criteria (e.g., based on search input).
        /// Since it's an ObservableCollection, changes (like adding/removing windows) automatically propagate to the UI.
        /// </summary>
        private ObservableCollection<AppWindowViewModel> _searchFilteredWindowList;

        /// <summary>
        /// The hotkey-filtered window list. #note This is used in the hotkey mode.
        /// This list contains the windows that are specifically targeted through direct keyboard input,
        /// usually by using a hotkey or some other direct selection mechanism (such as indexing alphabetically).
        /// It is typically a subset of the complete list of windows, but may not always be filtered by user input.
        /// Unlike _searchFilteredWindowList, this list is not automatically bound to the UI.
        /// It is used for special targeting operations.
        /// </summary>
        private List<AppWindowViewModel> _hotkeyFilteredWindowList = null;

        /// <summary>
        /// The backup of the hotkey-filtered window list. #note This is used in the hotkey mode.
        /// This list is a backup copy of _hotkeyFilteredWindowList, created to preserve the state of the hotkey-filtered windows
        /// when _hotkeyFilteredWindowList is cleared (for example, when switching between modes).
        /// It allows the application to restore the target windows if necessary.
        /// The backup ensures that the filtered or selected state can be reverted after operations that might
        /// temporarily modify the original _hotkeyFilteredWindowList.
        /// </summary>
        private List<AppWindowViewModel> _hotkeyFilteredWindowListBackup = null;

        /// <summary>
        /// The unfiltered window list.
        /// This list contains all windows currently open in the system, without any filtering applied.
        /// It serves as the complete source of windows from which both _searchFilteredWindowList and _hotkeyFilteredWindowList
        /// are derived.
        /// Unlike _searchFilteredWindowList and _hotkeyFilteredWindowList, this list is always populated with all available windows regardless of
        /// user input or filtering state. It is the base list for all window operations.
        /// </summary>
        private List<AppWindowViewModel> _unfilteredWindowList;

        private List<string> _blockList;

        private WindowCloser _windowCloser;

        private NotifyIcon _notifyIcon;

        private Hotkey _hotkeyMain; // Main hotkey. ALT+Space default.
        private Hotkey _hotkeyAlt; // Alternate hotkey. ALT+` default.

        // #example In XAML, some commands like ScrollListUp need to be set-up in Window.CommandBindings, Window.InputBindings, and Grid,
        // while other commands like LaunchWindowAsync need to be set-up only in Window.CommandBindings and Window.InputBindings.
        // ```
        // <Window.CommandBindings>
        //      <CommandBinding Command="local:MainWindow.RoutedUICommand_ScrollListUp" Executed="ScrollListUp" />
        //      <CommandBinding Command="local:MainWindow.RoutedUICommand_LaunchWindow" Executed="LaunchWindowAsync" />
        // ...
        // <Window.InputBindings>
        //      <KeyBinding Key="Up" Command="local:MainWindow.RoutedUICommand_ScrollListUp" />
        //      <KeyBinding Modifiers="Ctrl" Key="W" Command="local:MainWindow.RoutedUICommand_LaunchWindow" />
        // ...
        //        <Grid x:Name="GridMain" DockPanel.Dock="Top">
        //        <!--<TextBlock Name="ShowSearchIcon" Margin="0,0,10,0" Width="15" HorizontalAlignment="Left" TextAlignment="Center"
        //               VerticalAlignment="Center"
        //               FontSize="18" FontWeight="Bold" Opacity="0.4"
        //               PreviewMouseDown="ShowSearchIcon_OnPreviewMouseDown"
        //                Cursor="Hand" Foreground="LightGray">
        //            <TextBlock.Text>🔎</TextBlock.Text>
        //        </TextBlock>-->
        //        <TextBox Name="TextBoxSearch" Margin="60,0,0,0" Padding="5" VerticalAlignment="Top" TextChanged="TextChanged" FontSize="15"
        //                 VerticalContentAlignment="Center" BorderBrush="{x:Null}" BorderThickness="0"
        //            >
        //            <TextBox.InputBindings>
        //                <!-- #legacy[From Switcheroo.] For now, leave these here. Would it be better to route them upward? -->
        //                <KeyBinding Command="local:MainWindow.RoutedUICommand_ScrollListUp" Key="Up" />
        // ```
        // In XAML.CS...
        // ```
        // public static readonly RoutedUICommand RoutedUICommand_LaunchWindow = new();
        // private async void LaunchWindowAsync(object sender, ExecutedRoutedEventArgs e) {
        // ```
        public static readonly RoutedUICommand RoutedUICommand_LaunchWindow = new();
        public static readonly RoutedUICommand RoutedUICommand_LaunchAdminWindow = new();
        public static readonly RoutedUICommand RoutedUICommand_CloseWindow = new();
        public static readonly RoutedUICommand RoutedUICommand_KillWindow = new();
        public static readonly RoutedUICommand RoutedUICommand_SwitchToWindow = new();
        //public static readonly RoutedUICommand ScrollListDownCommand = new();
        public static readonly RoutedUICommand RoutedUICommand_ScrollListDown = new();
        public static readonly RoutedUICommand RoutedUICommand_ScrollListUp = new();
        public static readonly RoutedUICommand RoutedUICommand_ScrollListHome = new();
        public static readonly RoutedUICommand RoutedUICommand_ScrollListEnd = new();

        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;

        //private bool _altTabAutoSwitch; // #todo What does this do? Maybe something where the search box is focused? Or maybe something with quick single button hits? Or takes-over ALT+Tab? Or does ALT+Tab immediate switch, but using ALT+Space?
        //private bool _isSorted = false;

        private Theme _theme;
        #endregion Properties

        #region Constructor
        /// <summary>
        /// The MainWindow constructor.
        /// </summary>
        public MainWindow() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)} constructor start...");

            InitializeComponent();

            SetUpNotifyIcon();

            //this.KeyDown += this.MainWindow_KeyDown; // #todo Move to below?
            SetUpDefaultHotkeys(); // Set up default ALT+Space and ALT+`.

            SetUpAltTabHook();

            SetUpKeyBindings();

            LoadSettings();

            //CheckForUpdates(); // #cut

            _theme = new Theme(this);

            this.Opacity = 0;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)} constructor end.");
        }
        #endregion Constructor

        #region Methods
        private void LoadSettings() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LoadSettings)} start...");

            this.SettingsCharToAppList = [];

            // Initialize SettingsCharToAppList dictionary using the Settings.
            for(int i = (int)'A'; i <= (int)'Z'; i++) {
                char letter = (char)i; // E.g. 'F'.

#if DEBUG
                if (letter == 'B') {
                    Debug.WriteLine("TARGET");
                }
#endif

                string propertyName = $"Apps_{letter}"; // E.g. "Apps_F".

                // Use reflection to get the property by name, e.g. Properties.Settings.Default.Apps_F.
                PropertyInfo settingsPropertyInfo = Properties.Settings.Default.GetType().GetProperty(propertyName);
                if(settingsPropertyInfo == null) {
                    continue;
                }

                List<AppWindowViewModel> appList = [];

                // Get the raw value of the settings-property-info.
                object value = settingsPropertyInfo.GetValue(Properties.Settings.Default, null);

                // Check if the settings value is a StringCollection.
                if(value is System.Collections.Specialized.StringCollection stringCollection) {
                    List<string> rawStrings = stringCollection
                        .Cast<string>()
                        .Select(x => x.ToLower())
                        .ToList();

                    //List<(string, string, string)> tuples = rawStrings
                    //.Select(x => x.ToTupleOf3())
                    //.ToList();

                    foreach(string appString in rawStrings) {
                        // #note The "app" value is either fully-quelified or not, so need to cover both cases.
                        string appFileNameWithExt = string.Empty;
                        string appFilePath = string.Empty;
                        if(Path.IsPathFullyQualified(appString)) {
                            // Fully qualified, so tuple Item1 is file-name-ext and Item2 is file-path.
                            appFileNameWithExt = Path.GetFileName(appString);
                            appFilePath = appString;
                        }
                        else {
                            // NOT fully qualified, so tuple Item1 is file-name-ext and Item2 is empty string (no file-path).
                            appFileNameWithExt = appString;
                        }

                        if(!string.IsNullOrWhiteSpace(appFileNameWithExt)) {
                            // #note We don't yet have appWindow here.
                            AppWindowViewModel appWindowViewModel = new(
                                appFileNameWithExt: appFileNameWithExt
                                , appFilePath: appFilePath
                                , isLaunchCommand: true // Set to true for now.
                                , icon: null
                            );

                            appList.Add(appWindowViewModel);
                        }
                    }
                }

                this.SettingsCharToAppList[letter] = appList;
            } //^^^ for(int i = (int)'A'; i <= (int)'Z'; i++) { ^^^

            bool isPowerMenuEnabled = true; // #TODO Param.
            if(isPowerMenuEnabled) {
                List<string> rawStrings = Properties.Settings.Default.Folder_0_Power.Cast<string>().ToList();

                // #FUTURE Could add an extra tuple items for specified alpha/symbol?
                List<AppWindowViewModel> appList = [];

                List<(string, string, string)> tuples = rawStrings
                    .Select(x => x.ToTupleOf3())
                    .ToList();
                foreach((string app, string letter, string symbol) in tuples) {
                    // #NEW
                    /*
                    //// If conversion fails, use a custom default icon
                    //if(iconImage == null || iconImage.UriSource == null) {
                    //    iconImage = new BitmapImage(new Uri("pack://application:,,,/YourAssemblyName;component/Images/DefaultIcon.png"));
                    //}
                    // Retrieve the default icon from the project's resources.
                    using(System.IO.MemoryStream iconStream = new System.IO.MemoryStream()) {
    #pragma warning disable CA1416 // Validate platform compatibility.
                        TaskSpace.Properties.Resources.icon.Save(iconStream); // Assuming DefaultIcon is of type `Icon`.
    #pragma warning restore CA1416 // Validate platform compatibility.
                        iconStream.Seek(0, System.IO.SeekOrigin.Begin);

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = iconStream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        iconImage = bitmapImage;
                    }
                    */

                    Icon icon = Properties.Resources.icon; // #TODO Not just one icon.

                    AppWindowViewModel appWindowViewModel = new(
                        launchCommand: app
                        , letter: letter?.FirstOrNull() ?? '?' // #TODO Fall-through character?
                                                               //, symbol: symbol // #TODO###
                        , icon: icon
                    );

                    appList.Add(appWindowViewModel);
                }

                this.SettingsCharToAppList[Properties.Settings.Default.Folder_0_Key] = appList;
            }

#if DEBUG
            // #BUG Why are settings changes not picked-up automatically?
            Properties.Settings.Default.Reset();
#endif

            _blockList = Properties.Settings.Default.BlockList.Cast<string>()
                .Select(x => x.ToLower())
                .ToList();

#if DEBUG
            // #BUG Why are settings changes not picked-up automatically?
            //_blockList.Add("rtkuwp");
#endif

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LoadSettings)} end.");
        }

        // #todo!!! Could add the main task launching here, e.g. check "1", "V", etc.
        // #todo If "_" is hit, then switch to the search mode (but could backspace back to the main mode).
        private void SetUpKeyBindings() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpKeyBindings)} start...");

            string letter = string.Empty;

            // Enter and Esc bindings are not executed before the keys have been released.
            // This is done to prevent that the window being focused after the key presses to get 'KeyUp' messages.
            KeyDown += async (sender, args) => { // #TODO Why async?
                                                 // #TODO Move a lot of mappings to KeyUp instead.

                // Opacity is set to 0 right away so it appears that action has been taken right away (instead of waiting for release).
                if(args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    Opacity = 0;
                }
                else if(args.Key == Key.Escape) {
                    Opacity = 0;
                }
                // #note Switcheroo doesn't have this.
                //else if(args.SystemKey == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                //    _altTabAutoSwitch = false;
                //    tb.Text = "";
                //    tb.IsEnabled = true;
                //    tb.Focus();
                //}
                else if(!_isSearchEnabled) {
                    // #note CAN'T detect WIN at this level.
                    // #todo Capture all different modifiers, e.g. CTRL+ALT+SHIFT+<key>.

                    // #todo But what if the app is not mapped?
                    // e.g. hit 9, but there are no 9 apps? Or hit V, but this is not mapped directly to anything?
                    // Then, automatically switch to search mode, e.g. type " " (to be consistent with search mode), then type 9 or V.

                    // Do determine this here (i.e. DON'T hide the app if the above search was actually executed).

                    // #CUT
                    // #todo!!! Should follow the same schema as below.
                    //if(args.Key == Key.OemTilde) { // #todo Make sure that there are no modifiers? #CUT?
                    //    AppWindowViewModel targetWindow = GetSwitchableWindows(args.Key, out letter).FirstOrDefault();
                    //    if(targetWindow != null) {
                    //        _launchCommand = targetWindow.LaunchCommand;
                    //        this.Opacity = 0; // Disappear the main UI window.
                    //    }
                    //}
                    //else
                    if(args.Key == Properties.Settings.Default.Folder_0_Key.ToString().ToKey()) { // #todo Make sure that there are no modifiers?
                                                                                                  // #FUTURE The settings should have the pre-mapped keys (letter, symbol, maybe also NumPad?,) as well as icon names.
                                                                                                  // For icons, should be able to override the icon with the one from settings (might also work for regular apps).
                        _hotkeyFilteredWindowList = GetPowerMenuOptions();
                        _hotkeyFilteredWindowListBackup = [.. _hotkeyFilteredWindowList]; // Create a shallow copy.
                        if(_hotkeyFilteredWindowList != null && _hotkeyFilteredWindowList.Count == 1) {
                            this.Opacity = 0;
                        }

                        // #CUT
                        //// #todo _hotkeyFilteredWindowList = GetPowerMenuOptions(); // #todo Also _hotkeyFilteredWindowListBackup?
                        //AppWindowViewModel targetWindow = GetSwitchableWindows(args.Key, out letter).FirstOrDefault();
                        //if(targetWindow != null) {
                        //    _launchCommand = targetWindow.LaunchCommand;
                        //    this.Opacity = 0; // Disappear the main UI window.
                        //}
                    }
                    else if((Key.D0 <= args.Key && args.Key <= Key.D9)
                        || (Key.NumPad0 <= args.Key && args.Key <= Key.NumPad9)
                        || (Key.F1 <= args.Key && args.Key <= Key.F24)
                        || (Key.A <= args.Key && args.Key <= Key.Z)
                    ) {
                        // #TODO Move to KeyUp instead?
                        _hotkeyFilteredWindowList = GetSwitchableWindows(args.Key, out letter);
                        _hotkeyFilteredWindowListBackup = [.. _hotkeyFilteredWindowList]; // Create a shallow copy.
                        if(_hotkeyFilteredWindowList != null && _hotkeyFilteredWindowList.Count == 1) {
                            this.Opacity = 0;
                        }
                    }
                }
            };

            // #warning DON'T use variables from outer scope (unless global).
            KeyUp += (sender, args) => {
                // ... But only when the keys are released, the action is actually executed.
                if(args.Key == Key.Space) {
                    //Debug.WriteLine($"DEBUG :: Space detected.");

                    bool shouldSwitch = false;
                    string debugShouldSwitch = string.Empty;
                    if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                        // #nop This is likely the current main-window launch.
                        // #todo Never hit?
                        Debug.WriteLine($"DEBUG :: NOP :: ALT+Space detected.");

                        if(IS_ALT_SPACE_ALT_SPACE_ENABLED) {
                            TimeSpan? timeSpanDiff = _mainWindowActiveDateTime.HasValue
                                ? DateTime.UtcNow - _mainWindowActiveDateTime
                                : null;
                            if(!timeSpanDiff.HasValue
                                || !_mainWindowActiveDateTime.HasValue
                            ) {
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this is likely ALT+Space near startup. timeSpanDiff=```{timeSpanDiff}```; _mainWindowActiveDateTime=```{_mainWindowActiveDateTime}```; .");
                            }
                            else if(timeSpanDiff < SHOW_TIMEOUT) {
                                // #nop This is likely the current main-window launch (even though ALT is not detected).
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space then released => Initial launch...");
                            }
                            else {
                                // [bug]?
                                //if(_isLocked) {
                                //    Debug.WriteLine($"DEBUG :: ALT not detected, but this is likely ALT+Space near startup. _isLocked=```{_isLocked}```.");
                                //}
                                //else {
                                //debugShouldSwitch = $"";
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space (UI launched), then Space => Switch. _isLocked=```{_isLocked}```.");

                                // #todo Check flags like Control etc.
                                shouldSwitch = true;
                                //}
                            }
                        }
                    }
                    else if(_isMainWindowActive) {
                        if(!IS_ALT_SPACE_ALT_SPACE_ENABLED || IS_ALT_SPACE_SPACE_ENABLED) {
                            TimeSpan? timeSpanDiff = _mainWindowActiveDateTime.HasValue
                                ? DateTime.UtcNow - _mainWindowActiveDateTime
                                : null;
                            if(!timeSpanDiff.HasValue
                                || !_mainWindowActiveDateTime.HasValue
                            ) {
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this is likely ALT+Space near startup. timeSpanDiff=```{timeSpanDiff}```; _mainWindowActiveDateTime=```{_mainWindowActiveDateTime}```; .");
                            }
                            else if(timeSpanDiff < SHOW_TIMEOUT) {
                                // #nop This is likely the current main-window launch (even though ALT is not detected).
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space then released => Initial launch. _mainWindowActiveDateTime=```{_mainWindowActiveDateTime:u}```; UtcNow=```{DateTime.UtcNow:u}```.");
                            }
                            else {
                                Debug.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space (UI launched), then Space => Switch. _mainWindowActiveDateTime=```{_mainWindowActiveDateTime:u}```; UtcNow=```{DateTime.UtcNow:u}```; timeSpanDiff=```{timeSpanDiff}```.");

                                // #todo Check flags like Control etc.
                                shouldSwitch = true;
                            }
                        }
                    }

                    if(shouldSwitch) {
                        Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} before 511...");
                        Switch(); // Switch to currently selected app (i.e. treat Space as Enter).
                    }
                }
                else if(args.Key == Key.Back) {
                    _hotkeyFilteredWindowList = _unfilteredWindowList;
                    _hotkeyFilteredWindowListBackup = [.. _hotkeyFilteredWindowList]; // Create a shallow copy.
                    SwitchOrFilter(_hotkeyFilteredWindowList, letter: string.Empty); // Restore unfiltered list in UI.
                }
                // #note This might not have all the combinations from above, e.g. ALT+S.
                else if(args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} before 376...");
                    Switch();
                }
                else if(args.Key == Key.Escape) {
                    HideMainWindow();
                }
                // #note Switcheroo doesn't have this.
                //else if(args.SystemKey == Key.LeftAlt && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                //    // #bug!!! This needs more sanity-testing: maybe this is why often the first app just switches immediately.
                //    Switch();
                //}
                //else if(args.Key == Key.LeftAlt && _altTabAutoSwitch) {
                //    Switch();
                //}
                else if(!string.IsNullOrWhiteSpace(_launchCommand)) {
                    Process.Start(_launchCommand);
                }
                else if(!_isSearchEnabled && _hotkeyFilteredWindowList != null) {
                    // #TODO This has the 2 windows (eg if filtered from 10+ window to 2 Explorer windows).
                    // #BUT Why is the global list never updated properly, eg when removing windows in _filteredWindowList, it still seems to have the starting full list?

                    // Progressively hotkey-filter:
                    // 1. Starts with windows already filtered in the current method (```_hotkeyFilteredWindowList = GetSwitchableWindows(args.Key, out letter);```).
                    // 2. The next hotkey is expected to be one of the following:
                    //      a. Same letter => Select the first item in the list.
                    //      b. Digit (or F-key) => Select the corresponding item.
#if DEBUG
                    Debug.WriteLine("Before SwitchOrFilter...");
#endif
                    SwitchOrFilter(_hotkeyFilteredWindowList, letter);
                }

                _launchCommand = string.Empty;
            };

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpKeyBindings)} end.");
        }

        /// <summary>
        /// Toggles the search mode for the application:
        /// - If `isSearchEnabled` is `null`, the method will toggle the search mode (switching it between enabled and disabled).
        /// - If `isSearchEnabled` is `true`, the search mode will be explicitly enabled.
        /// - If `isSearchEnabled` is `false`, the search mode will be explicitly disabled.
        ///
        /// This method also updates the visual appearance of the search textbox and its background color depending
        /// on whether the search mode is enabled or disabled, and modifies the opacity of the search icon.
        /// </summary>
        /// <param name="isSearchEnabled">
        /// #opt[default is null]
        /// Optional parameter controlling the state of the search mode:
        ///     - `null` :: Toggle between enabled/disabled.
        ///     - `true` :: Enable search mode.
        ///     - `false` :: Disable search mode.
        /// </param>
        private void ToggleSearch(
            bool? isSearchEnabled = null
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} start...");

            bool isSearchEnabledLocal = !isSearchEnabled.HasValue
                ? !_isSearchEnabled
                : (isSearchEnabled == true);

            if(isSearchEnabledLocal) {
                _isSearchEnabled = true;

                TextBoxSearch.IsReadOnly = false; // [!] Read-only false so that the caret now shows (but only after additional typing?,).

                TextBoxSearch.Background = _theme.BackgroundAlt; // Slightly lighter (in dark theme).

                SearchIcon.Opacity = 1.0;
            }
            else {
                _isSearchEnabled = false;

                TextBoxSearch.IsReadOnly = false; // [!] Read-only true so that the caret no longer shows.

                TextBoxSearch.Background = _theme.Background;

                SearchIcon.Opacity = 0.4;
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} end.");
        }

        private void SetUpDefaultHotkeys() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpDefaultHotkeys)} start...");

            this.PreviewKeyDown += this.MainWindow_PreviewKeyDown;

            // Set up the main hotkey, e.g. ALT+Space.
            _hotkeyMain = new Hotkey();
            _hotkeyMain.LoadSettingsMain();

            // Set up the alt hotkey, e.g. ALT+` (below tilde).
            _hotkeyAlt = new Hotkey();
            _hotkeyAlt.LoadSettingsAlt();

            Application.Current.Properties["HotkeyMain"] = _hotkeyMain;
            Application.Current.Properties["HotkeyAlt"] = _hotkeyAlt;

            _hotkeyMain.HotkeyPressed += hotkeyMain_HotkeyPressed; // ALT+Space default.
            try {
                _hotkeyMain.IsEnabled = Settings.Default.EnableHotkeyMain;
            }
            catch(HotkeyAlreadyInUseException) {
                string boxText = "The current hotkey for activating TaskSpace is in use by another program."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "You can change the hotkey by right-clicking the TaskSpace icon in the system tray and choosing 'Options'.";
                // #TODO Update.
                MessageBox.Show(boxText, "Hotkey already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _hotkeyAlt.HotkeyPressed += hotkeyAlt_HotkeyPressed; // ALT+` default.
            try {
                _hotkeyAlt.IsEnabled = Settings.Default.EnableHotkeyAlt;
            }
            catch(HotkeyAlreadyInUseException) {
                string boxText = "The current hotkey2 for activating TaskSpace is in use by another program."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "You can change the hotkey by right-clicking the TaskSpace icon in the system tray and choosing 'Options'.";
                // #TODO Update.
                MessageBox.Show(boxText, "Hotkey2 already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpDefaultHotkeys)} end.");
        }

        private void MainWindow_PreviewKeyDown(
            object sender
            , System.Windows.Input.KeyEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_PreviewKeyDown)} start...");

            if(e.Key == Key.System) {
                // Handle ALT key combinations. Use e.SystemKey to get the actual key pressed with ALT.
                _lastKeyPressed = e.SystemKey;
            }
            //else if(e.Key == Key.None) {
            //    // Ignore if no key was pressed.
            //    return;
            //}
            else {
                // Store the last key pressed for non-system keys.
                _lastKeyPressed = e.Key;
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_PreviewKeyDown)} end.");
        }

        private void SetUpAltTabHook() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpAltTabHook)} start...");

            _altTabHook = new AltTabHook();
            _altTabHook.Pressed += AltTabPressed;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpAltTabHook)} end.");
        }

        private void SetUpNotifyIcon() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpNotifyIcon)} start...");

            Icon icon = Properties.Resources.icon;

            //MenuItem runOnStartupMenuItem = new MenuItem("Run on Startup", (s, e) => RunOnStartup(s as MenuItem)) { // #warning .NET Framework 4.8-.
            ToolStripMenuItem runOnStartupMenuItem = new ToolStripMenuItem(
                text: "Run on Startup",
                image: null,
                (s, e) => RunOnStartup(s as MenuItem)
            ) {
                Checked = new AutoStart().IsEnabled
            };

            _notifyIcon = new NotifyIcon() {
                Text = "TaskSpace",
                Icon = icon,
                Visible = true,
                //ContextMenu = new System.Windows.Forms.ContextMenu(new[] { // #warning .NET Framework 4.8-.
                ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip {
                    Items = {
                        new ToolStripMenuItem(text: "Options", image: null, (s, e) => Options()),
                        runOnStartupMenuItem,
                        new ToolStripMenuItem(text: "About", image: null, (s, e) => About()),
                        new ToolStripMenuItem(text: "Exit", image: null, (s, e) => Quit())
                    }
                }
            };

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SetUpNotifyIcon)} end.");
        }

        //private static void RunOnStartup(MenuItem menuItem) { // #warning .NET Framework 4.8-.
        private static void RunOnStartup(
            ToolStripMenuItem toolStripMenuItem
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(RunOnStartup)} start...");

            try {
                AutoStart autoStart = new AutoStart {
                    IsEnabled = !toolStripMenuItem.Checked
                };

                toolStripMenuItem.Checked = autoStart.IsEnabled;
            }
            catch(AutoStartException ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(RunOnStartup)} end.");
        }

        /// <summary>
        /// Populates the window list with the current running windows, creating the mappings.
        /// #future #perf Could map from the settings once and append only automatic mappings on every LoadData call.
        /// </summary>
        /// <param name="initialFocus">The initial focus value, i.e. Previous/Current/Next.</param>
        /// <param name="isSingleAppModeEnabled">#opt[default is false] Activates the single-app-mode, i.e. the UI is filtered to a single (current) app instances.</param>
        private void PopulateWindowList(
            InitialFocus initialFocus
            , bool isSingleAppModeEnabled = false
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(PopulateWindowList)} start...");

            // Restore searchbox defaults.
            TextBoxSearch.Background = _theme.Background;
            SearchIcon.Opacity = 0.4;

            _isMainWindowActive = true;

            TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret again doesn't show.

#pragma warning disable IDE0305 // Simplify collection initialization.

            _unfilteredWindowList = WindowFinder
                .GetAndMapWindows(_blockList, this.SettingsCharToAppList, this._isAutoMappingEnabled, isPowerMenuEnabled: true)
                .ToList();
#pragma warning restore IDE0305 // Simplify collection initialization.

            AppWindowViewModel firstAppWindowViewModel = _unfilteredWindowList.FirstOrDefault();

            bool isForegroundWindowMovedToBottom = false;

            // Index the unfiltered windows.
            for(int i0 = 0, i1 = 1; i0 < _unfilteredWindowList.Count; i0++, i1++) {
                AppWindowViewModel appWindowViewModel = _unfilteredWindowList[i0];

                appWindowViewModel.FormattedTitle = new XamlHighlighter().Highlight([new StringPart(appWindowViewModel.WindowTitle)]);

                appWindowViewModel.FormattedProcessTitle = new XamlHighlighter().Highlight([new StringPart(appWindowViewModel.AppFileNameWithExt)]);

                appWindowViewModel.OrdinalMapped1 = new XamlHighlighter().Highlight([new StringPart(i1.Get1BasedIndexString())]);
            }

            _searchFilteredWindowList = new ObservableCollection<AppWindowViewModel>(_unfilteredWindowList);
            _windowCloser = new WindowCloser();

            ListBoxPrograms.DataContext = null; // #opt
            ListBoxPrograms.DataContext = _searchFilteredWindowList;

            FocusItemInList(initialFocus); //, foregroundWindowMovedToBottom); // #cut Moving to bottom isn't intuitive.

            TextBoxSearch.Clear();
            TextBoxSearch.Focus();
            TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret doesn't show (yet).

            // #buggy?
            if(isSingleAppModeEnabled) {
                string letterMapped = firstAppWindowViewModel.LetterMapped;
                if(!string.IsNullOrEmpty(letterMapped)) {
                    _hotkeyFilteredWindowList = GetSwitchableWindows(
                        key: Key.DeadCharProcessed
                        , letter: out string letter
                        , inputLetter: letterMapped
                    );

                    _hotkeyFilteredWindowListBackup = [.. _hotkeyFilteredWindowList]; // Create a shallow copy.

                    // #CUT?
                    //if(_hotkeyFilteredWindowListBackup != null && _hotkeyFilteredWindowListBackup.Count == 1) {
                    //    Opacity = 0;
                    //}

                    SwitchOrFilter(
                        appWindowViewModels: _hotkeyFilteredWindowList
                        , letter: letter
                    );
                }
            }

            CenterWindow();
            ScrollSelectedItemIntoView();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(PopulateWindowList)} end.");
        }

        private List<AppWindowViewModel> GetPowerMenuOptions() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetPowerMenuOptions)} start...");

            try {
                List<AppWindowViewModel> appWindowViewModels = SettingsCharToAppList[','];
                return appWindowViewModels;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetPowerMenuOptions)} end.");
            }
        }

        private void FocusItemInList(
            InitialFocus initialFocus
        //, bool foregroundWindowMovedToBottom
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(FocusItemInList)} start...");

            if(initialFocus == InitialFocus.PreviousItem) {
                // #note!!! Properties.Settings.Default.IsFocusNextEnabled is never applicable here.
                int previousItemIndex = ListBoxPrograms.Items.Count - 1;
                //if(foregroundWindowMovedToBottom) {
                //    --previousItemIndex;
                //}

                ListBoxPrograms.SelectedIndex = 0 < previousItemIndex
                    ? previousItemIndex
                    : 0;
            }
            else if(initialFocus == InitialFocus.CurrentItem) {
                // #nop Keeping the current item index.
            }
            else if(initialFocus == InitialFocus.NextItem) { // #redundant #todo!!! Add to visual settings with description.
                if(Properties.Settings.Default.IsFocusNextEnabled) {
                    ListBoxPrograms.SelectedIndex = 1;
                }
                else {
                    ListBoxPrograms.SelectedIndex = 0;
                }
            }

            //_lastSelectedIndex = listboxProgramName.SelectedIndex; // #cut?

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(FocusItemInList)} end.");
        }

        /// <summary>
        /// Place the TaskSpace window in the center of the screen.
        /// </summary>
        private void CenterWindow() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CenterWindow)} start...");

            // Reset height every time to ensure that resolution changes take effect.
            Border.MaxHeight = SystemParameters.PrimaryScreenHeight;

            // Force a rendering before repositioning the window.
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Position the window in the center of the screen.
            Left = (SystemParameters.PrimaryScreenWidth / 2) - (ActualWidth / 2);
            Top = (SystemParameters.PrimaryScreenHeight / 2) - (ActualHeight / 2);

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CenterWindow)} end.");
        }

        /// <summary>
        /// Gets the switchable windows of the same process.
        /// </summary>
        private List<AppWindowViewModel> GetSwitchableWindows(
            string processName
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetSwitchableWindows)} start...");

            try {
                List<AppWindowViewModel> retVal = [];

                // #note ItemCollection is not queryable.
                foreach(AppWindowViewModel appWindowViewModel in ListBoxPrograms.Items) {
                    if(appWindowViewModel.AppFileNameWithExt.Equals(processName, StringComparison.OrdinalIgnoreCase)) {
                        retVal.Add(appWindowViewModel);
                    }
                }

                return retVal;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetSwitchableWindows)} end.");
            }
        }

        /// <summary>
        /// Gets the switchable windows associated with the input key code.
        /// Supports multiple different apps bound to the same letter.
        /// </summary>
        private List<AppWindowViewModel> GetSwitchableWindows(
            Key key
            , out string letter
            , string inputLetter = ""
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetSwitchableWindows)} start...");

            try {
                List<AppWindowViewModel> retVal = [];
                letter = string.Empty;

                Key keyLocal = key;
                if(!string.IsNullOrEmpty(inputLetter)) {
                    try {
                        keyLocal = inputLetter.ToKey();
                    }
                    catch {
                    }
                }

                if((Key.D0 <= keyLocal && keyLocal <= Key.D9)
                    || (Key.NumPad0 <= keyLocal && keyLocal <= Key.NumPad9)
                    || (Key.F1 <= keyLocal && keyLocal <= Key.F24)
                ) {
                    int? i1 = null;
                    if(keyLocal == Key.D0 || keyLocal == Key.NumPad0) {
                        i1 = 10;
                    }
                    else if(Key.D1 <= keyLocal && keyLocal <= Key.D9) {
                        i1 = (int)(keyLocal - Key.D0); // e.g. for Key.D1, the index is 1 (D1 value 35 minus D0 value 34).
                    }
                    else if(Key.NumPad1 <= keyLocal && keyLocal <= Key.NumPad9) {
                        i1 = (int)(keyLocal - Key.NumPad0); // e.g. for Key.NumPad1, the index is 1 (NumPad1 value 75 minus NumPad0 value 74).
                    }
                    else if(Key.F1 <= keyLocal && keyLocal <= Key.F24) {
                        i1 = 11 + (int)(keyLocal - Key.F1); // e.g. for Key.F1, the index is 11 (F1 value 90 minus F1 value 90 plus 11).
                    }

                    int? i0 = !i1.HasValue ? (int?)null : i1.Value - 1;
                    if(i0.HasValue && i0.Value < ListBoxPrograms.Items.Count) {
                        retVal.Add((AppWindowViewModel)ListBoxPrograms.Items[i0.Value]);
                    }
                }
                else if(Key.A <= keyLocal && keyLocal <= Key.Z) {
                    // Calculate the letter based on the offset from Key.A.
                    letter = ((char)((int)keyLocal - (int)Key.A + 'A')).ToString();

                    // #note ItemCollection is not queryable.
                    foreach(AppWindowViewModel appWindowViewModel in ListBoxPrograms.Items) {
                        if(appWindowViewModel.LetterBound.Equals(letter, StringComparison.OrdinalIgnoreCase)) {
                            retVal.Add(appWindowViewModel);
                        }
                    }

                    retVal = [.. retVal.OrderBy(x => x.OrdinalMapped0)];
                }

                return retVal;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetSwitchableWindows)} end.");
            }
        }

        /// </summary>
        /// Switches to the window, or letter-filters if multiple instances found.
        /// </summary>
        /// <param name="appWindowViewModels">The window list, e.g. passed-in _hotkeyFilteredWindowList.</param>
        /// <param name="letter">#opt[default is empty string] Passing-in default letter means "restore unfiltered windows" (assumed that they are passed-in).</param>
        /// <param name="forceShowWindow">#opt[default is false] The force-show-window value.</param>
        private void SwitchOrFilter(
            List<AppWindowViewModel> appWindowViewModels
            , string letter = ""
            , bool forceShowWindow = false
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SwitchOrFilter)} start...");

            try {
                if(appWindowViewModels == null
                    || appWindowViewModels.Count == 0
                ) {
                    // #nop
                    return;
                }


#if DEBUG
                //if(letter == "R") {
                //    Debug.WriteLine("TARGET");
                //}
#endif

                if(appWindowViewModels.Count == 1) {
                    AppWindowViewModel appWindowViewModel = appWindowViewModels.First();
                    if(appWindowViewModel.IsLaunchCommand) {
                        Process.Start(appWindowViewModel.LaunchCommand);
                    }
                    else if(!forceShowWindow) {
                        // #bug? If there is only one app, still show the main window?
                        appWindowViewModel.AppWindow.SwitchToLastVisibleActivePopup();

                        _hotkeyFilteredWindowList = null; // #gotcha Without this, next ALT+Space will still be filtered (to just one app) and immediately hide. #bug But if cleared here, closing already filtered windows doesn't work. #warning DON'T also modify _targetWindowListBackup.

                        HideMainWindow();
                    }
                }
                else {
                    // [!] If true, don't immediately switch.

                    // Update the indexing (with proper highlighting).
                    for(int i0 = 0, i1 = 1; i0 < appWindowViewModels.Count; i0++, i1++) {
                        AppWindowViewModel appWindowViewModel = appWindowViewModels[i0];
                        appWindowViewModel.OrdinalMapped1 = new XamlHighlighter().Highlight(new[] { new StringPart(i1.Get1BasedIndexString()) });

                        string letterBound = (string.IsNullOrEmpty(letter) || i0 == 0 || appWindowViewModel.IsLaunchCommand)
                            ? appWindowViewModel.LetterMapped
                            // [!] Clear the letter, e.g. only the first app in list is mapped to letter (so hitting letter again to select it would work).
                            : string.Empty;

                        appWindowViewModel.LetterBound = letterBound;
                    }

                    ListBoxPrograms.DataContext = null; // #opt
                    ListBoxPrograms.DataContext = appWindowViewModels;

                    if(0 < ListBoxPrograms.Items.Count) {
                        ListBoxPrograms.SelectedItem = ListBoxPrograms.Items[0];
                    }

                    _hotkeyFilteredWindowList = null; // #gotcha Without this, next ALT+Space will still be filtered. #warning DON'T also modify _hotkeyFilteredWindowListBackup.
                }
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SwitchOrFilter)} end.");
            }
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Switch)} start...");

            if(0 < ListBoxPrograms.Items.Count) {
                AppWindowViewModel appWindowViewModel = (AppWindowViewModel)(ListBoxPrograms.SelectedItem ?? ListBoxPrograms.Items[0]);
                if(appWindowViewModel.IsLaunchCommand) {
                    Process.Start(appWindowViewModel.LaunchCommand);
                }
                else {
                    appWindowViewModel.AppWindow.SwitchToLastVisibleActivePopup();
                }
            }

            // #bug? What if above didn't find any apps? Still hide?
            HideMainWindow();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Switch)} end.");
        }

        /// <summary>
        /// Hides the main TaskSpace window.
        /// </summary>
        private void HideMainWindow() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(HideMainWindow)} start...");

            if(_windowCloser != null) {
                _windowCloser.Dispose();
                _windowCloser = null;
            }

            _isSearchEnabled = false;
            //_altTabAutoSwitch = false;
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(HideMainWindow)} end.");
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Dispose)} start...");

            Dispose(true);
            GC.SuppressFinalize(this);

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Dispose)} end.");
        }

        /// <summary>
        /// Disposes of managed and unmanaged resources.
        /// </summary>
        /// <param name="shouldDispose">Indicates whether the resources should be disposed.</param>
        protected virtual void Dispose(
            bool shouldDispose
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Dispose)}({shouldDispose}) start...");

            try {
                if(_isDisposed) {
                    return;
                }

                if(shouldDispose) {
                    // Dispose managed resources here.
                    if(_notifyIcon != null) {
                        _notifyIcon.Dispose();
                        _notifyIcon = null;
                    }

                    if(_hotkeyMain != null) {
                        _hotkeyMain.Dispose();
                        _hotkeyMain = null;
                    }

                    if(_hotkeyAlt != null) {
                        _hotkeyAlt.Dispose();
                        _hotkeyAlt = null;
                    }
                }

                // Free unmanaged resources (if any) here.
                _isDisposed = true;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Dispose)} end.");
            }
        }
        #endregion Methods

        #region Right-Click Menu Functions
        /// <summary>
        /// Shows the Options dialog.
        /// </summary>
        private void Options() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Options)} start...");

            if(_optionsWindow != null) {
                _optionsWindow.Activate();
            }
            else {
                _optionsWindow = new OptionsWindow {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                _optionsWindow.Closed += (sender, args) => _optionsWindow = null;
                _optionsWindow.ShowDialog();
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Options)} end.");
        }

        /// <summary>
        /// Shows the About dialog.
        /// </summary>
        private void About() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(About)} start...");

            if(_aboutWindow != null) {
                _aboutWindow.Activate();
            }
            else {
                _aboutWindow = new AboutWindow {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                _aboutWindow.Closed += (sender, args) => _aboutWindow = null;
                _aboutWindow.ShowDialog();
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(About)} end.");
        }

        /// <summary>
        /// Quits the app.
        /// </summary>
        private static void Quit() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Quit)} start...");

            Application.Current.Shutdown();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(Quit)} end.");
        }
        #endregion Right-Click Menu Functions

        #region Event Handlers
        // The main hotkey pressed, e.g. ALT+Space.
        private void hotkeyMain_HotkeyPressed(
            object sender
            , EventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(hotkeyMain_HotkeyPressed)} start...");

            try {
                //_isLocked = true; // [cut]
                if(!Settings.Default.EnableHotkeyMain) {
                    return;
                }

                if(Visibility != Visibility.Visible) {
                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: Not visible => Show...");
                    _foregroundWindow = SystemWindow.ForegroundWindow;
                    Show();

                    Activate();
                    //_mainWindowActiveDateTime = DateTime.UtcNow; // [cut]?

                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: After _mainWindowActiveDateTime is set to ```{_mainWindowActiveDateTime:u}``` (1212).");

                    //Keyboard.Focus(textboxSearch); // #cut?

                    // [!]
                    PopulateWindowList(initialFocus: InitialFocus.NextItem);
                    Opacity = 1;
                }
                else {
                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: Visible and hotkey pressed again => Search...");

                    if(IS_ALT_SPACE_ALT_SPACE_ENABLED) {
                        Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} before 1114...");
                        Switch();
                    }
                    else {
                        _foregroundWindow = SystemWindow.ForegroundWindow;
                        Show();

                        Activate();
                        _mainWindowActiveDateTime = DateTime.UtcNow;
                        Debug.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: After _mainWindowActiveDateTime is set to ```{_mainWindowActiveDateTime:u}``` (1233).");

                        PopulateWindowList(initialFocus: InitialFocus.CurrentItem);
                        Opacity = 1;
                        ToggleSearch(true);
                    }
                }
            }
            finally {
                //_isLocked = false;
                //Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(hotkeyMain_HotkeyPressed)} end. _isLocked=```{_isLocked}```.");
            }
        }

        // The alternate hotkey pressed, e.g. ALT+` (below tilde).
        private void hotkeyAlt_HotkeyPressed(
            object sender
            , EventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(hotkeyAlt_HotkeyPressed)} start...");

            try {
                _isLocked = true;
                if(!Settings.Default.EnableHotkeyAlt) {
                    return;
                }

                if(Visibility != Visibility.Visible) {
                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyAlt_HotkeyPressed)} :: Not visible => Show...");

                    _foregroundWindow = SystemWindow.ForegroundWindow;
                    Show();

                    Activate();
                    _mainWindowActiveDateTime = DateTime.UtcNow;
                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: After _mainWindowActiveDateTime is set to ```{_mainWindowActiveDateTime:u}``` (1266).");

                    //Keyboard.Focus(textboxSearch); // #cut?

                    // [!]
                    PopulateWindowList(initialFocus: InitialFocus.NextItem, isSingleAppModeEnabled: true);
                    Opacity = 1;
                }
                else {
                    Debug.WriteLine($"DEBUG :: {nameof(hotkeyAlt_HotkeyPressed)} :: #nop?...");

                    // #old
                    //HideMainWindow();
                    //_mainWindowActiveDateTime = DateTime.MinValue;
                    //Debug.WriteLine($"DEBUG :: {nameof(hotkeyAlt_HotkeyPressed)} :: After _mainWindowActiveDateTime is set to ```{_mainWindowActiveDateTime:u}``` (1280).");
                }
            }
            finally {
                _isLocked = false;
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(hotkeyAlt_HotkeyPressed)} end. _isLocked=```{_isLocked}```.");
            }
        }

        private void AltTabPressed(
            object sender
            , AltTabHookEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(AltTabPressed)} start...");

            try {
                if(!Settings.Default.AltTabHook) {
                    // Ignore ALT+Tab presses if the hook is not activated by the user.
                    return;
                }

                e.Handled = true;

                if(Visibility != Visibility.Visible) {
                    //textboxSearch.IsEnabled = true;

                    _foregroundWindow = SystemWindow.ForegroundWindow;

                    ActivateAndFocusMainWindow();

                    Keyboard.Focus(TextBoxSearch);
                    if(e.ShiftDown) {
                        // [!]
                        PopulateWindowList(initialFocus: InitialFocus.PreviousItem);
                    }
                    else {
                        // [!]
                        PopulateWindowList(initialFocus: InitialFocus.NextItem);
                    }

                    //if(Settings.Default.AutoSwitch && !e.CtrlDown) {
                    //    //_altTabAutoSwitch = true;
                    //    tb.IsEnabled = false;
                    //    tb.Text = "Press Alt + S to search";
                    //}

                    this.Opacity = 1;
                }
                else {
                    if(e.ShiftDown) {
                        PreviousItem();
                    }
                    else {
                        NextItem();
                    }
                }
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(AltTabPressed)} end.");
            }
        }

        // #gotcha The 3rd party apps like TaskSpace might not have the "right" to manage which app is in the foreground,
        // and pressing ALT key might be hack for this.
        // #bug? This might be why the previous foreground window goes to the last Z-order place?
        private void ActivateAndFocusMainWindow() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ActivateAndFocusMainWindow)} start...");

            // What happens below looks a bit weird, but for TaskSpace to get focus when using the Alt+Tab hook,
            // it is needed to simulate an Alt keypress which will bring TaskSpace to the foreground.
            // Otherwise TaskSpace will become the foreground window, but the previous window will retain focus, and keep getting the keyboard input.
            // #link http://www.codeproject.com/Tips/76427/How-to-bring-window-to-top-with-SetForegroundWindo

            IntPtr mainWindowHandle = new WindowInteropHelper(this).Handle;
            AppWindow mainWindow = new(mainWindowHandle);

#pragma warning disable CA1416 // Validate platform compatibility.
            KeyboardKey keyboardKeyAlt = new(Keys.Alt);
#pragma warning restore CA1416 // Validate platform compatibility.
            bool altKeyPressed = false;

            // Press the ALT key if it is not already being pressed.
            if((keyboardKeyAlt.AsyncState & 0x8000) == 0) {
                keyboardKeyAlt.Press();
                altKeyPressed = true;
            }

            // Bring the main window to the foreground.
            Show();
            SystemWindow.ForegroundWindow = mainWindow;

            Activate();
            _mainWindowActiveDateTime = DateTime.UtcNow;
            Debug.WriteLine($"DEBUG :: {nameof(ActivateAndFocusMainWindow)} :: After _mainWindowActiveDateTime is set to ```{_mainWindowActiveDateTime:u}``` (1372).");

            // Release the ALT key if it was pressed above. #todo Is this why sometimes the app is immediately switched on ALT+Space?
            if(altKeyPressed) {
                keyboardKeyAlt.Release();
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ActivateAndFocusMainWindow)} end.");
        }

        private void TextChanged(
            object sender
            , TextChangedEventArgs args
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(TextChanged)} start...");

            try {
                if(!_isSearchEnabled) {
                    return;
                }

                string query = TextBoxSearch.Text;
                if(string.IsNullOrEmpty(query)) {
                    // Back to the starting point (eg now can immediately switch to app 1 etc).
                    _isSearchEnabled = false;
                    TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret again doesn't show.
                }

                // [!] Below code is required both for _isSearchEnabled true and false (i.e. back to no filtering).
                WindowFilterContext<AppWindowViewModel> windowFilterContext = new() {
                    Windows = _unfilteredWindowList,
                    ForegroundWindowProcessTitle = new AppWindow(_foregroundWindow.HWnd).ProcessName
                };

                List<FilterResult<AppWindowViewModel>> filterResults = new WindowFilterer().Filter(windowFilterContext, query).ToList();

                for(int i0 = 0, i1 = 1; i0 < filterResults.Count; i0++, i1++) {
                    FilterResult<AppWindowViewModel> filterResult = filterResults[i0];
                    filterResult.AppWindow.FormattedTitle = GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                    filterResult.AppWindow.FormattedProcessTitle = GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
                    filterResult.AppWindow.OrdinalMapped1 = new XamlHighlighter().Highlight(new[] { new StringPart(i1.Get1BasedIndexString()) });
                }

                _searchFilteredWindowList = new ObservableCollection<AppWindowViewModel>(filterResults.Select(r => r.AppWindow));
                ListBoxPrograms.DataContext = _searchFilteredWindowList;

                if(0 < ListBoxPrograms.Items.Count) {
                    // If filtering, select the first best match, otherwise select the current app (instead of trying to maybe backspace to 2nd next app).
                    ListBoxPrograms.SelectedItem = ListBoxPrograms.Items[0];
                }
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(TextChanged)} end.");
            }
        }

        private static string GetFormattedTitleFromBestResult(
            IList<MatchResult> matchResults
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetFormattedTitleFromBestResult)} start...");

            try {
                MatchResult bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
                return new XamlHighlighter().Highlight(bestResult.StringParts);
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(GetFormattedTitleFromBestResult)} end.");
            }
        }

        private void SwitchToWindow(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SwitchToWindow)} start...");

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} before 1303...");
            Switch();
            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SwitchToWindow)} end.");
        }

        private void ListBoxItem_MouseLeftButtonUp(
            object sender
            , MouseButtonEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ListBoxItem_MouseLeftButtonUp)} start...");

            if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ToggleSearch)} before 1315...");
                Switch();
            }

            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ListBoxItem_MouseLeftButtonUp)} end.");
        }

        /// <summary>
        /// Launches the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LaunchWindowAsync(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LaunchWindowAsync)} start...");

            try {
                // Check if the main window is visible before handling the hotkey (i.e. user might have cancelled via Esc).
                if(this.Visibility != Visibility.Visible) {
                    e.Handled = true;
                    return;
                }

                // #TODO Actually, should be populated with Settings data even if there is no active mapped app.
                List<AppWindowViewModel> hotkeyFilteredWindowList = GetSwitchableWindows(_lastKeyPressed, out _);
                if(hotkeyFilteredWindowList == null
                    || hotkeyFilteredWindowList.Count == 0
                    || string.IsNullOrWhiteSpace(hotkeyFilteredWindowList.First().AppFilePath)
                ) {
                    e.Handled = true;
                    return;
                }

                // Spawn new process.
                // #BUG Should fall-back on the first app in the settings, NOT first active mapped app.
                Process.Start(hotkeyFilteredWindowList.First().AppFilePath);

                // #TODO Preferentially launch current row if applicable?

                e.Handled = true;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LaunchWindowAsync)} end.");
            }
        }

        /// <summary>
        /// Launches the admin window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LaunchAdminWindowAsync(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LaunchAdminWindowAsync)} start...");

            try {
                // Check if the main window is visible before handling the hotkey (i.e. user might have cancelled via Esc).
                if(this.Visibility != Visibility.Visible) {
                    e.Handled = true;
                    return;
                }

                // #TODO Actually, should be populated with Settings data even if there is no active mapped app.
                List<AppWindowViewModel> hotkeyFilteredWindowList = GetSwitchableWindows(_lastKeyPressed, out _);
                if(hotkeyFilteredWindowList == null
                    || hotkeyFilteredWindowList.Count == 0
                    || string.IsNullOrWhiteSpace(hotkeyFilteredWindowList.First().AppFilePath)
                ) {
                    e.Handled = true;
                    return;
                }

                ProcessStartInfo processInfo = new() {
                    FileName = hotkeyFilteredWindowList.First().AppFilePath, // Path to the application.
                    UseShellExecute = true, // Use shell execute to enable running as admin.
                    Verb = "runas" // Specifies to run the process as an administrator.
                };

                try {
                    // #BUG Should fall-back on the first app in the settings, NOT first active mapped app.
                    // Spawn new admin process.
                    Process.Start(processInfo);

                    // #TODO Preferentially launch current row if applicable?
                }
                catch(System.ComponentModel.Win32Exception) {
                    // This exception will occur if the user cancels the UAC prompt.
                }

                e.Handled = true;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(LaunchAdminWindowAsync)} end.");
            }
        }

        /// <summary>
        /// Closes the window.
        /// #note If an app-window is closed and there is only one remaining app-window in TaskSpace main window
        /// (either in unfiltered or filtered list), the remaining app-window will be auto-selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CloseWindowAsync(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CloseWindowAsync)} start...");

            try {
                // Check if the main window is visible before handling the hotkey (i.e. user might have cancelled via Esc).
                if(this.Visibility != Visibility.Visible) {
                    e.Handled = true;
                    return;
                }

                if(ListBoxPrograms.Items.Count <= 0) {
                    // All windows are already closed, so hide the main TaskSpace window.
                    HideMainWindow();
                    e.Handled = true;
                    return;
                }

                List<AppWindowViewModel> hotkeyFilteredWindowList = GetSwitchableWindows(_lastKeyPressed, out _);
                if(hotkeyFilteredWindowList == null
                    || hotkeyFilteredWindowList.Count == 0
                    || string.IsNullOrWhiteSpace(hotkeyFilteredWindowList.First().AppFilePath)
                ) {
                    e.Handled = true;
                    return;
                }

                await CloseWindowAsync(hotkeyFilteredWindowList.First());
                //if(hotkeyFilteredWindowList.Count == 1) {
                //    await CloseWindowAsync(hotkeyFilteredWindowList.First());
                //}
                //else {
                //    // #TODO# If currently selected row matches this app, close it preferentially. #OR Gradual close (first filter).
                //}

                e.Handled = true;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CloseWindowAsync)} end.");
            }
        }

        /// <summary>
        /// Kills the window.
        /// #note If an app-window is killed and there is only one remaining app-window in TaskSpace main window
        /// (either in unfiltered or filtered list), the remaining app-window will be auto-selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KillWindowAsync(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(KillWindowAsync)} start...");

            try {
                // Check if the main window is visible before handling the hotkey (i.e. user might have cancelled via Esc).
                if(this.Visibility != Visibility.Visible) {
                    e.Handled = true;
                    return;
                }

                if(ListBoxPrograms.Items.Count <= 0) {
                    // All windows are already closed, so hide the main TaskSpace window.
                    HideMainWindow();
                    e.Handled = true;
                    return;
                }

                List<AppWindowViewModel> hotkeyFilteredWindowList = GetSwitchableWindows(_lastKeyPressed, out _);
                if(hotkeyFilteredWindowList == null
                    || hotkeyFilteredWindowList.Count == 0
                    || string.IsNullOrWhiteSpace(hotkeyFilteredWindowList.First().AppFilePath)
                ) {
                    e.Handled = true;
                    return;
                }

                await CloseWindowAsync(hotkeyFilteredWindowList.First());
                //if(hotkeyFilteredWindowList.Count == 1) {
                //    await CloseWindowAsync(hotkeyFilteredWindowList.First());
                //}
                //else {
                //    // #TODO# If currently selected row matches this app, close it preferentially. #OR Gradual close (first filter).
                //}

                e.Handled = true;
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(KillWindowAsync)} end.");
            }
        }

        private async Task CloseWindowAsync(
            AppWindowViewModel appWindowViewModel
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CloseWindowAsync)} start...");

            bool isClosed = await _windowCloser.TryCloseAsync(appWindowViewModel);
            if(isClosed) {
                RemoveClosedWindow(appWindowViewModel);
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(CloseWindowAsync)} end.");
        }

        private async Task KillWindowAsync(
            AppWindowViewModel appWindowViewModel
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(KillWindowAsync)} start...");

            bool isClosed = await _windowCloser.TryKillAsync(appWindowViewModel);
            if(isClosed) {
                RemoveClosedWindow(appWindowViewModel);
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(KillWindowAsync)} end.");
        }

        private void RemoveClosedWindow(
            AppWindowViewModel appWindowViewModel
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(RemoveClosedWindow)} start...");

            try {
                // Track the current selected index.
                int currentSelectedIndex = ListBoxPrograms.SelectedIndex;

                // Get the currently selected window (to check if it was the one being removed).
                AppWindowViewModel currentlySelectedWindow = ListBoxPrograms.SelectedItem as AppWindowViewModel;

                // #note The _hotkeyFilteredWindowList will likely always be null due to logic where it's set to null in some cases.
                // Use _hotkeyFilteredWindowListBackup if _hotkeyFilteredWindowList is null to work with a valid list.
                List<AppWindowViewModel> hotkeyFilteredWindowList = _hotkeyFilteredWindowList ?? _hotkeyFilteredWindowListBackup;
                bool isHotkeyFilteredWindowMode = hotkeyFilteredWindowList != null;

                List<AppWindowViewModel> windowList = isHotkeyFilteredWindowMode
                    ? hotkeyFilteredWindowList
                    : _unfilteredWindowList;

                int windowIndex = windowList.IndexOf(appWindowViewModel);
                if(0 <= windowIndex) {
                    // Remove the window from the window list (either hotkeyFilteredWindowList or _unfilteredWindowList).
                    windowList.Remove(appWindowViewModel);

                    if(isHotkeyFilteredWindowMode) {
                        // If in filter mode, also remove the window from the unfiltered window list.
                        _unfilteredWindowList.Remove(appWindowViewModel);
                    }
                }

                if(windowList.Count == 0) {
                    // All windows are already closed, so hide the main TaskSpace window.
                    HideMainWindow();
                    return;
                }

                if(isHotkeyFilteredWindowMode) {
                    // Backup the first item LetterBound and LetterMapped (should both be populated).
                    string firstItemLetterBound = windowList.First().LetterBound;
                    string firstItemLetterMapped = windowList.First().LetterMapped;

                    // Re-initialize digit/letter mappings (with proper highlighting).
                    for(int i0 = 0, i1 = 1; i0 < windowList.Count; i0++, i1++) {
                        windowList[i0].OrdinalMapped1 = new XamlHighlighter().Highlight(new[] { new StringPart(i1.Get1BasedIndexString()) });

                        if(i0 == 0) {
                            // Reassign the hotkey letter only to the first window in the list.
                            windowList[i0].LetterBound = firstItemLetterBound;
                            windowList[i0].LetterMapped = firstItemLetterMapped;
                        }
                    }
                }
                else {
                    for(int i0 = 0, i1 = 1; i0 < windowList.Count; i0++, i1++) {
                        // In unfiltered mode, reassign only the ordinal index, not the letter bindings (with proper highlighting).
                        windowList[i0].OrdinalMapped1 = new XamlHighlighter().Highlight(new[] { new StringPart(i1.Get1BasedIndexString()) });
                    }
                }

                // Reinitialize the data context of the ListBox to ensure the UI is updated.
                ListBoxPrograms.DataContext = null; // Rebind the data context to reflect changes.
                ListBoxPrograms.DataContext = windowList;

                // Handle selection.
                if(ListBoxPrograms.Items.Count <= 0) {
                    // If the list is empty, clear the selection.
                    ListBoxPrograms.SelectedIndex = -1;
                }
                else if(currentlySelectedWindow == appWindowViewModel) {
                    // If the closed window was the selected one, select the next available one.
                    if(currentSelectedIndex < ListBoxPrograms.Items.Count) {
                        ListBoxPrograms.SelectedIndex = currentSelectedIndex;
                    }
                    else {
                        ListBoxPrograms.SelectedIndex = ListBoxPrograms.Items.Count - 1;
                    }
                }
                else {
                    // If the closed window wasn't the selected one, preserve the current selection.
                    ListBoxPrograms.SelectedItem = currentlySelectedWindow;
                }

                // Bring the selected item into view.
                ListBoxPrograms.ScrollIntoView(ListBoxPrograms.SelectedItem);
            }
            finally {
                Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(RemoveClosedWindow)} end.");
            }
        }

        private void ScrollListUp(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListUp)} start...");

            PreviousItem();
            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListUp)} end.");
        }

        private void PreviousItem() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(PreviousItem)} start...");

            if(0 < ListBoxPrograms.Items.Count) {
                if(ListBoxPrograms.SelectedIndex != 0) {
                    --ListBoxPrograms.SelectedIndex;
                }
                else {
                    // Jump to last item.
                    ListBoxPrograms.SelectedIndex = ListBoxPrograms.Items.Count - 1;
                }

                ScrollSelectedItemIntoView();
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(PreviousItem)} end...");
        }

        private void ScrollListDown(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListDown)} start...");

            NextItem();
            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListDown)} end.");
        }

        private void NextItem() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(NextItem)} start...");

            if(0 < ListBoxPrograms.Items.Count) {
                if(ListBoxPrograms.SelectedIndex != ListBoxPrograms.Items.Count - 1) {
                    ListBoxPrograms.SelectedIndex++;
                }
                else {
                    // Jump to first item.
                    ListBoxPrograms.SelectedIndex = 0;
                }

                ScrollSelectedItemIntoView();
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(NextItem)} end.");
        }

        private void ScrollSelectedItemIntoView() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollSelectedItemIntoView)} start...");

            object selectedItem = ListBoxPrograms.SelectedItem;
            if(selectedItem != null) {
                ListBoxPrograms.ScrollIntoView(selectedItem);
            }

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollSelectedItemIntoView)} end.");
        }

        private void ScrollListHome(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListHome)} start...");

            ListBoxPrograms.SelectedIndex = 0;
            ScrollSelectedItemIntoView();

            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListHome)} end.");
        }

        private void ScrollListEnd(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListEnd)} start...");

            ListBoxPrograms.SelectedIndex = ListBoxPrograms.Items.Count - 1;
            ScrollSelectedItemIntoView();

            e.Handled = true;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(ScrollListEnd)} end.");
        }

        private void MainWindow_Deactivated(
            object sender
            , EventArgs e
        ) {
            // #BUG Why is this activating right away?

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_Deactivated)} start...");

            HideMainWindow();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_Deactivated)} end.");
        }

        private void MainWindow_OnLoaded(
            object sender
            , RoutedEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_OnLoaded)} start...");

            DisableSystemMenu();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(MainWindow_OnLoaded)} end.");
        }

        private void DisableSystemMenu() {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(DisableSystemMenu)} start...");

            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            SystemWindow systemWindow = new(windowHandle);
            systemWindow.Style &= ~WindowStyleFlags.SYSMENU;

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(DisableSystemMenu)} end.");
        }

        private void SearchIcon_OnPreviewMouseDown(
            object sender
            , MouseButtonEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SearchIcon_OnPreviewMouseDown)} start...");

            // Toggle.
            ToggleSearch();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(SearchIcon_OnPreviewMouseDown)} end.");
        }

        private void OnClose(
            object sender
            , System.ComponentModel.CancelEventArgs e
        ) {
            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(OnClose)} start...");

            // Hide the main window (this is NOT shutdown or exit).
            e.Cancel = true;
            HideMainWindow();

            Debug.WriteLine($"TRACE :: {nameof(MainWindow)}.{nameof(OnClose)} end.");
        }
        #endregion Event Handlers
    }
}
