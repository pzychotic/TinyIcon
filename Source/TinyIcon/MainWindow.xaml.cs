using System.Windows;
using System.Windows.Input;
using TinyIcon.Services;
using TinyIcon.ViewModels;

namespace TinyIcon;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the view model (with a window-bound dialog service)
/// and registers keyboard shortcuts directly against the generated commands.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel(new DialogService(this));
        DataContext = viewModel;

        // InputBindings don't inherit DataContext, so bind to the command instances directly.
        InputBindings.Add(new KeyBinding(viewModel.NewIconCommand, Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ImportImageCommand, Key.I, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.SaveIconCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ZoomInCommand, Key.Add, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ZoomInCommand, Key.OemPlus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ZoomOutCommand, Key.Subtract, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ZoomOutCommand, Key.OemMinus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(viewModel.ZoomResetCommand, Key.Multiply, ModifierKeys.Control));
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
