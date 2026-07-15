using System.ComponentModel;
using System.Windows;
using TinyIcon.Models;
using TinyIcon.Services;
using TinyIcon.ViewModels;

namespace TinyIcon;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the view model with a window-bound dialog service;
/// keyboard shortcuts are declared as InputBindings in the XAML.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;

    public MainWindow(ISettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;

        InitializeComponent();
        DataContext = new MainViewModel(new WpfDialogService(this, settings));
        RestorePlacement(settings);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            SavePlacement(_settings);
            _settingsService.Save(_settings);
        }
    }

    private void RestorePlacement(AppSettings settings)
    {
        if (settings.WindowLeft is not double left || settings.WindowTop is not double top ||
            settings.WindowWidth is not double width || settings.WindowHeight is not double height)
            return;

        // Ignore stale bounds that no longer touch any monitor (e.g. a display was unplugged).
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!virtualScreen.IntersectsWith(new Rect(left, top, width, height)))
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SavePlacement(AppSettings settings)
    {
        // When maximized or minimized, RestoreBounds holds the normal-state geometry.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        settings.WindowLeft = bounds.Left;
        settings.WindowTop = bounds.Top;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
    }
}
