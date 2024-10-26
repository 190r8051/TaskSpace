using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TaskSpace.Core {
    public class WindowFinder {
        public List<AppWindowViewModel> GetAndMapWindows(
            List<string> blockList
            , Dictionary<char, List<(string, string)>> settingsCharToAppList
            , bool isAutoMappingEnabled = false
        ) {
            Dictionary<char, (bool, List<(string, string)>)> dynamicCharToAppList = [];

            // Get the valid windows.
            IEnumerable<AppWindow> appWindows = ManagedWinapi.Windows.SystemWindow
                .AllToplevelWindows // Get raw windows.
                .Select(systemWindow => new AppWindow(systemWindow.HWnd)) // Get appWindows.
                .Where(a => a.IsAltTabbableWindow(blockList)); // Get alt-tabbable appWindows.

            List<AppWindowViewModel> appWindowViewModels = [];

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

                    if(settingsCharToAppList.TryGetValue(letter, out List<(string, string)> appList)) {
                        int ordinal = appList.FindIndex(x => x.Item1 == processWithExtension);

                        if(ordinal != -1) {
                            (string appNameExtLocal, string appFilePath) app = appList[ordinal];

                            // Found, so add to dynamic.
                            if(!dynamicCharToAppList.ContainsKey(letter)) {
                                // #note Item1 is true, meaning this is static (and will be NOT re-used for any dynamic apps).
                                (bool, List<(string, string)>) val = (true, new List<(string, string)>());
                                dynamicCharToAppList[letter] = val;
                            }
                            dynamicCharToAppList[letter].Item2.Add(app);

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
                string appNameExt = $"{appWindowViewModels[indexOfUnmapped].AppFileNameExt}.exe".ToLower();
                string appFileName = $"{appWindowViewModels[indexOfUnmapped].AppFilePath}.exe".ToLower();

                char processFirstCharLowercase = char.ToLower(appWindowViewModels[indexOfUnmapped].AppFileNameExt.First());
                char processFirstCharUppercase = char.ToUpper(appWindowViewModels[indexOfUnmapped].AppFileNameExt.First());

                IEnumerable<string> processNameWords = appWindowViewModels[indexOfUnmapped].AppFileNameExt.SplitCamelCaseWords();
                char? processSecondWordFirstCharLowercase = 2 <= processNameWords.Count() ? char.ToLower(processNameWords.ElementAt(1).First()) : null;
                char? processSecondWordFirstCharUppercase = 2 <= processNameWords.Count() ? char.ToUpper(processNameWords.ElementAt(1).First()) : null;

                // If not found and auto-mapping is enabled, try to auto-map.
                if(isAutoMappingEnabled) {
                    if((!dynamicCharToAppList.ContainsKey(processFirstCharUppercase) || dynamicCharToAppList[processFirstCharUppercase].Item1 == false)
                    //&& (!dynamicCharToAppList.ContainsKey(processFirstCharLowercase) || dynamicCharToAppList[processFirstCharLowercase].Item1 == false)
                    ) {
                        bool isLowercaseEnabled = false; // #todo Move to settings.
                        char letter = isLowercaseEnabled
                            ? processFirstCharLowercase
                            : processFirstCharUppercase;

                        // #note LetterMapped and LetterMappedOrdinal remain as-is.
                        appWindowViewModels[indexOfUnmapped].LetterBound = letter.ToString();

                        // Found, so add to dynamic.
                        if(!dynamicCharToAppList.ContainsKey(letter)) {
                            // #note Item1 is false, meaning this is dynamic (and will be re-used for any other dynamic apps).
                            (bool, List<(string, string)>) val = (false, new List<(string, string)>());
                            dynamicCharToAppList[letter] = val;
                        }
                        dynamicCharToAppList[letter].Item2.Add((appNameExt, string.Empty)); // #TODO!!!
                    }
                    else if(2 <= processNameWords.Count()
                        && (
                            (!dynamicCharToAppList.ContainsKey(processSecondWordFirstCharUppercase.Value) || dynamicCharToAppList[processSecondWordFirstCharUppercase.Value].Item1 == false)
                        //|| && (!dynamicCharToAppList.ContainsKey(processSecondWordFirstCharLowercase.Value) || dynamicCharToAppList[processSecondWordFirstCharLowercase.Value].Item1 == false)
                        )
                    ) {
                        bool isLowercaseEnabled = false; // #todo Move to settings.
                        char letter = isLowercaseEnabled
                            ? processSecondWordFirstCharLowercase.Value
                            : processSecondWordFirstCharUppercase.Value;

                        // #note LetterMapped and LetterMappedOrdinal remain as-is.
                        appWindowViewModels[indexOfUnmapped].LetterBound = letter.ToString();

                        // Found, so add to dynamic.
                        if(!dynamicCharToAppList.ContainsKey(letter)) {
                            // #note Item1 is false, meaning this is dynamic (and will be re-used for any other dynamic apps).
                            (bool, List<(string, string)>) val = (false, new List<(string, string)>());
                            dynamicCharToAppList[letter] = val;
                        }
                        dynamicCharToAppList[letter].Item2.Add((appNameExt, string.Empty)); // #TODO!!!
                    }
                }
            }

            return appWindowViewModels;
        }
    }
}
