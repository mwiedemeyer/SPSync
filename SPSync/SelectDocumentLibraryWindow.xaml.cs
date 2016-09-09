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
    /// <summary>
    /// Interaction logic for SelectDocumentLibraryWindow.xaml
    /// </summary>
    public partial class SelectDocumentLibraryWindow : Window
    {
        private SyncViewModel viewModel;

        public List<string> DocLibs { get; private set; }

        public SelectDocumentLibraryWindow()
        {
            InitializeComponent();
        }

        internal bool? ShowDialog(SyncViewModel viewModel)
        {
            this.viewModel = viewModel;
            DataContext = this;
            DocLibs = new List<string>(viewModel.DocumentLibrary.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

            return ShowDialog();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            viewModel.DocumentLibrary = DocLibList.SelectedItem == null ? string.Empty : DocLibList.SelectedItem.ToString();

            DialogResult = true;
            Close();
        }
    }
}
