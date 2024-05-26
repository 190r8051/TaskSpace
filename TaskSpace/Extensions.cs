using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TaskSpace {
    public static class Extensions {
        public static Key ToKey(this string @this) {
            if(Enum.TryParse<Key>(@this, out Key result)) {
                return result;
            }

            throw new ArgumentException("Invalid key name", nameof(@this));
        }

        /// <summary>
        /// Transforms the 1-based index to 1-based index string which represents a key on the keyboard,
        /// eg either 1..9 digit, or 0 (meaning 1-index 10), or F1+ keys.
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
                retVal = $"F{functionIndex}"; // [todo] Use the small "f" unicode char?
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
    }
}
