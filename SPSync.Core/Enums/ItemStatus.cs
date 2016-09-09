using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core.Common
{
    public enum ItemStatus : int
    {
        Unchanged = 0,
        UpdatedLocal = 1,
        UpdatedRemote = 2,
        DeletedLocal = 3,
        DeletedRemote = 4,
        Conflict = 5,
        RenamedRemote = 6,
        RenamedLocal = 7
    }
}
