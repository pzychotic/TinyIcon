using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TinyIcon.Services;

namespace TinyIcon.ViewModels;

/// <summary>Root view model: owns the sub-image list, the selection, the zoom level and the file commands.</summary>
public partial class MainViewModel : ObservableObject
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 16.0;
    private const double ZoomStep = 1.25;

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
            var source = ImageScaler.Load(path);
            foreach (var slot in SubImages)
                slot.Bitmap = ImageScaler.ScaleTo(source, slot.Width);
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
            IconFileWriter.Write(path, SubImages);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not save icon:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void ZoomIn() => ZoomLevel = Math.Clamp(ZoomLevel * ZoomStep, MinZoom, MaxZoom);

    [RelayCommand]
    private void ZoomOut() => ZoomLevel = Math.Clamp(ZoomLevel / ZoomStep, MinZoom, MaxZoom);

    [RelayCommand]
    private void ZoomReset() => ZoomLevel = 1.0;
}
