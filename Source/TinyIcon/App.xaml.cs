using System.Globalization;
using System.Windows;
using TinyIcon.Models;
using TinyIcon.Services;

namespace TinyIcon;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var invariant = CultureInfo.InvariantCulture;

        // Ensure the current thread uses invariant culture
        Thread.CurrentThread.CurrentCulture = invariant;
        Thread.CurrentThread.CurrentUICulture = invariant;

        // Ensure future threads default to invariant culture
        CultureInfo.DefaultThreadCurrentCulture = invariant;
        CultureInfo.DefaultThreadCurrentUICulture = invariant;

        base.OnStartup(e);

        // composition root: construct services and inject them into the main window
        var settingsService = new JsonSettingsService();
        var settings = settingsService.Load() ?? new AppSettings();

        var window = new MainWindow(settingsService, settings);
        window.Show();
    }
}
