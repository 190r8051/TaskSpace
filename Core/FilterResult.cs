using System.Collections.Generic;
using TaskSpace.Core.Matchers;

namespace TaskSpace.Core
{
    public class FilterResult<T> where T : IWindowText
    {
        public T AppWindow { get; set; }
        public IList<MatchResult> WindowTitleMatchResults { get; set; }
        public IList<MatchResult> ProcessTitleMatchResults { get; set; }
    }
}