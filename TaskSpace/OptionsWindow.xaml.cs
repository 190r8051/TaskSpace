using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ManagedWinapi;
using TaskSpace.Core;
using TaskSpace.Properties;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace TaskSpace {
    public partial class OptionsWindow : Window {
        private readonly Hotkey _hotkeyMain;
        private readonly Hotkey _hotkeyAlt;
        private HotkeyViewModel _hotkeyViewModelMain;
        private HotkeyViewModel _hotkeyViewModelAlt;

        public OptionsWindow() {
            InitializeComponent();

            _hotkeyMain = (Hotkey)Application.Current.Properties["HotkeyMain"];
            try {
                _hotkeyMain.LoadSettingsMain();
            }
            catch(HotkeyAlreadyInUseException) {
            }

            _hotkeyAlt = (Hotkey)Application.Current.Properties["HotkeyAlt"];
            try {
                _hotkeyAlt.LoadSettingsAlt();
            }
            catch(HotkeyAlreadyInUseException) {
            }

            _hotkeyViewModelMain = new HotkeyViewModel {
                KeyCode = KeyInterop.KeyFromVirtualKey((int)_hotkeyMain.KeyCode),
                Alt = _hotkeyMain.Alt,
                Ctrl = _hotkeyMain.Ctrl,
                Windows = _hotkeyMain.WindowsKey,
                Shift = _hotkeyMain.Shift
            };
            HotkeyCheckBox.IsChecked = Settings.Default.EnableHotkeyMain;
            HotkeyPreview.Text = _hotkeyViewModelMain.ToString();
            HotkeyPreview.IsEnabled = Settings.Default.EnableHotkeyMain;

            // #note For now, reuse modifier keys, e.g. if above is ALT+Space, then this hotkey is ALT+`.
            _hotkeyViewModelAlt = new HotkeyViewModel {
                KeyCode = KeyInterop.KeyFromVirtualKey(192), // #note For now, hardcode to Oemtilde. #TODO Should get from settings.
                Alt = _hotkeyMain.Alt,
                Ctrl = _hotkeyMain.Ctrl,
                Windows = _hotkeyMain.WindowsKey,
                Shift = _hotkeyMain.Shift
            };
            HotKeyCheckBox2.IsChecked = Settings.Default.EnableHotkeyAlt;
            HotKeyPreview2.Text = _hotkeyViewModelAlt.ToString();
            HotKeyPreview2.IsEnabled = true; //Settings.Default.EnableHotKey; // #todo Add a separate setting.

            AltTabCheckBox.IsChecked = Settings.Default.AltTabHook;
            AutoSwitch.IsChecked = Settings.Default.AutoSwitch;
            AutoSwitch.IsEnabled = Settings.Default.AltTabHook;
            RunAsAdministrator.IsChecked = Settings.Default.RunAsAdmin;
            Theme.Text = Settings.Default.Theme;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        // #todo Should add modification for alt hotkey etc.
        private void Ok_Click(object sender, RoutedEventArgs e) {
            bool closeOptionsWindow = true;

            try {
                _hotkeyMain.IsEnabled = false;

                if(Settings.Default.EnableHotkeyMain) { // #TODO!!! Also EnableHotkeyAlt.
                    // Change the active hotkey.
                    _hotkeyMain.Alt = _hotkeyViewModelMain.Alt;
                    _hotkeyMain.Shift = _hotkeyViewModelMain.Shift;
                    _hotkeyMain.Ctrl = _hotkeyViewModelMain.Ctrl;
                    _hotkeyMain.WindowsKey = _hotkeyViewModelMain.Windows;
                    _hotkeyMain.KeyCode = (Keys)KeyInterop.VirtualKeyFromKey(_hotkeyViewModelMain.KeyCode);
                    _hotkeyMain.IsEnabled = true;
                }

                _hotkeyMain.SaveSettings();
            }
            catch(HotkeyAlreadyInUseException) {
                string boxText = "Sorry! The selected shortcut for activating TaskSpace is in use by another program. "
                    + "Please choose another.";
                MessageBox.Show(boxText, "Shortcut already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
                closeOptionsWindow = false;
            }

            Settings.Default.EnableHotkeyMain = HotkeyCheckBox.IsChecked.GetValueOrDefault();
            Settings.Default.AltTabHook = AltTabCheckBox.IsChecked.GetValueOrDefault();
            Settings.Default.AutoSwitch = AutoSwitch.IsChecked.GetValueOrDefault();
            Settings.Default.RunAsAdmin = RunAsAdministrator.IsChecked.GetValueOrDefault();
            Settings.Default.Theme = Theme.Text;
            Settings.Default.Save();

            if(closeOptionsWindow) {
                Close();
            }
        }

        private void HotKeyPreview_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            // The text box grabs all input.
            e.Handled = true;

            // Fetch the actual shortcut key.
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Ignore modifier keys.
            if(key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin
            ) {
                return;
            }

            HotkeyViewModel previewHotKeyModel = new HotkeyViewModel();
            previewHotKeyModel.Ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            previewHotKeyModel.Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            previewHotKeyModel.Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

            KeyboardKey winLKey = new KeyboardKey(Keys.LWin);
            KeyboardKey winRKey = new KeyboardKey(Keys.RWin);
            previewHotKeyModel.Windows = (winLKey.State & 0x8000) == 0x8000 || (winRKey.State & 0x8000) == 0x8000;
            previewHotKeyModel.KeyCode = key;

            string previewText = previewHotKeyModel.ToString();

            // Jump to the next element if the user presses only the Tab key.
            if(previewText == "Tab") {
                ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return;
            }

            HotkeyPreview.Text = previewText;
            _hotkeyViewModelMain = previewHotKeyModel;
        }

        private class HotkeyViewModel {
            public Key KeyCode { get; set; }
            public bool Shift { get; set; }
            public bool Alt { get; set; }
            public bool Ctrl { get; set; }
            public bool Windows { get; set; }

            public override string ToString() {
                StringBuilder shortcutText = new StringBuilder();

                if(Ctrl) {
                    shortcutText.Append("Ctrl + ");
                }

                if(Shift) {
                    shortcutText.Append("Shift + ");
                }

                if(Alt) {
                    shortcutText.Append("Alt + ");
                }

                if(Windows) {
                    shortcutText.Append("Win + ");
                }

                string keyString = KeyboardHelper.CodeToString((uint)KeyInterop.VirtualKeyFromKey(KeyCode)).ToUpper().Trim();
                if(keyString.Length == 0) {
                    keyString = new KeysConverter().ConvertToString(KeyCode);
                }

                // If the user presses "Escape" then show "Escape".
                if(keyString == "\u001B") {
                    keyString = "Escape";
                }

                shortcutText.Append(keyString);
                return shortcutText.ToString();
            }
        }

        private void HotkeyPreview_OnGotFocus(object sender, RoutedEventArgs e) {
            // Disable the current hotkey while the hotkey field is active.
            _hotkeyMain.IsEnabled = false;
        }

        private void HotkeyPreview_OnLostFocus(object sender, RoutedEventArgs e) {
            try {
                _hotkeyMain.IsEnabled = true;
            }
            catch(HotkeyAlreadyInUseException) {
                // It is alright if the hotkey can't be reactivated.
            }
        }

        private void AltTabCheckBox_OnChecked(object sender, RoutedEventArgs e) {
            AutoSwitch.IsEnabled = true;
        }

        private void AltTabCheckBox_OnUnchecked(object sender, RoutedEventArgs e) {
            AutoSwitch.IsEnabled = false;
            AutoSwitch.IsChecked = false;
        }

        private void HotkeyCheckBox_Checked(object sender, RoutedEventArgs e) {
            HotkeyPreview.IsEnabled = true;
        }

        private void HotkeyCheckBox_OnUnchecked(object sender, RoutedEventArgs e) {
            HotkeyPreview.IsEnabled = false;
        }
    }
}
