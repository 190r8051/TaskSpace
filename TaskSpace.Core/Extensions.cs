using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace TaskSpace.Core {
    public static class Extensions {
        public static IEnumerable<Keys> ToRangeList(
            this Keys @this
            , int limit = 0
        ) {
#pragma warning disable CA1416 // Validate platform compatibility.
            List<Keys> retVal = [];

            if(limit == 0) {
                // Return an empty list when the step is 0.
                return retVal;
            }

            int startKeyValue = (int)@this;

            if(limit < 0) {
                // Reverse iteration.
                for(int i = 0; limit < i; --i) {
                    retVal.Add((Keys)(startKeyValue + i));
                }
            }
            else {
                // Forward iteration.
                for(int i = 0; i < limit; i++) {
                    retVal.Add((Keys)(startKeyValue + i));
                }
            }

            return retVal;
#pragma warning restore CA1416 // Validate platform compatibility.
        }

        /// <summary>
        /// Returns the next Key in sequence, cycling through digits and letters.
        /// </summary>
        public static Keys Succ(this Keys @this) {
#pragma warning disable CA1416 // Validate platform compatibility.

            if(Keys.A <= @this && @this <= Keys.Z) {
                // Cycle through letters (A to Z).
                return @this == Keys.Z ? Keys.A : @this + 1;
            }
            if(Keys.NumPad0 <= @this && @this <= Keys.NumPad9) {
                // Cycle through NumPad digits (NumPad0 to NumPad9).
                return @this == Keys.NumPad9 ? Keys.NumPad0 : @this + 1;
            }
            else if(Keys.D0 <= @this && @this <= Keys.D9) {
                // Cycle through NumRow digits (D0 to D9).
                return @this == Keys.D9 ? Keys.D0 : @this + 1;
            }
            else if(Keys.F1 <= @this && @this <= Keys.F24) {
                // Cycle through function keys (F1 to F24). #todo Actually only up to F12?
                return @this == Keys.F24 ? Keys.F1 : @this + 1;
            }

            // Return original key if no match (default behavior)
            return @this;
#pragma warning restore CA1416 // Validate platform compatibility.
        }

        public static System.Windows.Input.Key ToKey(this System.Windows.Forms.Keys @this) {
#pragma warning disable CA1416 // Validate platform compatibility.
            return @this switch {
                // Numpad keys.
                System.Windows.Forms.Keys.NumPad0 => System.Windows.Input.Key.NumPad0,
                System.Windows.Forms.Keys.NumPad1 => System.Windows.Input.Key.NumPad1,
                System.Windows.Forms.Keys.NumPad2 => System.Windows.Input.Key.NumPad2,
                System.Windows.Forms.Keys.NumPad3 => System.Windows.Input.Key.NumPad3,
                System.Windows.Forms.Keys.NumPad4 => System.Windows.Input.Key.NumPad4,
                System.Windows.Forms.Keys.NumPad5 => System.Windows.Input.Key.NumPad5,
                System.Windows.Forms.Keys.NumPad6 => System.Windows.Input.Key.NumPad6,
                System.Windows.Forms.Keys.NumPad7 => System.Windows.Input.Key.NumPad7,
                System.Windows.Forms.Keys.NumPad8 => System.Windows.Input.Key.NumPad8,
                System.Windows.Forms.Keys.NumPad9 => System.Windows.Input.Key.NumPad9,
                System.Windows.Forms.Keys.Multiply => System.Windows.Input.Key.Multiply,
                System.Windows.Forms.Keys.Add => System.Windows.Input.Key.Add,
                System.Windows.Forms.Keys.Subtract => System.Windows.Input.Key.Subtract,
                System.Windows.Forms.Keys.Decimal => System.Windows.Input.Key.Decimal,
                System.Windows.Forms.Keys.Divide => System.Windows.Input.Key.Divide,

                // Numrow keys.
                System.Windows.Forms.Keys.D0 => System.Windows.Input.Key.D0,
                System.Windows.Forms.Keys.D1 => System.Windows.Input.Key.D1,
                System.Windows.Forms.Keys.D2 => System.Windows.Input.Key.D2,
                System.Windows.Forms.Keys.D3 => System.Windows.Input.Key.D3,
                System.Windows.Forms.Keys.D4 => System.Windows.Input.Key.D4,
                System.Windows.Forms.Keys.D5 => System.Windows.Input.Key.D5,
                System.Windows.Forms.Keys.D6 => System.Windows.Input.Key.D6,
                System.Windows.Forms.Keys.D7 => System.Windows.Input.Key.D7,
                System.Windows.Forms.Keys.D8 => System.Windows.Input.Key.D8,
                System.Windows.Forms.Keys.D9 => System.Windows.Input.Key.D9,

                // Letter keys.
                System.Windows.Forms.Keys.A => System.Windows.Input.Key.A,
                System.Windows.Forms.Keys.B => System.Windows.Input.Key.B,
                System.Windows.Forms.Keys.C => System.Windows.Input.Key.C,
                System.Windows.Forms.Keys.D => System.Windows.Input.Key.D,
                System.Windows.Forms.Keys.E => System.Windows.Input.Key.E,
                System.Windows.Forms.Keys.F => System.Windows.Input.Key.F,
                System.Windows.Forms.Keys.G => System.Windows.Input.Key.G,
                System.Windows.Forms.Keys.H => System.Windows.Input.Key.H,
                System.Windows.Forms.Keys.I => System.Windows.Input.Key.I,
                System.Windows.Forms.Keys.J => System.Windows.Input.Key.J,
                System.Windows.Forms.Keys.K => System.Windows.Input.Key.K,
                System.Windows.Forms.Keys.L => System.Windows.Input.Key.L,
                System.Windows.Forms.Keys.M => System.Windows.Input.Key.M,
                System.Windows.Forms.Keys.N => System.Windows.Input.Key.N,
                System.Windows.Forms.Keys.O => System.Windows.Input.Key.O,
                System.Windows.Forms.Keys.P => System.Windows.Input.Key.P,
                System.Windows.Forms.Keys.Q => System.Windows.Input.Key.Q,
                System.Windows.Forms.Keys.R => System.Windows.Input.Key.R,
                System.Windows.Forms.Keys.S => System.Windows.Input.Key.S,
                System.Windows.Forms.Keys.T => System.Windows.Input.Key.T,
                System.Windows.Forms.Keys.U => System.Windows.Input.Key.U,
                System.Windows.Forms.Keys.V => System.Windows.Input.Key.V,
                System.Windows.Forms.Keys.W => System.Windows.Input.Key.W,
                System.Windows.Forms.Keys.X => System.Windows.Input.Key.X,
                System.Windows.Forms.Keys.Y => System.Windows.Input.Key.Y,
                System.Windows.Forms.Keys.Z => System.Windows.Input.Key.Z,

                // Common control keys.
                System.Windows.Forms.Keys.Enter => System.Windows.Input.Key.Enter,
                System.Windows.Forms.Keys.Escape => System.Windows.Input.Key.Escape,
                System.Windows.Forms.Keys.Space => System.Windows.Input.Key.Space,
                System.Windows.Forms.Keys.Tab => System.Windows.Input.Key.Tab,
                System.Windows.Forms.Keys.Back => System.Windows.Input.Key.Back,
                System.Windows.Forms.Keys.Delete => System.Windows.Input.Key.Delete,
                System.Windows.Forms.Keys.Insert => System.Windows.Input.Key.Insert,

                // Modifier keys.
                System.Windows.Forms.Keys.ControlKey => System.Windows.Input.Key.LeftCtrl,
                System.Windows.Forms.Keys.ShiftKey => System.Windows.Input.Key.LeftShift,
                System.Windows.Forms.Keys.Menu => System.Windows.Input.Key.LeftAlt,

                // Function keys.
                System.Windows.Forms.Keys.F1 => System.Windows.Input.Key.F1,
                System.Windows.Forms.Keys.F2 => System.Windows.Input.Key.F2,
                System.Windows.Forms.Keys.F3 => System.Windows.Input.Key.F3,
                System.Windows.Forms.Keys.F4 => System.Windows.Input.Key.F4,
                System.Windows.Forms.Keys.F5 => System.Windows.Input.Key.F5,
                System.Windows.Forms.Keys.F6 => System.Windows.Input.Key.F6,
                System.Windows.Forms.Keys.F7 => System.Windows.Input.Key.F7,
                System.Windows.Forms.Keys.F8 => System.Windows.Input.Key.F8,
                System.Windows.Forms.Keys.F9 => System.Windows.Input.Key.F9,
                System.Windows.Forms.Keys.F10 => System.Windows.Input.Key.F10,
                System.Windows.Forms.Keys.F11 => System.Windows.Input.Key.F11,
                System.Windows.Forms.Keys.F12 => System.Windows.Input.Key.F12,

                // Arrow keys.
                System.Windows.Forms.Keys.Up => System.Windows.Input.Key.Up,
                System.Windows.Forms.Keys.Down => System.Windows.Input.Key.Down,
                System.Windows.Forms.Keys.Left => System.Windows.Input.Key.Left,
                System.Windows.Forms.Keys.Right => System.Windows.Input.Key.Right,

                // Page keys.
                System.Windows.Forms.Keys.PageUp => System.Windows.Input.Key.PageUp,
                System.Windows.Forms.Keys.PageDown => System.Windows.Input.Key.PageDown,

                _ => System.Windows.Input.Key.None
            };
#pragma warning restore CA1416 // Validate platform compatibility
        }

        public static System.Windows.Forms.Keys ToKeys(this System.Windows.Input.Key @this) {
#pragma warning disable CA1416 // Validate platform compatibility.
            return @this switch {
                // Numpad keys.
                System.Windows.Input.Key.NumPad0 => System.Windows.Forms.Keys.NumPad0,
                System.Windows.Input.Key.NumPad1 => System.Windows.Forms.Keys.NumPad1,
                System.Windows.Input.Key.NumPad2 => System.Windows.Forms.Keys.NumPad2,
                System.Windows.Input.Key.NumPad3 => System.Windows.Forms.Keys.NumPad3,
                System.Windows.Input.Key.NumPad4 => System.Windows.Forms.Keys.NumPad4,
                System.Windows.Input.Key.NumPad5 => System.Windows.Forms.Keys.NumPad5,
                System.Windows.Input.Key.NumPad6 => System.Windows.Forms.Keys.NumPad6,
                System.Windows.Input.Key.NumPad7 => System.Windows.Forms.Keys.NumPad7,
                System.Windows.Input.Key.NumPad8 => System.Windows.Forms.Keys.NumPad8,
                System.Windows.Input.Key.NumPad9 => System.Windows.Forms.Keys.NumPad9,
                System.Windows.Input.Key.Multiply => System.Windows.Forms.Keys.Multiply,
                System.Windows.Input.Key.Add => System.Windows.Forms.Keys.Add,
                System.Windows.Input.Key.Subtract => System.Windows.Forms.Keys.Subtract,
                System.Windows.Input.Key.Decimal => System.Windows.Forms.Keys.Decimal,
                System.Windows.Input.Key.Divide => System.Windows.Forms.Keys.Divide,

                // Numrow keys.
                System.Windows.Input.Key.D0 => System.Windows.Forms.Keys.D0,
                System.Windows.Input.Key.D1 => System.Windows.Forms.Keys.D1,
                System.Windows.Input.Key.D2 => System.Windows.Forms.Keys.D2,
                System.Windows.Input.Key.D3 => System.Windows.Forms.Keys.D3,
                System.Windows.Input.Key.D4 => System.Windows.Forms.Keys.D4,
                System.Windows.Input.Key.D5 => System.Windows.Forms.Keys.D5,
                System.Windows.Input.Key.D6 => System.Windows.Forms.Keys.D6,
                System.Windows.Input.Key.D7 => System.Windows.Forms.Keys.D7,
                System.Windows.Input.Key.D8 => System.Windows.Forms.Keys.D8,
                System.Windows.Input.Key.D9 => System.Windows.Forms.Keys.D9,

                // Letter keys.
                System.Windows.Input.Key.A => System.Windows.Forms.Keys.A,
                System.Windows.Input.Key.B => System.Windows.Forms.Keys.B,
                System.Windows.Input.Key.C => System.Windows.Forms.Keys.C,
                System.Windows.Input.Key.D => System.Windows.Forms.Keys.D,
                System.Windows.Input.Key.E => System.Windows.Forms.Keys.E,
                System.Windows.Input.Key.F => System.Windows.Forms.Keys.F,
                System.Windows.Input.Key.G => System.Windows.Forms.Keys.G,
                System.Windows.Input.Key.H => System.Windows.Forms.Keys.H,
                System.Windows.Input.Key.I => System.Windows.Forms.Keys.I,
                System.Windows.Input.Key.J => System.Windows.Forms.Keys.J,
                System.Windows.Input.Key.K => System.Windows.Forms.Keys.K,
                System.Windows.Input.Key.L => System.Windows.Forms.Keys.L,
                System.Windows.Input.Key.M => System.Windows.Forms.Keys.M,
                System.Windows.Input.Key.N => System.Windows.Forms.Keys.N,
                System.Windows.Input.Key.O => System.Windows.Forms.Keys.O,
                System.Windows.Input.Key.P => System.Windows.Forms.Keys.P,
                System.Windows.Input.Key.Q => System.Windows.Forms.Keys.Q,
                System.Windows.Input.Key.R => System.Windows.Forms.Keys.R,
                System.Windows.Input.Key.S => System.Windows.Forms.Keys.S,
                System.Windows.Input.Key.T => System.Windows.Forms.Keys.T,
                System.Windows.Input.Key.U => System.Windows.Forms.Keys.U,
                System.Windows.Input.Key.V => System.Windows.Forms.Keys.V,
                System.Windows.Input.Key.W => System.Windows.Forms.Keys.W,
                System.Windows.Input.Key.X => System.Windows.Forms.Keys.X,
                System.Windows.Input.Key.Y => System.Windows.Forms.Keys.Y,
                System.Windows.Input.Key.Z => System.Windows.Forms.Keys.Z,

                // Common control keys.
                System.Windows.Input.Key.Enter => System.Windows.Forms.Keys.Enter,
                System.Windows.Input.Key.Escape => System.Windows.Forms.Keys.Escape,
                System.Windows.Input.Key.Space => System.Windows.Forms.Keys.Space,
                System.Windows.Input.Key.Tab => System.Windows.Forms.Keys.Tab,
                System.Windows.Input.Key.Back => System.Windows.Forms.Keys.Back,
                System.Windows.Input.Key.Delete => System.Windows.Forms.Keys.Delete,
                System.Windows.Input.Key.Insert => System.Windows.Forms.Keys.Insert,

                // Modifier keys.
                System.Windows.Input.Key.LeftCtrl => System.Windows.Forms.Keys.ControlKey,
                System.Windows.Input.Key.LeftShift => System.Windows.Forms.Keys.ShiftKey,
                System.Windows.Input.Key.LeftAlt => System.Windows.Forms.Keys.Menu,

                // Function keys.
                System.Windows.Input.Key.F1 => System.Windows.Forms.Keys.F1,
                System.Windows.Input.Key.F2 => System.Windows.Forms.Keys.F2,
                System.Windows.Input.Key.F3 => System.Windows.Forms.Keys.F3,
                System.Windows.Input.Key.F4 => System.Windows.Forms.Keys.F4,
                System.Windows.Input.Key.F5 => System.Windows.Forms.Keys.F5,
                System.Windows.Input.Key.F6 => System.Windows.Forms.Keys.F6,
                System.Windows.Input.Key.F7 => System.Windows.Forms.Keys.F7,
                System.Windows.Input.Key.F8 => System.Windows.Forms.Keys.F8,
                System.Windows.Input.Key.F9 => System.Windows.Forms.Keys.F9,
                System.Windows.Input.Key.F10 => System.Windows.Forms.Keys.F10,
                System.Windows.Input.Key.F11 => System.Windows.Forms.Keys.F11,
                System.Windows.Input.Key.F12 => System.Windows.Forms.Keys.F12,

                // Arrow keys.
                System.Windows.Input.Key.Up => System.Windows.Forms.Keys.Up,
                System.Windows.Input.Key.Down => System.Windows.Forms.Keys.Down,
                System.Windows.Input.Key.Left => System.Windows.Forms.Keys.Left,
                System.Windows.Input.Key.Right => System.Windows.Forms.Keys.Right,

                // Page keys.
                System.Windows.Input.Key.PageUp => System.Windows.Forms.Keys.PageUp,
                System.Windows.Input.Key.PageDown => System.Windows.Forms.Keys.PageDown,

                _ => System.Windows.Forms.Keys.None
            };
#pragma warning restore CA1416 // Validate platform compatibility.
        }


        public static Key ToKey(this string @this) {
            if(Enum.TryParse<Key>(@this, out Key result)) {
                return result;
            }

            throw new ArgumentException("Invalid key name", nameof(@this));
        }

        /// <summary>
        /// Transforms the 1-based index to 1-based index string which represents a key on the keyboard,
        /// e.g. either 1..9 digit, or 0 (meaning 1-index 10), or F1+ keys.
        /// [example]
        /// - 1 => "1".
        /// - 9 => "9".
        /// - 10 => "0" (but meaning an item at 1-based index 10).
        /// - 11 => "F1" (meaning an item at 1-based index 11).
        /// - 19 => "F9" (meaning an item at 1-based index 19).
        /// - 20 => "F10" (meaning an item at 1-based index 20).
        /// - 21 => "F11" (meaning an item at 1-based index 21).
        /// - 22 => "F12" (meaning an item at 1-based index 22).
        /// </summary>
        /// <param name="this">[this] This integer.</param>
        /// <returns>The 1-based index string.</returns>
        public static string Get1BasedIndexString(this int @this) {
            string retVal = @this.ToString();
            if(10 == @this) {
                // Special case: 1-based index 10 maps to digit "0".
                retVal = "0";
            }
            else if(10 < @this) {
                int functionIndex = @this - 10;
                retVal = $"F{functionIndex}"; // #todo Use the small "f" unicode char?
            }

            return retVal;
        }

        public static string FindLongestCommonSubstring(this List<string> @this) {
            if(@this == null || !@this.Any()) {
                return string.Empty;
            }

            // Split all strings into lists of words.
            List<List<string>> allWords = @this
                .Select(str => str.Split(' ').ToList())
                .ToList();

            // Find the shortest list to minimize comparisons.
            List<string> shortestList = allWords.OrderBy(list => list.Count).First();

            string retVal = string.Empty;
            int maxLength = 0;

            for(int start = 0; start < shortestList.Count; start++) {
                for(int end = start; end < shortestList.Count; end++) {
                    List<string> candidateSubsequence = shortestList.GetRange(start, end - start + 1);

                    if(maxLength < candidateSubsequence.Count
                        && allWords.All(words => words.ContainsSubsequence(candidateSubsequence))
                    ) {
                        maxLength = candidateSubsequence.Count;

                        string candidateString = string.Join(" ", candidateSubsequence);
                        retVal = candidateString;
                    }
                }
            }

            return retVal;
        }

        // Helper method to check if a list of words contains a subsequence.
        public static bool ContainsSubsequence(
            this List<string> @this
            , List<string> subsequence
        ) {
            int found = 0;
            foreach(string word in @this) {
                if(word == subsequence[found]) {
                    found++;
                    if(found == subsequence.Count) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Splits this string into the camel-case words.
        /// </summary>
        /// <sources>
        /// https://github.com/peter-frentrup/NppMenuSearch
        /// </sources>
        /// <param name="this">[this] This string.</param>
        /// <returns>The enumerable with words.</returns>
        public static IEnumerable<string> SplitCamelCaseWords(
            this string @this
        ) {
            int pos = 0;
            while(pos < @this.Length) {
                int next = pos + 1;
                while(next < @this.Length) {
                    if('A' <= @this[next] && @this[next] <= 'Z') {
                        break;
                    }

                    next++;
                }

                if(next == pos + 1) {
                    while(next < @this.Length) {
                        if(!('A' <= @this[next] && @this[next] <= 'Z')) {
                            break;
                        }

                        next++;
                    }
                }

                yield return @this.Substring(pos, next - pos);
                pos = next;
            }
        }

    }
}
