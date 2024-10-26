using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TaskSpace.Core {
    public class WindowCloser : IDisposable {
        private bool _isDisposed;

        private static readonly TimeSpan _checkInterval = TimeSpan.FromMilliseconds(125);

        public async Task<bool> TryCloseAsync(
            AppWindowViewModel window
        ) {
            window.IsBeingClosed = true;
            window.AppWindow.Close();

            while(!_isDisposed && !window.AppWindow.IsClosedOrHidden) {
                await Task.Delay(_checkInterval).ConfigureAwait(false);
            }

            return window.AppWindow.IsClosedOrHidden;
        }

        public async Task<bool> TryKillAsync(
            AppWindowViewModel window
        ) {
            window.IsBeingClosed = true;

            try {
                window.AppWindow.Process.Kill(); // This forcibly terminates the process.
                window.AppWindow.Process.WaitForExit(); // #optional Waits for the process to exit after termination.
            }
            catch(Exception) {
                // Handle exceptions such as "AccessDenied" or "ProcessHasExited".
                return false; // Return false if the process couldn't be killed.
            }

            while(!_isDisposed && !window.AppWindow.IsClosedOrHidden) {
                await Task.Delay(_checkInterval).ConfigureAwait(false);
            }

            return window.AppWindow.IsClosedOrHidden;
        }

        public void Dispose() {
            _isDisposed = true;
        }
    }
}
