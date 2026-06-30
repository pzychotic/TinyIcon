using System.Windows;
using Microsoft.Win32;
using TinyIcon.ViewModels;
using TinyIcon.Views;

namespace TinyIcon.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/> using Win32 common dialogs.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog()
    {
        var viewModel = new NewIconViewModel();
        var dialog = new NewIconDialog { DataContext = viewModel, Owner = owner };
        return dialog.ShowDialog() == true ? viewModel.BuildSpecs() : null;
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
}
