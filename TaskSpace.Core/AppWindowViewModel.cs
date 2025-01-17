﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaskSpace.Core {
    public class AppWindowViewModel : INotifyPropertyChanged, IWindowText {
        #region IWindowText Members
        public AppWindow AppWindow { get; private set; }

        private string _windowTitle;
        public string WindowTitle {
            get {
                string retVal = _isLaunchCommand
                    ? _windowTitle
                    : this.AppWindow?.Title ?? "WindowTitle TEST";
                return retVal;
            }
        }

        private string _appFilePath;
        public string AppFilePath {
            get {
                string retVal = _isLaunchCommand
                    ? _appFilePath
                    : this.AppWindow?.ExecutablePath ?? "AppFilePath TEST";
                return retVal;
            }
        }

        private string _appFileNameWithExt;
        public string AppFileNameWithExt {
            get {
                string retVal = _isLaunchCommand
                    ? _appFileNameWithExt
                    : this.AppWindow?.ProcessName ?? "AppFileNameWithExt TEST";
                return retVal;
            }
        }

        // #todo Also AppFileName, i.e. without Ext?
        #endregion IWindowText Members

        #region Bindable Properties
        public IntPtr HWnd {
            get {
                return AppWindow?.HWnd ?? IntPtr.Zero;
            }
        }

        private bool _isLaunchCommand = false;
        public bool IsLaunchCommand {
            get { return _isLaunchCommand; }
            set {
                _isLaunchCommand = value;
                NotifyOfPropertyChange(() => IsLaunchCommand);
            }
        }

        // #TODO!!! Instead of this being a special case,
        // every app window can also be a launch-command
        // (provided the appFilePath is detected and saved).
        private string _launchCommand = string.Empty;
        public string LaunchCommand {
            get { return _launchCommand; }
            set {
                _launchCommand = value;
                NotifyOfPropertyChange(() => LaunchCommand);
            }
        }

        /// <summary>
        /// The ordinal mapped (0-based) inside the app list,
        /// e.g. if 'M' is mapped to app list ["spotify.exe", "aimp.exe"] and the current app is "aimp.exe",
        /// then the value is 1.
        /// #note This is different but directly mapped to the UI 1-based ordinals (_indexString/IndexString).
        /// </summary>
        private int _ordinalMapped0;
        public int OrdinalMapped0 {
            get { return _ordinalMapped0; }
            set {
                _ordinalMapped0 = value;
                NotifyOfPropertyChange(() => OrdinalMapped0); // #cut? This is not bound to UI.
            }
        }

        // The 1-based index string.
        // #note This is different but directly mapped to the non-UI 0-based ordinals (_ordinalMapped0/OrdinalMapped0).
        // #warning If renaming, make sure to update below in XAML:
        // ```
        // <!-- OrdinalMapped1. -->
        // <TextBlock local:FormattedTextAttribute.FormattedText="{Binding Path=OrdinalMapped1}"
        // ```
        private string _ordinalMapped1;
        public string OrdinalMapped1 {
            get { return _ordinalMapped1; }
            set {
                _ordinalMapped1 = value;
                NotifyOfPropertyChange(() => OrdinalMapped1); // Notify the UI.
            }
        }

        // #TODO Should also have IsLetterAutoMapped property?

        private string _symbolMapped;
        public string SymbolMapped {
            get { return _symbolMapped; }
            set {
                _symbolMapped = value;
                NotifyOfPropertyChange(() => SymbolMapped);
            }
        }

        // This is the bound symbol if found for this app: starts the same as SymbolMapped, but might change to blank if symbol-filtering multiple instances.
        private string _symbolBound;
        public string SymbolBound {
            get { return _symbolBound; }
            set {
                _symbolBound = value;
                NotifyOfPropertyChange(() => SymbolBound);
            }
        }

        // This is the mapped symbol if found for this app (might change only on start-up, e.g. if there was a change in config).
        private string _letterMapped;
        public string LetterMapped {
            get { return _letterMapped; }
            set {
                _letterMapped = value;
                NotifyOfPropertyChange(() => LetterMapped);
            }
        }

        // This is the bound letter if found for this app: starts the same as LetterMapped, but might change to blank if letter-filtering multiple instances.
        private string _letterBound;
        public string LetterBound {
            get { return _letterBound; }
            set {
                _letterBound = value;
                NotifyOfPropertyChange(() => LetterBound);
            }
        }

        private string _formattedTitle;
        public string FormattedTitle {
            get { return _formattedTitle; }
            set {
                _formattedTitle = value;
                NotifyOfPropertyChange(() => FormattedTitle);
            }
        }

        private string _formattedProcessTitle;
        public string FormattedProcessTitle {
            get { return _formattedProcessTitle; }
            set {
                _formattedProcessTitle = value;
                NotifyOfPropertyChange(() => FormattedProcessTitle);
            }
        }

        private bool _isBeingClosed = false;
        public bool IsBeingClosed {
            get { return _isBeingClosed; }
            set {
                _isBeingClosed = value;
                NotifyOfPropertyChange(() => IsBeingClosed);
            }
        }
        #endregion Bindable Properties

        #region Constructors
        // Blank constructor (initialize all properties directly).
        public AppWindowViewModel() {
            this.AppWindow = null;
        }

        //public AppWindowViewModel(
        //    AppWindow appWindow
        //) {
        //    // #todo Param checking.
        //    this.AppWindow = appWindow;
        //}

        // Constructor.
        public AppWindowViewModel(
            string appFileNameWithExt
            , string appFilePath
            , bool isLaunchCommand
            , Icon icon
        ) {
            this._appFileNameWithExt = appFileNameWithExt;
            this._appFilePath = appFilePath;
            //this._windowTitle = ?;
            this._isLaunchCommand = isLaunchCommand;

            if(isLaunchCommand) {
                //this.AppWindow = null; // No window as this is a launch command (for now). // #CUT

                this.AppWindow = new AppWindow(hWnd: 0, icon: icon);
            }
        }

        // Command constructor (e.g. for launching Power Menu commands).
        public AppWindowViewModel(
            string launchCommand
            , char letter
            , Icon icon
        ) {
            this._isLaunchCommand = true;

            //this.AppWindow = null; // No window as this is a launch command. // #CUT
            this.AppWindow = new AppWindow(hWnd: 0, icon: icon);

            this._windowTitle = launchCommand;
            this._appFilePath = launchCommand;
            this._appFileNameWithExt = launchCommand;

            this.LaunchCommand = launchCommand; // #todo Should be possible to mark an app as always in main window (eg if NPP is active, show instance, otherwise show launch command, including if filtering to &Editors).

            this.LetterMapped = letter.ToString();
            this.LetterBound = letter.ToString();

            FileInfo fileInfo = new(this.LaunchCommand);
            this.FormattedProcessTitle = fileInfo.Name;

            this.FormattedTitle = launchCommand;
        }

        /// <summary>
        /// The AppWindowViewModel constructor for mapping the input appWindow to the settings config letters.
        /// </summary>
        /// <param name="appWindow">The app-window value.</param>
        /// <param name="letterMapped">The letter-mapped value.</param>
        /// <param name="letterMappedOrdinal">The letter-mapped-ordinal value.</param>
        /// <param name="letterBound">The letter-bound value.</param>
        public AppWindowViewModel(
            AppWindow appWindow
            , string letterMapped
            , int letterMappedOrdinal
            , string letterBound
        ) {
            // #todo Param checking.
            this.AppWindow = appWindow;
            this.LetterMapped = letterMapped;
            this.OrdinalMapped0 = letterMappedOrdinal;
            this.LetterBound = letterBound;
        }
        #endregion Constructors

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyOfPropertyChange<T>(Expression<Func<T>> property) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if(handler != null) {
                handler(this, new PropertyChangedEventArgs(GetPropertyName(property)));
            }
        }

        private string GetPropertyName<T>(Expression<Func<T>> property) {
            LambdaExpression lambda = (LambdaExpression)property;

            MemberExpression memberExpression;
            if(lambda.Body is UnaryExpression) {
                UnaryExpression unaryExpression = (UnaryExpression)lambda.Body;
                memberExpression = (MemberExpression)unaryExpression.Operand;
            }
            else {
                memberExpression = (MemberExpression)lambda.Body;
            }

            return memberExpression.Member.Name;
        }
        #endregion INotifyPropertyChanged Members
    }
}
