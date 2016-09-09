using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync.Core
{
    public enum AuthenticationType : int
    {
        NTLM = 0,
        ADFS = 1,
        Office365 = 2
    }
}
