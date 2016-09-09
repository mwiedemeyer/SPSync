using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core
{
    public enum ProgressStatus : int
    {
        Idle = 0,
        Analyzing = 1,
        Analyzed = 2,
        Running = 3,
        Conflict = 4,
        Error = 5,
        Completed = 6,
        Warning = 7
    }
}
