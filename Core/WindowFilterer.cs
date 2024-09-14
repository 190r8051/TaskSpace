using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaskSpace.Core.Matchers;

namespace TaskSpace.Core {
    public class WindowFilterer {
        // #cut
        //public IEnumerable<FilterResult<T>> FilterByLetter<T>(WindowFilterContext<T> context, string letter) where T : IWindowText {

        //    string filterText = letter;
        //    string processFilterText = null;

        //    var temp1 = context.Windows
        //        .Where(w => w.Letter == letter)
        //        //.Select(
        //        //    w =>
        //        //        new {
        //        //            Window = w,
        //        //            ResultsTitle = new List<MatchResult>(), // #blank
        //        //            ResultsProcessTitle = new List<MatchResult>() // #blank
        //        //        }
        //        //)
        //        //.Where(predicate: r => {
        //        //    if(processFilterText == null) {
        //        //        return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
        //        //    }

        //        //    return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
        //        //})
        //        //.OrderByDescending(r =>
        //        //    r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score)
        //        //)
        //        .Select(
        //            w =>
        //                new {
        //                    Window = w,
        //                    ResultsTitle = Score(w.WindowTitle, filterText),
        //                    ResultsProcessTitle = Score(w.ProcessTitle, processFilterText ?? filterText)
        //                }
        //        )
        //        .OrderByDescending(r =>
        //            r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
        //        ;

        //    return temp1.Select(
        //           r =>
        //               new FilterResult<T> {
        //                   AppWindow = r.Window,
        //                   WindowTitleMatchResults = r.ResultsTitle,
        //                   ProcessTitleMatchResults = r.ResultsProcessTitle
        //               }
        //       );
        //}

        public IEnumerable<FilterResult<T>> Filter<T>(WindowFilterContext<T> context, string query) where T : IWindowText {
            string filterText = query;
            string processFilterText = null;

            string[] queryParts = query.Split(new[] { '.' }, 2);

            if(queryParts.Length != 2) {
                return context.Windows
                    .Select(
                        w => new {
                            Window = w,
                            ResultsTitle = Score(w.WindowTitle, filterText),
                            ResultsProcessTitle = Score(w.ProcessName, processFilterText ?? filterText)
                        }
                    )
                    .Where(predicate: r => {
                        if(processFilterText == null) {
                            return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
                        }

                        return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
                    })
                    .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                    .Select(
                        r => new FilterResult<T> {
                            AppWindow = r.Window,
                            WindowTitleMatchResults = r.ResultsTitle,
                            ProcessTitleMatchResults = r.ResultsProcessTitle
                        }
                    );
            }

            processFilterText = queryParts[0];
            if(processFilterText.Length == 0) {
                processFilterText = context.ForegroundWindowProcessTitle;
            }

            filterText = queryParts[1];

            return context.Windows
                .Select(
                    w => new {
                        Window = w,
                        ResultsTitle = Score(w.WindowTitle, filterText),
                        ResultsProcessTitle = Score(w.ProcessName, processFilterText ?? filterText)
                    }
                )
                .Where(r => {
                    if(processFilterText == null) {
                        return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
                    }

                    return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
                })
                .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                .Select(
                    r => new FilterResult<T> {
                        AppWindow = r.Window,
                        WindowTitleMatchResults = r.ResultsTitle,
                        ProcessTitleMatchResults = r.ResultsProcessTitle
                    }
                );
        }

        private static List<MatchResult> Score(string title, string filterText) {
            StartsWithMatcher startsWithMatcher = new StartsWithMatcher();
            ContainsMatcher containsMatcher = new ContainsMatcher();
            SignificantCharactersMatcher significantCharactersMatcher = new SignificantCharactersMatcher();

            // #bug? This seems to take-over preferrable matches like "TASk space" so that "TASk" is not even found (went down the fuzzy-search path).
            //IndividualCharactersMatcher individualCharactersMatcher = new IndividualCharactersMatcher();

            // [!] If the filterText is just a single space, DON'T filter.
            // [!] If the filterText is " t", filter based on "t".
            string filterTextLocal = filterText.TrimStart();

            List<MatchResult> results = new List<MatchResult> {
                startsWithMatcher.Evaluate(title, filterTextLocal),
                significantCharactersMatcher.Evaluate(title, filterTextLocal),
                containsMatcher.Evaluate(title, filterTextLocal)
                //individualCharactersMatcher.Evaluate(title, filterTextLocal)
            };

            return results;
        }
    }

    public class WindowFilterContext<T> where T : IWindowText {
        public string ForegroundWindowProcessTitle { get; set; }
        public IEnumerable<T> Windows { get; set; }
    }
}
