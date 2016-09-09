using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPSync.Core.Common
{
    public enum SyncDirection : int
    {
        Both = 0,
        LocalToRemote = 1,
        RemoteToLocal = 2
    }
}
