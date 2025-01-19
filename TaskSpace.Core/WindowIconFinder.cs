using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;

namespace TaskSpace.Core {
    public enum WindowIconSize {
        Small,
        Large
    }

    public class WindowIconFinder {
        public Icon Find(
            AppWindow window
            , WindowIconSize windowIconSize
        ) {
            Icon icon = null;
            try {
                // http://msdn.microsoft.com/en-us/library/windows/desktop/ms632625(v=vs.85).aspx
                IntPtr outValue = WinApi.SendMessageTimeout(
                    window.HWnd, 0x007F,
                    windowIconSize == WindowIconSize.Small ? new IntPtr(2) : new IntPtr(1),
                    IntPtr.Zero,
                    WinApi.SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 100,
                    out var response
                );

                if(outValue == IntPtr.Zero || response == IntPtr.Zero) {
                    response = WinApi.GetClassLongPtr(
                        window.HWnd,
                        windowIconSize == WindowIconSize.Small
                            ? WinApi.ClassLongFlags.GCLP_HICONSM
                            : WinApi.ClassLongFlags.GCLP_HICON
                    );
                }

                if(response != IntPtr.Zero) {
                    icon = Icon.FromHandle(response);
                }
                else {
                    string executablePath = window.ExecutablePath;
                    if (string.IsNullOrWhiteSpace(executablePath)) {
                        return icon;
                    }

                    icon = Icon.ExtractAssociatedIcon(executablePath);
                }
            }
            catch(Win32Exception) {
                //Debug.WriteLine($"Could not extract icon.");
            }

            return icon;
        }
    }
}
