using Microsoft.Win32;
using System.Windows;
using TinyIcon.Models;
using TinyIcon.ViewModels;
using TinyIcon.Views;

namespace TinyIcon.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/> using Win32 common dialogs.</summary>
public sealed class WpfDialogService(Window owner, AppSettings settings) : IDialogService
{
    public IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(RememberedSizes, RememberedEnabledDepths());
        if (!ShowPicker(viewModel))
            return null;

        // Remember the confirmed selection as the default for the next New Icon dialog.
        settings.DepthSizes = viewModel.Columns.ToDictionary(c => c.Bpp, c => c.CheckedSizes);
        settings.EnabledDepths = [.. viewModel.Columns.Where(c => c.IsEnabled).Select(c => c.Bpp)];
        settings.Bpp24Sizes = settings.Bpp32Sizes = null;
        settings.Bpp24Enabled = settings.Bpp32Enabled = null;
        return viewModel.BuildSpecs();
    }

    public IReadOnlyList<(int Size, int Bpp)>? ShowAddSubImagesDialog(IReadOnlyCollection<(int Size, int Bpp)> existing)
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages(existing);
        return ShowPicker(viewModel) ? viewModel.BuildSpecs() : null;
    }

    private bool ShowPicker(SubImagePickerViewModel viewModel) =>
        new SubImagePickerDialog { DataContext = viewModel, Owner = owner }.ShowDialog() == true;

    private IReadOnlyCollection<int> RememberedSizes(int bpp)
    {
        if (settings.DepthSizes is { } sizes)
            return sizes.GetValueOrDefault(bpp) ?? IconResolutions.DefaultChecked;

        var legacy = bpp switch { 24 => settings.Bpp24Sizes, 32 => settings.Bpp32Sizes, _ => null };
        return legacy ?? IconResolutions.DefaultChecked;
    }

    private int[] RememberedEnabledDepths()
    {
        if (settings.EnabledDepths is { } depths)
            return depths;

        // Legacy defaults were 24-bit off, 32-bit on — the same as IconColorDepths.DefaultEnabled.
        var legacy = new List<int>();
        if (settings.Bpp24Enabled ?? false)
            legacy.Add(24);
        if (settings.Bpp32Enabled ?? true)
            legacy.Add(32);
        return [.. legacy];
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

    public string? OpenIconFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Icon",
            Filter = "Icon files|*.ico|All files|*.*",
            DefaultExt = ".ico",
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
