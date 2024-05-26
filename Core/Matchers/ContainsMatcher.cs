using System.Text.RegularExpressions;

namespace TaskSpace.Core.Matchers {
    public class ContainsMatcher : IMatcher {
        public MatchResult Evaluate(string input, string pattern) {
            if(input == null || pattern == null) {
                return NonMatchResult(input);
            }

            Match match = Regex.Match(input, "(.*)(" + Regex.Escape(pattern) + ")(.*)", RegexOptions.IgnoreCase);

            if(!match.Success) {
                return NonMatchResult(input);
            }

            MatchResult matchResult = new MatchResult();
            if(0 < match.Groups[1].Length) {
                matchResult.StringParts.Add(new StringPart(match.Groups[1].Value));
            }

            if(0 < match.Groups[2].Length) {
                matchResult.StringParts.Add(new StringPart(match.Groups[2].Value, true));
            }

            if(0 < match.Groups[3].Length) {
                matchResult.StringParts.Add(new StringPart(match.Groups[3].Value));
            }

            matchResult.Matched = true;
            matchResult.Score = 2;

            return matchResult;
        }

        private static MatchResult NonMatchResult(string input) {
            MatchResult matchResult = new MatchResult();
            if(input != null) {
                matchResult.StringParts.Add(new StringPart(input));
            }

            return matchResult;
        }
    }
}
