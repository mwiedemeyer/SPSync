using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPSync
{
    internal class NotifyStatusEventArgs : EventArgs
    {
        internal string Message { get; set; }
        internal Hardcodet.Wpf.TaskbarNotification.BalloonIcon Icon { get; set; }

        public NotifyStatusEventArgs(string message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon icon)
        {
            this.Message = message;
            this.Icon = icon;
        }
    }
    internal class NotifyInvalidCredentialsEventArgs : EventArgs
    {
        internal SyncViewModel SyncConfiguration { get; set; }

        public NotifyInvalidCredentialsEventArgs(SyncViewModel configuration)
        {
            this.SyncConfiguration = configuration;
        }
    }
}
