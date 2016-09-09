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
using SPSync.Core;
using SPSync.Core.Common;
using SPSync.Core.Metadata;

namespace SPSync
{
    /// <summary>
    /// Interaction logic for ConflictWindow.xaml
    /// </summary>
    public partial class ConflictWindow : Window
    {
        private MetadataItem viewModel;
        private SyncConfiguration configuration;

        public ConflictWindow()
        {
            InitializeComponent();
        }

        internal bool? ShowDialog(SyncConfiguration syncConfiguration, MetadataItem metadataItem)
        {
            configuration = syncConfiguration;
            viewModel = metadataItem;
            DataContext = viewModel;
            return ShowDialog();
        }

        public ItemStatus ItemStatus { get; private set; }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var remember = checkBoxForAll.IsChecked.Value;

            if (button.Name == "buttonOverwriteLocal")
            {
                ItemStatus = ItemStatus.UpdatedRemote;
            }
            else if (button.Name == "buttonOverwriteRemote")
            {
                ItemStatus = ItemStatus.UpdatedLocal;
            }
            else if (button.Name == "buttonCancel")
            {
                ItemStatus = ItemStatus.Conflict;
            }

            if (remember)
                App.MainViewModel.AllNextConflictsCache[configuration.LocalFolder] = ItemStatus;

            DialogResult = true;
            Close();
        }

        private void textBlockName_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var relativeFile = viewModel.LocalFile.Replace(configuration.LocalFolder, string.Empty);
            var relFileUrl = relativeFile.Replace('\\', '/');
            var docLibUrl = configuration.SiteUrl.EndsWith("/") ? configuration.SiteUrl + configuration.DocumentLibrary : configuration.SiteUrl + "/" + configuration.DocumentLibrary;
            System.Diagnostics.Process.Start(docLibUrl.EndsWith("/") ? docLibUrl + relFileUrl : docLibUrl + "/" + relFileUrl);
        }

        private void textBlockLocalFolder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(viewModel.LocalFolder);
        }
    }
}
