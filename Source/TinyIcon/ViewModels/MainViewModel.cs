using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
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
    [NotifyCanExecuteChangedFor(nameof(DeleteSubImageCommand))]
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
        AddSubImagesCommand.NotifyCanExecuteChanged();
        SaveIconCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void OpenIcon()
    {
        var path = _dialogs.OpenIconFile();
        if (path is null)
            return;

        LoadIcon(path);
    }

    /// <summary>Handles files dropped on the window: an .ico replaces the icon, anything else is imported.</summary>
    [RelayCommand]
    private void Drop(string[]? files)
    {
        // Only the first file is used; an icon is a single document.
        var path = files?.FirstOrDefault();
        if (path is null)
            return;

        if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            LoadIcon(path);
            return;
        }

        if (!HasSlots)
        {
            _dialogs.ShowError("Create a new icon or open an existing one before importing an image.");
            return;
        }

        ImportImageFrom(path);
    }

    [RelayCommand(CanExecute = nameof(HasSlots))]
    private void ImportImage()
    {
        var path = _dialogs.OpenImageFile();
        if (path is null)
            return;

        ImportImageFrom(path);
    }

    private void LoadIcon(string path)
    {
        IReadOnlyList<IconImage> images;
        try
        {
            // Read everything first so a failure never leaves a half-replaced icon behind.
            images = IconFileReader.Read(path);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not open icon:\n{ex.Message}");
            return;
        }

        SubImages.Clear();
        foreach (var image in images)
        {
            SubImages.Add(new SubImageViewModel(image.Bitmap.PixelWidth, image.Bitmap.PixelHeight, image.Bpp)
            {
                Bitmap = image.Bitmap,
                Format = image.Format,
            });
        }

        SelectedSubImage = SubImages.FirstOrDefault();
        ImportImageCommand.NotifyCanExecuteChanged();
        AddSubImagesCommand.NotifyCanExecuteChanged();
        SaveIconCommand.NotifyCanExecuteChanged();
    }

    private void ImportImageFrom(string path)
    {
        try
        {
            // Scale everything first so a failure never leaves the slots half old, half new.
            var source = ImageScaler.Load(path);
            var scaled = SubImages
                .Select(slot => ImageScaler.ScaleToBox(source, slot.Width, slot.Height, slot.Bpp))
                .ToList();
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
                .Select(s => new IconImage(s.Bitmap!, s.Bpp, s.Format));
            IconFileWriter.Write(path, images);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not save icon:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Adds the sub-images chosen in the Add Sub-Images dialog. Each new slot is scaled from the best existing
    /// bitmap (see <see cref="BestSourceBitmap"/>), or left empty when there is none yet, and inserted in
    /// color-depth-then-size order. The first new slot is selected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSlots))]
    private void AddSubImages()
    {
        var existing = SubImages.Where(s => s.Width == s.Height).Select(s => (s.Width, s.Bpp)).ToList();
        var specs = _dialogs.ShowAddSubImagesDialog(existing);
        if (specs is null || specs.Count == 0)
            return;

        List<SubImageViewModel> added;
        try
        {
            // Scale everything first so a failure never leaves only some of the new slots behind.
            var source = BestSourceBitmap();
            added =
            [
                .. specs.Select(spec => new SubImageViewModel(spec.Size, spec.Size, spec.Bpp)
                {
                    Bitmap = source is null ? null : ImageScaler.ScaleToBox(source, spec.Size, spec.Size, spec.Bpp),
                }),
            ];
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Could not add sub-images:\n{ex.Message}");
            return;
        }

        foreach (var slot in added)
            SubImages.Insert(InsertionIndex(slot), slot);

        SelectedSubImage = added[0];
        SaveIconCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The largest existing bitmap, preferring the higher color depth when sizes tie.</summary>
    private BitmapSource? BestSourceBitmap() =>
        SubImages
            .Where(s => s.Bitmap is not null)
            .OrderByDescending(s => s.Width * s.Height)
            .ThenByDescending(s => s.Bpp)
            .FirstOrDefault()?.Bitmap;

    /// <summary>Before the first slot that sorts after <paramref name="slot"/> by color depth, then size.</summary>
    private int InsertionIndex(SubImageViewModel slot)
    {
        for (int i = 0; i < SubImages.Count; i++)
        {
            var other = SubImages[i];
            if ((other.Bpp, other.Width, other.Height).CompareTo((slot.Bpp, slot.Width, slot.Height)) > 0)
                return i;
        }

        return SubImages.Count;
    }

    private bool CanDeleteSubImage(SubImageViewModel? item) => (item ?? SelectedSubImage) is not null;

    /// <summary>Removes <paramref name="item"/> (the right-clicked preview), or the selected sub-image when null.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSubImage))]
    private void DeleteSubImage(SubImageViewModel? item)
    {
        var target = item ?? SelectedSubImage;
        if (target is null)
            return;

        var index = SubImages.IndexOf(target);
        if (index < 0)
            return;

        SubImages.RemoveAt(index);

        // Keep the selection near the removed entry so repeated deletes walk down the list.
        if (SelectedSubImage == target || SelectedSubImage is null)
            SelectedSubImage = SubImages.Count > 0 ? SubImages[Math.Min(index, SubImages.Count - 1)] : null;

        ImportImageCommand.NotifyCanExecuteChanged();
        AddSubImagesCommand.NotifyCanExecuteChanged();
        SaveIconCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ZoomIn() => ZoomLevel = Math.Clamp(ZoomLevel + ZoomDefaults.Step, ZoomDefaults.Min, ZoomDefaults.Max);

    [RelayCommand]
    private void ZoomOut() => ZoomLevel = Math.Clamp(ZoomLevel - ZoomDefaults.Step, ZoomDefaults.Min, ZoomDefaults.Max);

    [RelayCommand]
    private void ZoomReset() => ZoomLevel = 1.0;

    [RelayCommand]
    private void About() => _dialogs.ShowAbout();
}
