﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TaskSpace.Core;

namespace TaskSpace {
    public class AppWindowViewModel : INotifyPropertyChanged, IWindowText {
        #region IWindowText Members
        public AppWindow AppWindow { get; private set; }

        public string WindowTitle {
            get {
                return this.AppWindow?.Title ?? "WindowTitle TEST";
            }
        }

        public string ProcessName {
            get {
                return this.AppWindow?.ProcessName ?? "ProcessName TEST";
            }
        }
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

        private string _launchCommand = string.Empty;
        public string LaunchCommand {
            get { return _launchCommand; }
            set {
                _launchCommand = value;
                NotifyOfPropertyChange(() => LaunchCommand);
            }
        }

        private int _letterMappedOrdinal;
        public int LetterMappedOrdinal {
            get { return _letterMappedOrdinal; }
            set {
                _letterMappedOrdinal = value;
                NotifyOfPropertyChange(() => LetterMappedOrdinal);
            }
        }

        // This is the mapped letter if found for this app (might change only on start-up, e.g. if there was a change in config).
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

        // The 1-based index string.
        private string _indexString;
        public string IndexString {
            get { return _indexString; }
            set {
                _indexString = value;
                NotifyOfPropertyChange(() => IndexString);
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

        // Command constructor (eg for launching Power Menu commands).
        public AppWindowViewModel(string launchCommand, char letter) {
            this.IsLaunchCommand = true;

            this.AppWindow = null; // No window as this is a launch command?
            this.LaunchCommand = launchCommand; // #todo Should be possible to mark an app as always in main window (eg if NPP is active, show instance, otherwise show launch command, including if filtering to &Editors).

            this.LetterMapped = letter.ToString();
            this.LetterBound = letter.ToString();

            FileInfo fileInfo = new FileInfo(this.LaunchCommand);
            this.FormattedProcessTitle = fileInfo.Name;

            //this.FormattedTitle = "[🚀 Launch]";
            //this.FormattedTitle = ">>> Launch 🚀";
            this.FormattedTitle = ">>> LOCK"; // Hard-coded for now.
        }

        // This maps the settings config letters.
        public AppWindowViewModel(AppWindow appWindow, Dictionary<char, List<string>> charToAppList) {
            this.AppWindow = appWindow;

            this.LetterMapped = string.Empty;
            this.LetterBound = string.Empty;

            // #future Might still be inefficient, ie could be pivoted from charToAppList to appToChar.
            for(int i = (int)'A'; i <= (int)'Z'; i++) {
                char letter = (char)i;

                string processWithExtension = $"{this.AppWindow.ProcessName}.exe".ToLower();
                //if (appsList.Contains(processWithExtension)) {
                int ordinal = charToAppList[letter].IndexOf(processWithExtension);
                if(ordinal != -1) {
                    this.LetterMappedOrdinal = ordinal;
                    this.LetterMapped = letter.ToString();
                    this.LetterBound = letter.ToString();
                    break;
                }
            }
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
