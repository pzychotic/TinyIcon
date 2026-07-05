using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TinyIcon.Models;
using TinyIcon.Services;

namespace TinyIcon.ViewModels;

/// <summary>Root view model: owns the sub-image list, the selection, the zoom level and the file commands.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    public MainViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        ZoomLevel = 1.0;
    }

    public ObservableCollection<SubImageViewModel> SubImages { get; } = [];

    [ObservableProperty]
    public partial SubImageViewModel? SelectedSubImage { get; set; }

    [ObservableProperty]
    public partial double ZoomLevel { get; set; }

    private bool HasSlots => SubImages.Count > 0;

    private bool CanSave => SubImages.Any(s => s.HasImage);

    [RelayCommand]
    private void NewIcon()
    {
        var specs = _dialogs.ShowNewIconDialog();
        if (specs is null || specs.Count == 0)
            return;

        SubImages.Clear();
        foreach (var (size, bpp) in specs)
            SubImages.Add(new SubImageViewModel(size, size, bpp));

        SelectedSubImage = SubImages.FirstOrDefault();
        ImportImageCommand.NotifyCanExecuteChanged();
        SaveIconCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSlots))]
    private void ImportImage()
    {
        var path = _dialogs.OpenImageFile();
        if (path is null)
            return;

        try
        {
            // Scale everything first so a failure never leaves the slots half old, half new.
            var source = ImageScaler.Load(path);
            var scaled = SubImages.Select(slot => ImageScaler.ScaleTo(source, slot.Width)).ToList();
            foreach (var (slot, bitmap) in SubImages.Zip(scaled))
                slot.Bitmap = bitmap;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not import image:\n{ex.Message}");
            return;
        }

        SaveIconCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void SaveIcon()
    {
        var path = _dialogs.SaveIconFile();
        if (path is null)
            return;

        try
        {
            var images = SubImages
                .Where(s => s.Bitmap is not null)
                .Select(s => new IconImage(s.Bitmap!, s.Bpp));
            IconFileWriter.Write(path, images);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not save icon:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void ZoomIn() => ZoomLevel = Math.Clamp(ZoomLevel * ZoomDefaults.Step, ZoomDefaults.Min, ZoomDefaults.Max);

    [RelayCommand]
    private void ZoomOut() => ZoomLevel = Math.Clamp(ZoomLevel / ZoomDefaults.Step, ZoomDefaults.Min, ZoomDefaults.Max);

    [RelayCommand]
    private void ZoomReset() => ZoomLevel = 1.0;

    [RelayCommand]
    private void About() => _dialogs.ShowAbout();
}
