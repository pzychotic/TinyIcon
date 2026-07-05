using System.Globalization;
using System.Windows;
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

        SettingsService.Load();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.Save();

        base.OnExit(e);
    }
}
