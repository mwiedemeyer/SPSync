using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core.Common
{
    public enum ConflictHandling : int
    {
        ManualConflictHandling = 0,
        OverwriteLocalChanges = 1,
        OverwriteRemoteChanges = 2,
    }
}
