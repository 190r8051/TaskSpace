using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TaskSpace.Properties;

namespace TaskSpace {
    public class Theme {
        #region Enums
        public enum Mode {
            Light,
            Dark
        }
        #endregion Enums

        #region Constants
        public static SolidColorBrush BACKGROUND_DARK_0 = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        //public static SolidColorBrush BACKGROUND_DARK_0 = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #black
        public static SolidColorBrush BACKGROUND_DARK_1 = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        public static SolidColorBrush FOREGROUND_DARK_0 = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        
        public static SolidColorBrush BACKGROUND_LIGHT_0 = new SolidColorBrush(Color.FromRgb(248, 248, 248));
        public static SolidColorBrush BACKGROUND_LIGHT_1 = new SolidColorBrush(Color.FromRgb(248, 248, 248)); // #todo Update.
        public static SolidColorBrush FOREGROUND_LIGHT_0 = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #black
        #endregion Constants

        #region Properties
        public SolidColorBrush _background;
        public SolidColorBrush Background {
            get {
                return _background;
            }
        }

        public SolidColorBrush _backgroundAlt;
        public SolidColorBrush BackgroundAlt {
            get {
                return _backgroundAlt;
            }
        }

        public SolidColorBrush _foreground;
        public SolidColorBrush Foreground {
            get {
                return _foreground;
            }
        }
        private static MainWindow _mainWindow;
        #endregion Properties

        #region Constructor
        public Theme(MainWindow mainWindow) {
            _mainWindow = mainWindow;
            LoadTheme();
        }
        #endregion Constructor

        #region Methods
        public void LoadTheme() {
            Enum.TryParse(Settings.Default.Theme, out Mode mode);
            switch(mode) {
                case Mode.Light: {
                    _background = BACKGROUND_LIGHT_0;
                    _backgroundAlt = BACKGROUND_LIGHT_1;
                    _foreground = FOREGROUND_LIGHT_0;
                    break;
                }
                case Mode.Dark: {
                    _background = BACKGROUND_DARK_0;
                    _backgroundAlt = BACKGROUND_DARK_1;
                    _foreground = FOREGROUND_DARK_0;
                    break;
                }
                default: {
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }

            SetUpTheme();
        }

        private void SetUpTheme() {
            _mainWindow.Border.Background = Background;
            _mainWindow.TextBoxSearch.Background = Background;
            _mainWindow.ListBoxPrograms.Background = Background;
            _mainWindow.Border.BorderBrush = Background;

            _mainWindow.SearchIcon.Background = Background;
            //_mainWindow.SearchIcon.Background = BackgroundAlt;
            _mainWindow.SearchIcon.Foreground = Foreground;

            _mainWindow.TextBoxSearch.Foreground = Foreground;
            _mainWindow.ListBoxPrograms.Foreground = Foreground;
        }
        #endregion Methods
    }
}
