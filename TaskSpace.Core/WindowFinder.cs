using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TaskSpace.Core {
    public class WindowFinder {
        public static List<AppWindowViewModel> GetAndMapWindows(
            List<string> blockList
            , Dictionary<char, List<AppWindowViewModel>> settingsCharToAppList
            , bool isAutoMappingEnabled = false
            , bool isPowerMenuEnabled = false
        ) {
            // The schema is:
            // - The dictionary key is char, e.g. 'V' for "Visual Studio" etc.
            // - The dictionary value is a tuple:
            //      - IsFoundInSettings: if true, this app is found in the settings (the char will not be auto-mapped to non-mapped apps).
            //      - DynamicApps: the apps mapped to this char.
            Dictionary<char, (bool IsFoundInSettings, List<AppWindowViewModel> DynamicApps)> dynamicCharToAppList = [];

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
                string processFilePath = string.Empty;
                try {
                    processFilePath = $"{appWindow.Process.MainModule.FileName}.exe".ToLower();
                }
                catch {
                }

                string processFileNameWithExt = $"{appWindow.ProcessName}.exe".ToLower();
                char processFirstCharLowercase = char.ToLower(processFileNameWithExt.First());
                char processFirstCharUppercase = char.ToUpper(processFileNameWithExt.First());

                // Attempt to find the letter mapping from settingsCharToAppList.
                // #todo #perf In LoadSettings, pivot, e.g. can input key "devenv.exe" and get the mapped char 'V' (then can look-up here).
                for(int i = (int)'A'; i <= (int)'Z'; i++) {
                    char letter = (char)i;

#if DEBUG
                    if(letter == 'B') {
                        Debug.WriteLine("TARGET");
                    }
#endif

                    if(settingsCharToAppList.TryGetValue(letter, out List<AppWindowViewModel> appList)) {
                        // #warning This look-up relies on settings apps having IsLaunchCommand=true (so that the backing field is used, not the property with extra logic).
                        //int ordinal1 = appList.FindIndex(x => x.AppFilePath == processFilePath);

                        //if(ordinal1 != -1) {
                        //    AppWindowViewModel app = appList[ordinal1];

                        //    // Found, so add to dynamic.
                        //    if(!dynamicCharToAppList.ContainsKey(letter)) {
                        //        // #note Item1 (IsFoundInSettings) is true, meaning this char mapping is static from settings (and will be NOT re-used for any dynamic apps).
                        //        (bool, List<AppWindowViewModel>) val = (true, new List<AppWindowViewModel>());
                        //        dynamicCharToAppList[letter] = val;
                        //    }
                        //    dynamicCharToAppList[letter].DynamicApps.Add(app);

                        //    letterMappedOrdinal = ordinal1;
                        //    letterMapped = letter.ToString();
                        //    letterBound = letter.ToString();
                        //    isFound = true;
                        //    break;
                        //}
                        //else {
                        int ordinal2 = appList.FindIndex(x => x.AppFileNameWithExt == processFileNameWithExt);

                        if(ordinal2 != -1) {
                            AppWindowViewModel app = appList[ordinal2];

                            // Found, so add to dynamic.
                            if(!dynamicCharToAppList.ContainsKey(letter)) {
                                // #note Item1 (IsFoundInSettings) is true, meaning this char mapping is static from settings (and will be NOT re-used for any dynamic apps).
                                (bool, List<AppWindowViewModel>) val = (true, new List<AppWindowViewModel>());
                                dynamicCharToAppList[letter] = val;
                            }
                            dynamicCharToAppList[letter].DynamicApps.Add(app);

                            letterMappedOrdinal = ordinal2;
                            letterMapped = letter.ToString();
                            letterBound = letter.ToString();
                            isFound = true;
                            break;
                        }
                        //}
                    }
                }

                if(!isFound) {
                    indexesOfUnmapped.Add(i0);
                }

                // Create and add the AppWindowViewModel instance to the list.
                // #note This is an independent dynamic copy (i.e. NOT the static one from the settings).
                AppWindowViewModel appWindowViewModel = new(appWindow, letterMapped, letterMappedOrdinal, letterBound);

                appWindowViewModels.Add(appWindowViewModel);
            }

            // #DEBUG[Test closing an app here, but still sending-back the mapped app, then what happens if mapped letter is hit?]
            // #bug? What happens if MainWindow is active, but in the meantime, some process shuts-down an app which is being selected? Crash or ignore?

            // #future? New mode: all pre-mapped apps are always at the top (maybe even a line separating from auto-mapped).

            foreach(int indexOfUnmapped in indexesOfUnmapped) {
                // #todo Some processes might not have ".exe"?
                string appNameExt = $"{appWindowViewModels[indexOfUnmapped].AppFileNameWithExt}.exe".ToLower();
                string appFileName = $"{appWindowViewModels[indexOfUnmapped].AppFilePath}.exe".ToLower();

                char processFirstCharLowercase = char.ToLower(appWindowViewModels[indexOfUnmapped].AppFileNameWithExt.First());
                char processFirstCharUppercase = char.ToUpper(appWindowViewModels[indexOfUnmapped].AppFileNameWithExt.First());

                IEnumerable<string> processNameWords = appWindowViewModels[indexOfUnmapped].AppFileNameWithExt.SplitCamelCaseWords();
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

                        // #note LetterMappedOrdinal remain as-is.
                        appWindowViewModels[indexOfUnmapped].LetterBound = letter.ToString();
                        appWindowViewModels[indexOfUnmapped].LetterMapped = letter.ToString();

                        // Found, so add to dynamic.
                        if(!dynamicCharToAppList.ContainsKey(letter)) {
                            // #note Item1 is false, meaning this is dynamic (and will be re-used for any other dynamic apps).
                            //(bool, List<(string, string)>) val = (false, new List<(string, string)>());
                            (bool, List<AppWindowViewModel>) val = (false, new List<AppWindowViewModel>());
                            dynamicCharToAppList[letter] = val;
                        }

                        dynamicCharToAppList[letter].Item2.Add(appWindowViewModels[indexOfUnmapped]); // #TODO??? Should clear the LetterBound and LetterMapped?
                        //dynamicCharToAppList[letter].Item2.Add((appNameExt, string.Empty)); // #TODO!!!
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
                            //(bool, List<(string, string)>) val = (false, new List<(string, string)>());
                            (bool, List<AppWindowViewModel>) val = (false, new List<AppWindowViewModel>());
                            dynamicCharToAppList[letter] = val;
                        }

                        dynamicCharToAppList[letter].Item2.Add(appWindowViewModels[indexOfUnmapped]); // #TODO??? Should clear the LetterBound and LetterMapped?
                        //dynamicCharToAppList[letter].Item2.Add((appNameExt, string.Empty)); // #TODO!!!
                    }
                }
            } //^^^ foreach(int indexOfUnmapped in indexesOfUnmapped) { ^^^

            if(isPowerMenuEnabled) {
                AppWindowViewModel appWindowViewModel = new(
                    launchCommand: "Power Menu"
                    , letter: ',' // #TODO### Get from param.
                    , icon: null // #TODO###
                );

                appWindowViewModels.Add(appWindowViewModel);
            }

            return appWindowViewModels;
        }
    }
}
