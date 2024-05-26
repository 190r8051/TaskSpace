using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using static TaskSpace.Core.WinApi;

namespace TaskSpace.Core {
    /// <summary>
    /// This class is a wrapper around the Win32 api window handles.
    /// </summary>
    public class AppWindow : SystemWindow {
        #region Properties
        public string ProcessName {
            get {
                string key = "ProcessName-" + HWnd;
                if(MemoryCache.Default.Get(key) is string processName) {
                    return processName;
                }

                processName = Process.ProcessName;
                MemoryCache.Default.Add(key, processName, DateTimeOffset.Now.AddHours(1));
                return processName;
            }
        }

        //public Icon LargeWindowIcon => new WindowIconFinder().Find(this, WindowIconSize.Large);

        //public Icon SmallWindowIcon => new WindowIconFinder().Find(this, WindowIconSize.Small);

        public Icon LargeWindowIcon {
            get {
                if(IsApplicationFrameWindow()) {
                    AppWindow underlyingUwpWindow = GetUnderlyingUwpWindow();
                    return underlyingUwpWindow == null ? null : new UwpWindowIconFinder().Find(underlyingUwpWindow);
                }

                return new WindowIconFinder().Find(this, WindowIconSize.Large);
            }
        }

        public Icon SmallWindowIcon {
            get {
                if(IsApplicationFrameWindow()) {
                    AppWindow underlyingUwpWindow = GetUnderlyingUwpWindow();
                    return underlyingUwpWindow == null ? null : new UwpWindowIconFinder().Find(underlyingUwpWindow);
                }

                return new WindowIconFinder().Find(this, WindowIconSize.Small);
            }
        }

        public string ExecutablePath => GetExecutablePath(Process.Id);
        #endregion Properties

        #region Constructor
        public AppWindow(IntPtr HWnd) : base(HWnd) {
        }
        #endregion Constructor

        #region Methods
        private AppWindow GetUnderlyingUwpWindow() {
            SystemWindow window = AllChildWindows.FirstOrDefault(w => w.Process.Id != Process.Id);
            return window != null
                ? new AppWindow(window.HWnd)
                : null;
        }

        /// <summary>
        /// Sets the focus to this window and brings it to the foreground.
        /// </summary>
        public void SwitchTo() {
            // This function is deprecated, so should probably be replaced.
            WinApi.SwitchToThisWindow(HWnd, true);
        }

        public void SwitchToLastVisibleActivePopup() {
            IntPtr lastActiveVisiblePopup = GetLastActiveVisiblePopup();
            WinApi.SwitchToThisWindow(lastActiveVisiblePopup, true);
        }

        public AppWindow Owner {
            get {
                IntPtr ownerHandle = WinApi.GetWindow(HWnd, WinApi.GetWindowCmd.GW_OWNER);
                if(ownerHandle == IntPtr.Zero) return null;
                return new AppWindow(ownerHandle);
            }
        }

        public new static IEnumerable<AppWindow> AllToplevelWindows {
            get {
                return ManagedWinapi.Windows.SystemWindow
                    .AllToplevelWindows
                    .Select(w => new AppWindow(w.HWnd));
            }
        }

        public static bool IsCloaked(IntPtr window) {
            WinApi.DwmGetWindowAttribute(window, WindowAttribute.Cloaked, out bool cloaked, Marshal.SizeOf(typeof(bool)));
            return cloaked;
        }

        //// [experimental]<From FrigoTab.>
        //public bool IsAltTabWindow(List<string> blockList) {
        //    //if (this.ProcessName.Equals("devenv.exe") || this.ProcessName.Equals("NimbleText.exe")) { // [debug]
        //    //    string s = "abc";
        //    //    string s2 = s;
        //    //}

        //    if(IsCloaked(this.HWnd)) {
        //        return false;
        //    }

        //    //WindowStyles style = handle.GetWindowStyles();
        //    WindowStyles style = (WindowStyles)WinApi.GetWindowLongPtr(this.HWnd, WindowLong.Style);

        //    if(style.HasFlag(WindowStyles.Disabled)) {
        //        return false;
        //    }

        //    if(!style.HasFlag(WindowStyles.Visible)) {
        //        return false;
        //    }

        //    //WindowExStyles ex = handle.GetWindowExStyles();
        //    WindowExStyles ex = (WindowExStyles)WinApi.GetWindowLongPtr(this.HWnd, WindowLong.ExStyle);

        //    if(ex.HasFlag(WindowExStyles.NoActivate)) {
        //        return false;
        //    }

        //    if(ex.HasFlag(WindowExStyles.AppWindow)) {
        //        return false;
        //    }

        //    if(ex.HasFlag(WindowExStyles.ToolWindow)) {
        //        return false;
        //    }

        //    // A window is an alt-tab window if it's its own root owner.
        //    bool retVal = this.HWnd == GetLastActiveVisiblePopup(WinApi.GetAncestor(this.HWnd, WinApi.GetAncestorFlags.GetRootOwner))
        //        && !blockList.Contains(this.ProcessName);

        //    return retVal;
        //}

        // [todo] This used to work, but then started showing some non-switchable apps (see new blocked list logic).
        public bool IsAltTabWindow(List<string> blockList) {
            if(!Visible) {
                return false;
            }

            if(!HasWindowTitle()) {
                return false;
            }

            if(IsAppWindow()) {
                return true;
            }

            if(IsToolWindow()) {
                return false;
            }

            if(IsNoActivate()) {
                return false;
            }

            if(!IsOwnerOrOwnerNotVisible()) {
                return false;
            }

            if(HasITaskListDeletedProperty()) {
                return false;
            }

            if(IsCoreWindow()) {
                return false;
            }

            if(IsApplicationFrameWindow() && !HasAppropriateApplicationViewCloakType()) {
                return false;
            }

            // [warning]??? DON'T move it before the above checks. But shouldn't matter if blockList doesn't contain process?
            if(blockList.Contains(this.ProcessName)) {
                return false;
            }

            //StringBuilder sb = new StringBuilder();
            //if(IsCloaked(this.HWnd)) {
            //    //return false;
            //    sb.AppendLine("IsCloaked");
            //}

            ////WindowStyles style = handle.GetWindowStyles();
            //WindowStyles style = (WindowStyles)WinApi.GetWindowLongPtr(this.HWnd, WindowLong.Style);

            //if(style.HasFlag(WindowStyles.Disabled)) {
            //    //return false;
            //    sb.AppendLine("Disabled");
            //}

            //if(!style.HasFlag(WindowStyles.Visible)) {
            //    //return false;
            //    sb.AppendLine("!Visible");
            //}

            ////WindowExStyles ex = handle.GetWindowExStyles();
            //WindowExStyles ex = (WindowExStyles)WinApi.GetWindowLongPtr(this.HWnd, WindowLong.ExStyle);

            //if(ex.HasFlag(WindowExStyles.NoActivate)) {
            //    //return false;
            //    sb.AppendLine("NoActivate");
            //}

            //if(ex.HasFlag(WindowExStyles.AppWindow)) {
            //    //return false;
            //    sb.AppendLine("AppWindow");
            //}

            //if(ex.HasFlag(WindowExStyles.ToolWindow)) {
            //    //return false;
            //    sb.AppendLine("IsCToolWindowloaked");
            //}

            ////// A window is an alt-tab window if it's its own root owner.
            ////bool retVal = this.HWnd == GetLastActiveVisiblePopup(WinApi.GetAncestor(this.HWnd, WinApi.GetAncestorFlags.GetRootOwner))
            ////    && !blockList.Contains(this.ProcessName);

            //if(0 < sb.ToString().Length) {
            //    //MessageBox.Show(this.ProcessName + " " + sb.ToString());
            //    // Only battle.net shows as disabled.
            //}

            return true;
        }

        private bool HasWindowTitle() {
            return !string.IsNullOrEmpty(Title);
        }

        private bool IsToolWindow() {
            return (ExtendedStyle & WindowExStyleFlags.TOOLWINDOW) == WindowExStyleFlags.TOOLWINDOW
                    || (Style & WindowStyleFlags.TOOLWINDOW) == WindowStyleFlags.TOOLWINDOW;
        }

        private bool IsAppWindow() {
            return (ExtendedStyle & WindowExStyleFlags.APPWINDOW) == WindowExStyleFlags.APPWINDOW;
        }

        private bool IsNoActivate() {
            return (ExtendedStyle & WindowExStyleFlags.NOACTIVATE) == WindowExStyleFlags.NOACTIVATE;
        }

        // [new]
        private static IntPtr GetLastActiveVisiblePopup(IntPtr root) {
            IntPtr hwndWalk = IntPtr.Zero;
            IntPtr hwndTry = root;
            while(hwndWalk != hwndTry) {
                hwndWalk = hwndTry;
                hwndTry = WinApi.GetLastActivePopup(hwndWalk);
                if(WinApi.IsWindowVisible(hwndTry)) {
                    return hwndTry;
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr GetLastActiveVisiblePopup() {
            // Which windows appear in the Alt+Tab list? -Raymond Chen.
            // http://blogs.msdn.com/b/oldnewthing/archive/2007/10/08/5351207.aspx

            // Start at the root owner.
            IntPtr hwndWalk = WinApi.GetAncestor(this.HWnd, WinApi.GetAncestorFlags.GetRootOwner);

            // See if we are the last active visible popup.
            IntPtr hwndTry = IntPtr.Zero;
            while(hwndWalk != hwndTry) {
                hwndTry = hwndWalk;
                hwndWalk = WinApi.GetLastActivePopup(hwndTry);
                if(WinApi.IsWindowVisible(hwndWalk)) {
                    return hwndWalk;
                }
            }

            return hwndWalk;
        }

        private bool IsOwnerOrOwnerNotVisible() {
            return Owner == null || !Owner.Visible;
        }

        private bool HasITaskListDeletedProperty() {
            return WinApi.GetProp(HWnd, "ITaskList_Deleted") != IntPtr.Zero;
        }

        private bool IsCoreWindow() {
            // Avoids double entries for Windows Store Apps on Windows 10.
            return ClassName == "Windows.UI.Core.CoreWindow";
        }

        private bool IsApplicationFrameWindow() {
            return ClassName == "ApplicationFrameWindow";
        }

        private bool HasAppropriateApplicationViewCloakType() {
            // The ApplicationFrameWindows that host Windows Store Apps like to
            // hang around in Windows 10 (and win11) even after the underlying program has been closed.
            // A way to figure out if the ApplicationFrameWindow is
            // currently hosting an application is to check if it has a property called
            // "ApplicationViewCloakType", and that the value != 1.
            //
            // I've stumbled upon these values of "ApplicationViewCloakType":
            //    0 = Program is running on current virtual desktop.
            //    1 = Program is not running.
            //    2 = Program is running on a different virtual desktop.
            bool hasAppropriateApplicationViewCloakType = false;
            WinApi.EnumPropsEx(
                HWnd, (hwnd, lpszString, data, dwData) => {
                    string propName = Marshal.PtrToStringAnsi(lpszString);
                    if(propName == "ApplicationViewCloakType") {
                        hasAppropriateApplicationViewCloakType = data != 1;
                        return 0;
                    }

                    return 1;
                },
                IntPtr.Zero
            );

            return hasAppropriateApplicationViewCloakType;
        }

        // This method only works on ```Windows Vista <= Windows```.
        private static string GetExecutablePath(int processId) {
            StringBuilder buffer = new StringBuilder(1024);
            IntPtr hprocess = WinApi.OpenProcess(WinApi.ProcessAccess.QueryLimitedInformation, false, processId);
            if(hprocess == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try {
                // ReSharper disable once RedundantAssignment.
                int size = buffer.Capacity;
                if(WinApi.QueryFullProcessImageName(hprocess, 0, buffer, out size)) {
                    return buffer.ToString();
                }
            }
            finally {
                WinApi.CloseHandle(hprocess);
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        #endregion Methods
    }
}
