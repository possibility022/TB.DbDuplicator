using DatabaseCopier.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DatabaseCopier
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        private MainWindowViewModel ViewModel { get => _viewModel ?? (_viewModel = (MainWindowViewModel)DataContext); }

        public MainWindow()
        {
            InitializeComponent();
            var ViewModel = new MainWindowViewModel();
            DataContext = ViewModel;
        }

        private void MoveToToCopy_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoveToToCopyList();
        }

        private void MoveToIgnore_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoveToIgnore();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Start();
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Load();
        }
    }
}
