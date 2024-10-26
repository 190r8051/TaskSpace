using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using TaskSpace.Properties;

namespace TaskSpace {
    internal class Program {
        #region Constants
        private const string _MUTEX_ID = "DBDE24E4-91F6-11DF-B495-C536DFD72085-TaskSpace";
        #endregion Constants

        #region Methods
        [STAThread]
        private static void Main() {
            RunAsAdministratorIfConfigured();

            using(Mutex mutex = new Mutex(false, _MUTEX_ID)) {
                bool hasHandle = false;
                MainWindow mainWindow = null;

                try {
                    try {
                        hasHandle = mutex.WaitOne(5_000, false);
                        if(hasHandle == false) {
                            return; // Another instance exist.
                        }
                    }
                    catch(AbandonedMutexException) {
                        // Log the fact the mutex was abandoned in another process, it will still get aquired.
                    }

#if PORTABLE
                        MakePortable(Settings.Default);
#endif

                    MigrateUserSettings();

                    App app = new();
                    mainWindow = new();
                    app.MainWindow = mainWindow;
                    app.Run();
                }
                finally {
                    // The app is exiting, so dispose resources.
                    mainWindow?.Dispose();
                    if(hasHandle) {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static void RunAsAdministratorIfConfigured() {
            if(RunAsAdminRequested() && !IsRunAsAdmin()) {
                ProcessStartInfo proc = new ProcessStartInfo {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Assembly.GetEntryAssembly().CodeBase,
                    Verb = "runas"
                };

                Process.Start(proc);
                Environment.Exit(0);
            }
        }

        private static bool RunAsAdminRequested() {
            return Settings.Default.RunAsAdmin;
        }

        private static void MakePortable(ApplicationSettingsBase settings) {
            PortableSettingsProvider portableSettingsProvider = new PortableSettingsProvider();
            settings.Providers.Add(portableSettingsProvider);
            foreach(SettingsProperty prop in settings.Properties) {
                prop.Provider = portableSettingsProvider;
            }

            settings.Reload();
        }

        private static void MigrateUserSettings() {
            if(!Settings.Default.FirstRun) {
                return;
            }

            Settings.Default.Upgrade();
            Settings.Default.FirstRun = false;
            Settings.Default.Save();
        }

        private static bool IsRunAsAdmin() {
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);

            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        #endregion Methods
    }
}
