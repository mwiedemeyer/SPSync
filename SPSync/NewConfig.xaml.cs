using SPSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SPSync
{
    public partial class NewConfig : Window
    {
        private SyncViewModel _viewModel;

        public NewConfig()
        {
            InitializeComponent();
        }

        internal bool? ShowNewConfigDialog()
        {
            _viewModel = new SyncViewModel(new SyncConfiguration());

            DataContext = _viewModel;

            return ShowDialog();
        }

        private async void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            var password = textBoxPassword.Password;
            SyncConfiguration config = null;

            buttonSave.IsEnabled = false;
            buttonSave.Content = "Please wait...";

            var stop = false;
            await Task.Run(() =>
            {
                try
                {
                    config = SharePointManager.TryFindConfiguration(_viewModel.SiteUrl, _viewModel.Username, password);
                }
                catch (Exception ex)
                {
                    stop = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        buttonSave.IsEnabled = true;
                        buttonSave.Content = "Next";
                    }));
                    MessageBox.Show("An error occured:" + Environment.NewLine + ex.Message, "SPSync", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            if (stop)
                return;

            if (config == null)
            {
                MessageBox.Show("We could not automatically find your configuration. Please use the advanced dialog to enter your configuration manually.", "SPSync", MessageBoxButton.OK, MessageBoxImage.Error);

                DialogResult = false;
                Close();

                var d = new EditConfWindow();
                d.ShowDialog(_viewModel);
                return;
            }

            if (config.DocumentLibrary.Contains("|"))
            {
                _viewModel.DocumentLibrary = config.DocumentLibrary;
                var selectWindow = new SelectDocumentLibraryWindow();
                var selectResult = selectWindow.ShowDialog(_viewModel);
                if (!selectResult.HasValue || !selectResult.Value)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        buttonSave.IsEnabled = true;
                        buttonSave.Content = "Next";
                    }));
                    return;
                }
                config.DocumentLibrary = _viewModel.DocumentLibrary;
            }

            config.Name = _viewModel.Name;
            config.LocalFolder = _viewModel.LocalFolder;

            App.MainViewModel.AddOrUpdateConfiguration(config);

            DialogResult = true;
            Close();

            new NewConfigFolderSelect(config).ShowDialog();
        }

        private void buttonBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fd.Description = "Select the folder you want to sync with SharePoint";
                fd.ShowNewFolderButton = true;
                var result = fd.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _viewModel.LocalFolder = fd.SelectedPath;
                }
            }
        }

        private void TextBlockAdvancedConfig_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
            Close();

            var d = new EditConfWindow();
            d.ShowDialog(null);
        }
    }
}
