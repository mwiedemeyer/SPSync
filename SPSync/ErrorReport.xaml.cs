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
    /// <summary>
    /// Interaction logic for ErrorReport.xaml
    /// </summary>
    public partial class ErrorReport : Window
    {
        private SyncViewModel _viewModel;

        public ErrorReport()
        {
            InitializeComponent();
        }

        internal bool? ShowDialog(SyncViewModel conf)
        {
            _viewModel = conf;

            DataContext = _viewModel;

            return ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var id = (Guid)((Button)sender).Tag;
            _viewModel.ResetErrorFlag(id);            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _viewModel.ResetErrorFlag();
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            DataContext = _viewModel;
        }
    }
}
