using System.Collections.Generic;
using System.Linq;

namespace TaskSpace.Core {
    public class WindowFinder {
        public List<AppWindow> GetWindows(List<string> blockList) {
            return AppWindow.AllToplevelWindows
                .Where(a => a.IsAltTabWindow(blockList))
                .ToList();
        }
    }
}
