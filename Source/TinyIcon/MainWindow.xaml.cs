using System.Windows;
using TinyIcon.Services;
using TinyIcon.ViewModels;

namespace TinyIcon;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the view model with a window-bound dialog service;
/// keyboard shortcuts are declared as InputBindings in the XAML.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DialogService(this));
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
