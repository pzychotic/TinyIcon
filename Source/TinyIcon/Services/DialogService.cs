using Microsoft.Win32;
using System.Windows;
using TinyIcon.Models;
using TinyIcon.ViewModels;
using TinyIcon.Views;

namespace TinyIcon.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/> using Win32 common dialogs.</summary>
public sealed class DialogService(Window owner, AppSettings settings) : IDialogService
{
    public IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog()
    {
        var viewModel = new NewIconViewModel(
            settings.Bpp24Sizes ?? IconResolutions.DefaultChecked,
            settings.Bpp32Sizes ?? IconResolutions.DefaultChecked);
        var dialog = new NewIconDialog { DataContext = viewModel, Owner = owner };
        if (dialog.ShowDialog() != true)
            return null;

        // Remember the confirmed selection as the default for the next New Icon dialog.
        settings.Bpp24Sizes = [.. viewModel.Bpp24.Where(o => o.IsSelected).Select(o => o.Size)];
        settings.Bpp32Sizes = [.. viewModel.Bpp32.Where(o => o.IsSelected).Select(o => o.Size)];
        return viewModel.BuildSpecs();
    }

    public string? OpenImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Image",
            Filter = "Image files|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff|All files|*.*",
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? SaveIconFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Icon",
            Filter = "Icon files|*.ico",
            DefaultExt = ".ico",
            AddExtension = true,
            FileName = "icon.ico",
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(owner, message, "TinyIcon", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowAbout()
    {
        var about = new AboutWindow { Owner = owner };
        about.ShowDialog();
    }
}
