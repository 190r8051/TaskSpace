using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskSpace.Core {
    public class WindowFinder {
        public List<AppWindowViewModel> GetAndMapWindows(
            List<string> blockList
            , Dictionary<char, List<string>> settingsCharToAppList
            , bool isAutoMappingEnabled = false
        ) {
            Dictionary<char, List<string>> dynamicCharToAppList = [];

            // Get the valid windows.
            IEnumerable<AppWindow> appWindows = ManagedWinapi.Windows.SystemWindow
                .AllToplevelWindows // Get raw windows.
                .Select(systemWindow => new AppWindow(systemWindow.HWnd)) // Get appWindows.
                .Where(a => a.IsAltTabbableWindow(blockList)); // Get alt-tabbable appWindows.

            List<AppWindowViewModel> appWindowViewModels = [];

            // [todo] Should this get (or get-and-init, or just init internally?,) the dynamicCharToAppList?
            // dynamicCharToAppList['a'] = settingsCharToAppList['A'], but only if there is an app mapped to 'A';
            // otherwise, can use 'A' or 'a' to auto-map any dynamic apps.

            List<int> indexesOfUnmapped = [];

            int i0 = -1; // Dummy value so that inside the loop it increments to 0 at start.
            foreach(AppWindow appWindow in appWindows) {
                i0++;

                string letterMapped = string.Empty;
                string letterBound = string.Empty;
                int letterMappedOrdinal = -1;
                bool isFound = false;

                // #todo Some processes might not have ".exe"?
                string processWithExtension = $"{appWindow.ProcessName}.exe".ToLower();
                char processFirstCharLowercase = char.ToLower(processWithExtension.First());
                char processFirstCharUppercase = char.ToUpper(processWithExtension.First());

                // Attempt to find the letter mapping from settingsCharToAppList.
                for(int i = (int)'A'; i <= (int)'Z'; i++) {
                    char letter = (char)i;

                    if(settingsCharToAppList.TryGetValue(letter, out List<string> appList)) {
                        int ordinal = appList.IndexOf(processWithExtension);
                        if(ordinal != -1) {

                            if(letter == 'M') {
                                Console.WriteLine("TARGET");
                            }

                            // Found, so add to dynamic, but not all, just this app.
                            if(!dynamicCharToAppList.ContainsKey(letter)) {
                                dynamicCharToAppList[letter] = [];
                            }
                            dynamicCharToAppList[letter].Add(processWithExtension);

                            letterMappedOrdinal = ordinal;
                            letterMapped = letter.ToString();
                            letterBound = letter.ToString();
                            isFound = true;
                            break;
                        }
                    }
                }

                if(!isFound) {
                    indexesOfUnmapped.Add(i0);
                }

                // Create and add the AppWindowViewModel instance to the list.
                AppWindowViewModel appWindowViewModel = new(appWindow, letterMapped, letterMappedOrdinal, letterBound);

                appWindowViewModels.Add(appWindowViewModel);
            }

            // #DEBUG[Test closing an app here, but still sending-back the mapped app, then what happens if mapped letter is hit?]
            // #bug? What happens if MainWindow is active, but in the meantime, some process shuts-down an app which is being selected? Crash or ignore?

            // #future? New mode: all pre-mapped apps are always at the top (maybe even a line separating from auto-mapped).
            foreach(int indexOfUnmapped in indexesOfUnmapped) {
                // #todo Some processes might not have ".exe"?
                string processWithExtension = $"{appWindowViewModels[indexOfUnmapped].ProcessName}.exe".ToLower();
                // #todo Try next subword first letter, e.g. "WindowsTerminal" => 'T' (while skipping 'W' since mapped to "Word").
                char processFirstCharLowercase = char.ToLower(appWindowViewModels[indexOfUnmapped].ProcessName.First());
                char processFirstCharUppercase = char.ToUpper(appWindowViewModels[indexOfUnmapped].ProcessName.First());

                // If not found and auto-mapping is enabled, handle auto-mapping.
                if(isAutoMappingEnabled
                    && !dynamicCharToAppList.ContainsKey(processFirstCharLowercase)
                    && !dynamicCharToAppList.ContainsKey(processFirstCharUppercase)
                ) {
                    bool isLowercaseEnabled = false; // #todo Move to settings.
                    char letter = isLowercaseEnabled
                        ? processFirstCharLowercase
                        : processFirstCharUppercase;

                    // #note LetterMapped and LetterMappedOrdinal remain as-is.
                    appWindowViewModels[indexOfUnmapped].LetterBound = letter.ToString();

                    // Found, so add to dynamic, but not all, just this app.
                    if(!dynamicCharToAppList.ContainsKey(letter)) {
                        dynamicCharToAppList[letter] = [];
                    }
                    dynamicCharToAppList[letter].Add(processWithExtension);
                }
            }

            return appWindowViewModels;
        }
    }
}
