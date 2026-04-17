using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AIPCAssistant.ViewModels;

namespace AIPCAssistant
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}