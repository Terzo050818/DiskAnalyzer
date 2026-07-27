using System.Windows;
using DiskAnalyzer.App.ViewModels;

namespace DiskAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
