using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSpace {
    public interface IAsyncInitialization {
        /// <summary>
        /// The result of the asynchronous initialization of this instance.
        /// </summary>
        Task Initialization { get; }
    }
}
