using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem; // #note MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see "https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls".
//using MenuItem = System.Windows.Forms.MenuItem; // #warning .NET Framework 4.8- only.
using MessageBox = System.Windows.MessageBox;

namespace TaskSpace {
    public partial class MainWindow : System.Windows.Window {
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
        /// <summary>
        /// The SettingsCharToAppList value. Static, i.e. loaded once on startup.
        /// </summary>
        public Dictionary<char, List<string>> SettingsCharToAppList { get; private set; } = new Dictionary<char, List<string>>();

        /// <summary>
        /// Contains the value when the main window has been activated.
        /// Used for better detection if Space key was part of the original trigger (even if ALT is claimed not to be held anymore).
        /// </summary>
        private DateTime? _mainWindowActiveDateTime { get; set; }

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

        private List<List<string>> _folderFiles = new List<List<string>>();

        private List<AppWindowViewModel> _targetWindowList = null; // #note These are the windows returned by indexing of alphabetic targeting.
        private List<AppWindowViewModel> _unfilteredWindowList;

        private WindowCloser _windowCloser;
        private ObservableCollection<AppWindowViewModel> _filteredWindowList;
        private NotifyIcon _notifyIcon;
        private HotKey _hotkeyMain; // Main hotkey. ALT+Space default.
        private HotKey _hotkeyAlt; // Alternate hotkey. ALT+` default.

        public static readonly RoutedUICommand CloseWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand SwitchToWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListDownCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListUpCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListHomeCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListEndCommand = new RoutedUICommand();

        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;
        //private bool _altTabAutoSwitch; // #todo What does this do? Maybe something where the search box is focused? Or maybe something with quick single button hits? Or takes-over ALT+Tab? Or does ALT+Tab immediate switch, but using ALT+Space?
        //private bool _isSorted = false;

        private Theme _theme;
        #endregion Properties

        #region Constructor
        public MainWindow() {
#if DEBUG
            // Allocate console for Console.WriteLine calls.
            //AllocConsole();
#endif

            InitializeComponent();

            SetUpKeyBindings();

            SetUpNotifyIcon();

            SetUpHotKeys(); // Set up default ALT+Space and ALT+`.

            SetUpAltTabHook();

            //CheckForUpdates(); // #cut

            _theme = new Theme(this);

            this.Opacity = 0;
        }
        #endregion Constructor

        #region Private Methods
        // #todo!!! Could add the main task launching here, e.g. check "1", "V", etc.
        // If "_" is hit, then switch to the search mode (but could backspace back to the main mode).
        private void SetUpKeyBindings() {
            string letter = string.Empty;

            // Enter and Esc bindings are not executed before the keys have been released.
            // This is done to prevent that the window being focused after the key presses to get 'KeyUp' messages.
            KeyDown += (sender, args) => {
                // Opacity is set to 0 right away so it appears that action has been taken right away...
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
                    // #todo But what if the app is not mapped?
                    // e.g. hit 9, but there are no 9 apps? Or hit V, but this is not mapped directly to anything?
                    // Then, automatically switch to search mode, e.g. type " " (to be consistent with search mode), then type 9 or V.

                    // Do determine this here (ie DON'T hide the app if the above search was actually executed).

                    // #todo!!! Should follow the same schema as below.
                    if(args.Key == Key.OemTilde) { // #todo Make sure that there are no modifiers?
                        // #todo _targetWindowList = GetPowerMenuOptions();
                        AppWindowViewModel targetWindow = GetSwitchableWindows(args.Key, out letter).FirstOrDefault();
                        if(targetWindow != null) {
                            _launchCommand = targetWindow.LaunchCommand;
                            this.Opacity = 0; // Disappear the main UI window.
                        }
                    }
                    else if((Key.D0 <= args.Key && args.Key <= Key.D9)
                        || (Key.NumPad0 <= args.Key && args.Key <= Key.NumPad9)
                        || (Key.F1 <= args.Key && args.Key <= Key.F24)
                        || (Key.A <= args.Key && args.Key <= Key.Z)
                    ) { // #todo Make sure that there are no modifiers?
                        _targetWindowList = GetSwitchableWindows(args.Key, out letter);
                        if(_targetWindowList != null && _targetWindowList.Count == 1) {
                            this.Opacity = 0;
                        }
                    }
                }
            };

            // #warning DON'T use variables from outer scope (unless global).
            KeyUp += (sender, args) => {
                // ... But only when the keys are released, the action is actually executed.
                if(args.Key == Key.Space) {
#if DEBUG
                    //Console.WriteLine($"DEBUG :: Space detected.");
#endif

                    bool shouldSwitch = false;
                    if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                        // #nop This is likely the current main-window launch.
#if DEBUG
                        // #todo Never hit?
                        Console.WriteLine($"DEBUG :: NOP :: ALT+Space detected.");
#endif

                        if(IS_ALT_SPACE_ALT_SPACE_ENABLED) {
                            TimeSpan? timeSpanDiff = DateTime.UtcNow - _mainWindowActiveDateTime;
                            if(timeSpanDiff < SHOW_TIMEOUT) {
                                // #nop This is likely the current main-window launch (even though ALT is not detected).
#if DEBUG
                                Console.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space then released => Initial launch...");
#endif
                            }
                            else {
#if DEBUG
                                Console.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space (UI launched), then Space => Switch...");
#endif

                                // #todo Check flags like Control etc.
                                shouldSwitch = true;
                            }
                        }
                    }
                    else if(_isMainWindowActive) {
                        if(!IS_ALT_SPACE_ALT_SPACE_ENABLED || IS_ALT_SPACE_SPACE_ENABLED) {
                            TimeSpan? timeSpanDiff = DateTime.UtcNow - _mainWindowActiveDateTime;
                            if(timeSpanDiff < SHOW_TIMEOUT) {
                                // #nop This is likely the current main-window launch (even though ALT is not detected).
#if DEBUG
                                Console.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space then released => Initial launch...");
#endif
                            }
                            else {
#if DEBUG
                                Console.WriteLine($"DEBUG :: ALT not detected, but this was likely ALT+Space (UI launched), then Space => Switch...");
#endif

                                // #todo Check flags like Control etc.
                                shouldSwitch = true;
                            }
                        }
                    }

                    if(shouldSwitch) {
                        Switch(); // Switch to currently selected app (i.e. treat Space as Enter).
                    }
                }
                else if(args.Key == Key.Back) {
                    _targetWindowList = _unfilteredWindowList;
                    Switch(_targetWindowList, letter: string.Empty);
                }
                // #note This might not have all the combinations from above, e.g. ALT+S.
                else if(args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    Switch();
                }
                else if(args.Key == Key.Escape) {
                    HideWindow();
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
                else if(!_isSearchEnabled && _targetWindowList != null) {
                    Switch(_targetWindowList, letter);
                }

                _launchCommand = string.Empty;
            };
        }

        /// <summary>
        /// isSearchEnabled:
        ///     - null :: Toggle.
        ///     - true :: Toggle search to true.
        ///     - false :: Toggle search to false.
        /// </summary>
        /// <param name="isSearchEnabled"></param>
        private void ToggleSearch(bool? isSearchEnabled = null) {
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
        }

        private void SetUpHotKeys() {
            _hotkeyMain = new HotKey();
            _hotkeyMain.LoadSettings();

            _hotkeyAlt = new HotKey();
            _hotkeyAlt.LoadSettings2();

            Application.Current.Properties["hotkeyMain"] = _hotkeyMain;
            Application.Current.Properties["hotkeyAlt"] = _hotkeyAlt;

            _hotkeyMain.HotkeyPressed += hotkeyMain_HotkeyPressed; // ALT+Space default.
            try {
                _hotkeyMain.Enabled = Settings.Default.EnableHotKey;
            }
            catch(HotkeyAlreadyInUseException) {
                string boxText = "The current hotkey for activating TaskSpace is in use by another program."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "You can change the hotkey by right-clicking the TaskSpace icon in the system tray and choosing 'Options'.";
                MessageBox.Show(boxText, "Hotkey already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _hotkeyAlt.HotkeyPressed += hotkeyAlt_HotkeyPressed; // ALT+` default.
            try {
                _hotkeyAlt.Enabled = Settings.Default.EnableHotKey2;
            }
            catch(HotkeyAlreadyInUseException) {
                string boxText = "The current hotkey2 for activating TaskSpace is in use by another program."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "You can change the hotkey by right-clicking the TaskSpace icon in the system tray and choosing 'Options'.";
                MessageBox.Show(boxText, "Hotkey2 already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetUpAltTabHook() {
            _altTabHook = new AltTabHook();
            _altTabHook.Pressed += AltTabPressed;
        }

        private void SetUpNotifyIcon() {
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
        }

        //private static void RunOnStartup(MenuItem menuItem) { // #warning .NET Framework 4.8-.
        private static void RunOnStartup(ToolStripMenuItem toolStripMenuItem) {
            try {
                AutoStart autoStart = new AutoStart {
                    IsEnabled = !toolStripMenuItem.Checked
                };

                toolStripMenuItem.Checked = autoStart.IsEnabled;
            }
            catch(AutoStartException e) {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Populates the window list with the current running windows, creating the mappings.
        /// #future #perf Could map from the settings once and append only automatic mappings on every LoadData call.
        /// </summary>
        /// <param name="focus">The focus value, i.e. Previous/Current/Next.</param>
        /// <param name="isSingleAppModeEnabled">[opt; default is false] Activates the single-app-mode, i.e. the UI is filtered to a single (current) app instances.</param>
        private void LoadData(
            InitialFocus focus
            , bool isSingleAppModeEnabled = false
        ) {
            // Restore searchbox defaults.
            TextBoxSearch.Background = _theme.Background;
            SearchIcon.Opacity = 0.4;

            _isMainWindowActive = true;

            TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret again doesn't show.

            _mainWindowActiveDateTime = DateTime.UtcNow;

            // #future Should be done once on startup, not on every LoadData call (accept as param).
            // Clear the previous mappings (some previous apps might be closed now).
            this.SettingsCharToAppList = new Dictionary<char, List<string>>();

            // Initialize CharToAppsList dictionary using the Settings.
            for(int i = (int)'A'; i <= (int)'Z'; i++) {
                char letter = (char)i;
                string propertyName = $"Apps_{letter}";

                // Use reflection to get the property by name.
                PropertyInfo propertyInfo = Properties.Settings.Default.GetType().GetProperty(propertyName);

                List<string> appsList = new List<string>();
                if(propertyInfo == null) {
                    continue;
                }

                // Get the value of the property.
                object value = propertyInfo.GetValue(Properties.Settings.Default, null);

                // Check if the value is a StringCollection.
                if(value is StringCollection collection) {
                    // Convert the StringCollection to List<string>.
                    appsList = collection
                        .Cast<string>()
                        .Select(x => x.ToLower())
                        .ToList();
                }

                this.SettingsCharToAppList[letter] = appsList;
            }

            _unfilteredWindowList = new WindowFinder()
                // #todo This should be renamed to GetAndMapWindows and instead of returning
                // AppWindow, return AppWindowViewModel (already mapped to static/dynamic letter).
                .GetAndMapWindows(Properties.Settings.Default.BlockList.Cast<string>().ToList(), this.SettingsCharToAppList, this._isAutoMappingEnabled)
                .ToList();

            AppWindowViewModel firstWindow = _unfilteredWindowList.FirstOrDefault();

            bool isForegroundWindowMovedToBottom = false;

            // Index.
            for(int i0 = 0, i1 = 1; i0 < _unfilteredWindowList.Count; i0++, i1++) {
                AppWindowViewModel appWindowViewModel = _unfilteredWindowList[i0];
                appWindowViewModel.FormattedTitle = new XamlHighlighter().Highlight(new[] { new StringPart(appWindowViewModel.AppWindow.Title) });
                appWindowViewModel.FormattedProcessTitle = new XamlHighlighter().Highlight(new[] { new StringPart(appWindowViewModel.AppWindow.ProcessName) });
                appWindowViewModel.IndexString = new XamlHighlighter().Highlight(new[] { new StringPart(i1.Get1BasedIndexString()) });
            }

            // #todo!!! Could be integrated into GetSwitchableWindows (ie no filtering mode).
            bool isPowerMenuEnabled = false; // #future Move to settings.
            if(isPowerMenuEnabled) {
                List<AppWindowViewModel> windows = GetPowerMenuOptions();

                foreach(AppWindowViewModel window in windows) {
                    _unfilteredWindowList.Add(window);
                }

                //_filteredWindowList = new ObservableCollection<AppWindowViewModel>(_unfilteredWindowList); // ?
            }

            _filteredWindowList = new ObservableCollection<AppWindowViewModel>(_unfilteredWindowList);
            _windowCloser = new WindowCloser();

            ListBoxPrograms.DataContext = null;
            ListBoxPrograms.DataContext = _filteredWindowList;

            FocusItemInList(focus); //, foregroundWindowMovedToBottom); // #cut Moving to bottom isn't intuitive.

            TextBoxSearch.Clear();
            TextBoxSearch.Focus();
            TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret doesn't show (yet).

            // #buggy
            if(isSingleAppModeEnabled) {
                string letterMapped = firstWindow.LetterMapped;
                if(!string.IsNullOrEmpty(letterMapped)) {
                    _targetWindowList = GetSwitchableWindows(
                        Key.DeadCharProcessed
                        , out string letter
                        , isPowerMenuEnabled
                        , letterMapped
                    );
                    //if(_targetWindowList != null && _targetWindowList.Count == 1) {
                    //    Opacity = 0;
                    //}

                    Switch(_targetWindowList, letter);
                }
            }

            CenterWindow();
            ScrollSelectedItemIntoView();
        }

        // #experimental
        // #future Should return a list. When '`' is hit, should open the folder mapped to '`' key.
        // #future! Read the list from settings.
        private List<AppWindowViewModel> GetPowerMenuOptions() {
            throw new NotImplementedException();
        }

        //private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2) {
        //    return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        //}

        private void FocusItemInList(
            InitialFocus focus
        //, bool foregroundWindowMovedToBottom
        ) {
            if(focus == InitialFocus.PreviousItem) {
                // #note!!! Properties.Settings.Default.IsFocusNextEnabled is never applicable here.
                int previousItemIndex = ListBoxPrograms.Items.Count - 1;
                //if(foregroundWindowMovedToBottom) {
                //    --previousItemIndex;
                //}

                ListBoxPrograms.SelectedIndex = 0 < previousItemIndex
                    ? previousItemIndex
                    : 0;
            }
            else if(focus == InitialFocus.CurrentItem) {
                // #nop Keeping the current item index.
            }
            else if(focus == InitialFocus.NextItem) { // #redundant
                                                      // #todo!!! Add to visual settings with description.
                if(Properties.Settings.Default.IsFocusNextEnabled) {
                    ListBoxPrograms.SelectedIndex = 1;
                }
                else {
                    ListBoxPrograms.SelectedIndex = 0;
                }
            }

            //_lastSelectedIndex = listboxProgramName.SelectedIndex;
        }

        /// <summary>
        /// Place the TaskSpace window in the center of the screen.
        /// </summary>
        private void CenterWindow() {
            // Reset height every time to ensure that resolution changes take effect.
            Border.MaxHeight = SystemParameters.PrimaryScreenHeight;

            // Force a rendering before repositioning the window.
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Position the window in the center of the screen.
            Left = (SystemParameters.PrimaryScreenWidth / 2) - (ActualWidth / 2);
            Top = (SystemParameters.PrimaryScreenHeight / 2) - (ActualHeight / 2);
        }

        /// <summary>
        /// Gets the switchable windows of the same process.
        /// </summary>
        private List<AppWindowViewModel> GetSwitchableWindows(
            string processName
        ) {
            List<AppWindowViewModel> retVal = new List<AppWindowViewModel>();

            // #note ItemCollection is not queryable.
            foreach(AppWindowViewModel appWindowViewModel in ListBoxPrograms.Items) {
                if(appWindowViewModel.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) {
                    retVal.Add(appWindowViewModel);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the switchable windows associated with the input key code.
        /// Supports multiple different apps bound to the same letter.
        /// </summary>
        private List<AppWindowViewModel> GetSwitchableWindows(
            Key key
            , out string letter
            , bool isPowerMenuEnabled = false
            , string inputLetter = ""
        ) {
            List<AppWindowViewModel> retVal = new List<AppWindowViewModel>();
            letter = string.Empty;

            Key keyLocal = key;
            if(!string.IsNullOrEmpty(inputLetter)) {
                try {
                    keyLocal = inputLetter.ToKey();
                }
                catch {
                }
            }

            if(keyLocal == Key.OemTilde) {
                AppWindowViewModel targetWindow = null;
                foreach(object obj in ListBoxPrograms.Items) {
                    AppWindowViewModel appWindowViewModel = (AppWindowViewModel)obj;
                    if(appWindowViewModel.LetterMapped == "`") {
                        retVal.Add(appWindowViewModel);
                    }
                }
            }
            else if((Key.D0 <= keyLocal && keyLocal <= Key.D9)
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

                // #todo Get the apps from settings.

                //if(key == Key.A) {
                //    appsList = Properties.Settings.Default.Apps_A.Cast<string>().ToList();
                //}
                //else if(key == Key.E) {
                //    appsList = Properties.Settings.Default.Apps_E.Cast<string>().ToList();
                //}

                // #note ItemCollection is not queryable.
                foreach(AppWindowViewModel appWindowViewModel in ListBoxPrograms.Items) {
                    if(appWindowViewModel.LetterBound.Equals(letter, StringComparison.OrdinalIgnoreCase)) {
                        retVal.Add(appWindowViewModel);
                    }
                }

                retVal = retVal.OrderBy(x => x.LetterMappedOrdinal).ToList();
            }

            if(isPowerMenuEnabled) {
                List<AppWindowViewModel> windows = GetPowerMenuOptions();
                foreach(var window in windows) {
                    retVal.Add(window);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Switches to the window, or letter-filter if multiple instances found.
        /// Passing-in default letter means "restore unfiltered windows" (assumed that they are passed-in).
        /// </summary>
        private void Switch(
            List<AppWindowViewModel> appWindowViewModels
            , string letter = ""
            , bool forceShowWindow = false
        ) {
            if(appWindowViewModels != null) {
                if(appWindowViewModels.Count == 0) {
                    // #nop
                }
                else if(appWindowViewModels.Count == 1) {
                    AppWindowViewModel appWindowViewModel = appWindowViewModels.First();
                    if(appWindowViewModel.IsLaunchCommand) {
                        Process.Start(appWindowViewModel.LaunchCommand);
                    }
                    // [!] If true, don't immediately switch.
                    else if(!forceShowWindow) {
                        // #bug? If there is only one app, still show the main window?
                        appWindowViewModel.AppWindow.SwitchToLastVisibleActivePopup();

                        _targetWindowList = null; // #gotcha Without this, next ALT+Space will still be filtered (to just one app) and immediately hide.

                        HideWindow();
                    }
                }
                else {
                    //if(string.IsNullOrWhiteSpace(letter)) {
                    //}
                    //else if(!string.IsNullOrWhiteSpace(letter)) { // #redundant
                    // Update the indexing.
                    for(int i0 = 0, i1 = 1; i0 < appWindowViewModels.Count; i0++, i1++) {
                        AppWindowViewModel appWindowViewModel = appWindowViewModels[i0];
                        appWindowViewModel.IndexString = i1.ToString();

                        appWindowViewModel.LetterBound = (string.IsNullOrEmpty(letter) || i0 == 0 || appWindowViewModel.IsLaunchCommand)
                            ? appWindowViewModel.LetterMapped
                            // [!] Clear the letter, e.g. only the first app in list is mapped to letter (so hitting letter again to select it would work).
                            : string.Empty;
                    }

                    ListBoxPrograms.DataContext = appWindowViewModels;
                    //listboxProgramName.DataContext = _filteredWindowList;
                    if(0 < ListBoxPrograms.Items.Count) {
                        ListBoxPrograms.SelectedItem = ListBoxPrograms.Items[0];
                    }

                    _targetWindowList = null; // #gotcha Without this, next ALT+Space will still be filtered.
                }
            }
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch() {
            if(0 < ListBoxPrograms.Items.Count) {
                AppWindowViewModel win = (AppWindowViewModel)(ListBoxPrograms.SelectedItem ?? ListBoxPrograms.Items[0]);
                if(win.IsLaunchCommand) {
                    Process.Start(win.LaunchCommand);
                }
                else {
                    win.AppWindow.SwitchToLastVisibleActivePopup();
                }
            }

            // #bug? What if above didn't find any apps? Still hide?
            HideWindow();
        }

        private void HideWindow() {
            if(_windowCloser != null) {
                _windowCloser.Dispose();
                _windowCloser = null;
            }

            _isSearchEnabled = false;
            //_altTabAutoSwitch = false;
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);
        }
        #endregion Private Methods

        #region Right-Click Menu Functions
        /// <summary>
        /// Shows the Options dialog.
        /// </summary>
        private void Options() {
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
        }

        /// <summary>
        /// Shows the About dialog.
        /// </summary>
        private void About() {
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
        }

        /// <summary>
        /// Quits the app.
        /// </summary>
        private void Quit() {
            _notifyIcon.Dispose();
            _notifyIcon = null;
            _hotkeyMain.Dispose();
            Application.Current.Shutdown();
        }
        #endregion Right-Click Menu Functions

        #region Event Handlers
        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = true;
            HideWindow();
        }

        // The main hotkey pressed, e.g. ALT+Space.
        private void hotkeyMain_HotkeyPressed(object sender, EventArgs e) {
#if DEBUG
            Console.WriteLine($"TRACE :: {nameof(hotkeyMain_HotkeyPressed)}...");
#endif
            if(!Settings.Default.EnableHotKey) {
                return;
            }

            if(Visibility != Visibility.Visible) {
#if DEBUG
                Console.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: Not visible => Show...");
#endif
                _foregroundWindow = SystemWindow.ForegroundWindow;
                Show();
                Activate();
                //Keyboard.Focus(textboxSearch); // #cut?

                // [!]
                LoadData(InitialFocus.NextItem);
                Opacity = 1;
            }
            else {
#if DEBUG
                Console.WriteLine($"DEBUG :: {nameof(hotkeyMain_HotkeyPressed)} :: Visible and hotkey pressed again => Search...");
#endif

                if(IS_ALT_SPACE_ALT_SPACE_ENABLED) {
                    Switch();
                }
                else {
                    _foregroundWindow = SystemWindow.ForegroundWindow;
                    Show();
                    Activate();
                    LoadData(InitialFocus.CurrentItem);
                    Opacity = 1;
                    ToggleSearch(true);
                }
            }
        }

        // The alternate hotkey pressed, e.g. ALT+`.
        private void hotkeyAlt_HotkeyPressed(object sender, EventArgs e) {
#if DEBUG
            Console.WriteLine($"TRACE :: {nameof(hotkeyAlt_HotkeyPressed)}...");
#endif
            if(!Settings.Default.EnableHotKey2) {
                return;
            }

            if(Visibility != Visibility.Visible) {
#if DEBUG
                Console.WriteLine($"DEBUG :: {nameof(hotkeyAlt_HotkeyPressed)} :: Not visible => Show...");
#endif
                _foregroundWindow = SystemWindow.ForegroundWindow;
                Show();
                Activate();
                //Keyboard.Focus(textboxSearch); // #cut?

                // [!]
                LoadData(InitialFocus.NextItem, isSingleAppModeEnabled: true);
                Opacity = 1;
            }
            else {
#if DEBUG
                Console.WriteLine($"DEBUG :: {nameof(hotkeyAlt_HotkeyPressed)} :: #nop?...");
#endif

                // #old
                //HideWindow();
                //_mainWindowActiveDateTime = DateTime.MinValue;
            }
        }

        private void AltTabPressed(object sender, AltTabHookEventArgs e) {
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
                    LoadData(InitialFocus.PreviousItem);
                }
                else {
                    // [!]
                    LoadData(InitialFocus.NextItem);
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

        // #gotcha The 3rd party apps like TaskSpace might not have the "right" to manage which app is in the foreground,
        // and pressing ALT key might be hack for this.
        // #bug? This might be why the previous foreground window goes to the last Z-order place?
        private void ActivateAndFocusMainWindow() {
            // What happens below looks a bit weird, but for TaskSpace to get focus when using the Alt+Tab hook,
            // it is needed to simulate an Alt keypress which will bring TaskSpace to the foreground.
            // Otherwise TaskSpace will become the foreground window, but the previous window will retain focus, and keep getting the keyboard input.
            // #link http://www.codeproject.com/Tips/76427/How-to-bring-window-to-top-with-SetForegroundWindo

            IntPtr thisWindowHandle = new WindowInteropHelper(this).Handle;
            AppWindow thisWindow = new AppWindow(thisWindowHandle);

            KeyboardKey altKey = new KeyboardKey(Keys.Alt);
            bool altKeyPressed = false;

            // Press the ALT key if it is not already being pressed.
            if((altKey.AsyncState & 0x8000) == 0) {
                altKey.Press();
                altKeyPressed = true;
            }

            // Bring the main window to the foreground.
            Show();
            SystemWindow.ForegroundWindow = thisWindow;
            Activate();

            // Release the ALT key if it was pressed above. #todo Is this why sometimes the app is immediately switched on ALT+Space?
            if(altKeyPressed) {
                altKey.Release();
            }
        }

        private void TextChanged(object sender, TextChangedEventArgs args) {
            if(!_isSearchEnabled) {
                return;
            }

            string query = TextBoxSearch.Text;
            if(string.IsNullOrEmpty(query)) {
                // Back to the starting point (eg now can immediately switch to app 1 etc).
                _isSearchEnabled = false;
                TextBoxSearch.IsReadOnly = true; // [!] Read-only true so that the caret again doesn't show.
            }

            // [!] Below code is required both for _isSearchEnabled true and false (ie back to no filtering).
            WindowFilterContext<AppWindowViewModel> context = new WindowFilterContext<AppWindowViewModel> {
                Windows = _unfilteredWindowList,
                ForegroundWindowProcessTitle = new AppWindow(_foregroundWindow.HWnd).ProcessName
            };

            List<FilterResult<AppWindowViewModel>> filterResults = new WindowFilterer().Filter(context, query).ToList();

            for(int i0 = 0, i1 = 1; i0 < filterResults.Count; i0++, i1++) {
                FilterResult<AppWindowViewModel> filterResult = filterResults[i0];
                filterResult.AppWindow.FormattedTitle = GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                filterResult.AppWindow.FormattedProcessTitle = GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
                filterResult.AppWindow.IndexString = i1.Get1BasedIndexString();
            }

            _filteredWindowList = new ObservableCollection<AppWindowViewModel>(filterResults.Select(r => r.AppWindow));
            ListBoxPrograms.DataContext = _filteredWindowList;

            if(0 < ListBoxPrograms.Items.Count) {
                // If filtering, select the first best match, otherwise select the current app (instead of trying to maybe backspace to 2nd next app).
                ListBoxPrograms.SelectedItem = ListBoxPrograms.Items[0];
            }
        }

        private static string GetFormattedTitleFromBestResult(IList<MatchResult> matchResults) {
            MatchResult bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
            return new XamlHighlighter().Highlight(bestResult.StringParts);
        }

        private void OnEnterPressed(object sender, ExecutedRoutedEventArgs e) {
            Switch();
            e.Handled = true;
        }

        private void ListBoxItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                Switch();
            }

            e.Handled = true;
        }

        private async void CloseWindow(
            object sender
            , ExecutedRoutedEventArgs e
        ) {
            if(ListBoxPrograms.Items.Count <= 0) {
                HideWindow();
            }
            else {
                AppWindowViewModel appWindowViewModel = (AppWindowViewModel)ListBoxPrograms.SelectedItem;
                if(appWindowViewModel != null) {
                    bool isClosed = await _windowCloser.TryCloseAsync(appWindowViewModel);
                    if(isClosed) {
                        RemoveWindow(appWindowViewModel);
                    }
                }
            }

            e.Handled = true;
        }

        private void RemoveWindow(AppWindowViewModel window) {
            int index = _filteredWindowList.IndexOf(window);
            if(index < 0) {
                return;
            }

            if(ListBoxPrograms.SelectedIndex == index) {
                if(index + 1 < _filteredWindowList.Count) {
                    ListBoxPrograms.SelectedIndex++;
                }
                else {
                    if(0 < index) {
                        --ListBoxPrograms.SelectedIndex;
                    }
                }
            }

            _filteredWindowList.Remove(window);
            _unfilteredWindowList.Remove(window);
        }

        private void ScrollListUp(object sender, ExecutedRoutedEventArgs e) {
            PreviousItem();
            e.Handled = true;
        }

        private void PreviousItem() {
            if(0 < ListBoxPrograms.Items.Count) {
                if(ListBoxPrograms.SelectedIndex != 0) {
                    --ListBoxPrograms.SelectedIndex;
                }
                else {
                    ListBoxPrograms.SelectedIndex = -1 + ListBoxPrograms.Items.Count;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollListDown(object sender, ExecutedRoutedEventArgs e) {
            NextItem();
            e.Handled = true;
        }

        private void NextItem() {
            if(0 < ListBoxPrograms.Items.Count) {
                if(ListBoxPrograms.SelectedIndex != ListBoxPrograms.Items.Count - 1) {
                    ListBoxPrograms.SelectedIndex++;
                }
                else {
                    ListBoxPrograms.SelectedIndex = 0;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollSelectedItemIntoView() {
            object selectedItem = ListBoxPrograms.SelectedItem;
            if(selectedItem != null) {
                ListBoxPrograms.ScrollIntoView(selectedItem);
            }
        }

        private void ScrollListHome(object sender, ExecutedRoutedEventArgs e) {
            ListBoxPrograms.SelectedIndex = 0;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }

        private void ScrollListEnd(object sender, ExecutedRoutedEventArgs e) {
            ListBoxPrograms.SelectedIndex = ListBoxPrograms.Items.Count - 1;
            ScrollSelectedItemIntoView();

            e.Handled = true;
        }

        private void MainWindow_Deactivated(object sender, EventArgs e) {
            HideWindow();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
            DisableSystemMenu();
        }

        private void DisableSystemMenu() {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            SystemWindow window = new SystemWindow(windowHandle);
            window.Style &= ~WindowStyleFlags.SYSMENU;
        }

        private void SearchIcon_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            // Toggle.
            ToggleSearch();
        }
        #endregion Event Handlers
    }
}
