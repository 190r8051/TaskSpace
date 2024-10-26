namespace TaskSpace {
    public class Hotkey : ManagedWinapi.Hotkey {
        public void LoadSettingsMain() {
            KeyCode = (System.Windows.Forms.Keys)Properties.Settings.Default.HotkeyMain;
            WindowsKey = Properties.Settings.Default.WindowsKey;
            Alt = Properties.Settings.Default.Alt;
            Ctrl = Properties.Settings.Default.Ctrl;
            Shift = Properties.Settings.Default.Shift;
        }

        public void LoadSettingsAlt() {
            KeyCode = (System.Windows.Forms.Keys)Properties.Settings.Default.HotkeyAlt; // #TODO Should have KeyCodeAlt?
            WindowsKey = Properties.Settings.Default.WindowsKey;
            Alt = Properties.Settings.Default.Alt;
            Ctrl = Properties.Settings.Default.Ctrl;
            Shift = Properties.Settings.Default.Shift;
        }

        public void SaveSettings() {
            Properties.Settings.Default.HotkeyMain = (int)KeyCode;
            Properties.Settings.Default.WindowsKey = WindowsKey;
            Properties.Settings.Default.Alt = Alt;
            Properties.Settings.Default.Ctrl = Ctrl;
            Properties.Settings.Default.Shift = Shift;

            // #TODO Alt hotkey.

            Properties.Settings.Default.Save();
        }
    }
}
