using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using SPSync.Core;

namespace SPSync
{
    /// <summary>
    /// Interaction logic for DownloadWindow.xaml
    /// </summary>
    public partial class DownloadWindow : Window
    {
        public DownloadWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            var localFolder = System.IO.Path.GetDirectoryName(args[1]);
            var filename = System.IO.Path.GetFileName(args[1]);

            var fileDisplayName = filename.Replace(".spsync", string.Empty);
            var extension = System.IO.Path.GetExtension(fileDisplayName);
            if (fileDisplayName.Length > 12)
                fileDisplayName = fileDisplayName.Substring(0, 12);
            TextBlockStatus.Text = String.Format("Downloading '{0}(...){1}'...", fileDisplayName, extension);

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object state)
            {
                try
                {
                    SyncManager sync = new SyncManager(localFolder);
                    sync.DownloadFile(filename);
                }
                catch { }
                finally
                {
                    this.Dispatcher.Invoke(new Action(delegate
                    {
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    }));
                }
            }));
        }
    }
}
