using System.Collections.Generic;

namespace TaskSpace.Core.Matchers
{
    public class MatchResult
    {
        public bool Matched { get; set; }
        public int Score { get; set; }
        public IList<StringPart> StringParts { get; set; }

        public MatchResult()
        {
            StringParts = new List<StringPart>();
        }
    }
}