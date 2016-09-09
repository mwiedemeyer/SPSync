using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core.Common
{
    [Serializable]
    public class ConflictData
    {
        public ConflictData() { }

        public DateTime RemoteLastModified { get; set; }

        public DateTime LocalLastModified { get; set; }

        public bool LocalIsNewer => LocalLastModified > RemoteLastModified;

        public bool RemoteIsNewer => RemoteLastModified > LocalLastModified;
    }
}
