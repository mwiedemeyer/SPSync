using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core
{
    [Serializable]
    public class SyncConfigurationNotFoundException : Exception
    {
        public SyncConfigurationNotFoundException() { }
        public SyncConfigurationNotFoundException(string message) : base(message) { }
        public SyncConfigurationNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected SyncConfigurationNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
