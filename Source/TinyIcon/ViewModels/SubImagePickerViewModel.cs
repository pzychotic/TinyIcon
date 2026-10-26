using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>
/// Backs the sub-image picker dialog, used both by New Icon (choose every sub-image of a new icon) and by
/// Add Sub-Images (choose extra sub-images; the ones already in the icon are shown checked and locked).
/// The legacy depths can be collapsed; while hidden they contribute nothing to the result.
/// </summary>
public partial class SubImagePickerViewModel : ObservableObject
{
    private SubImagePickerViewModel(
        string title, string description, IEnumerable<ColorDepthColumnViewModel> columns, bool showLegacy)
    {
        Title = title;
        Description = description;
        Columns = [.. columns];

        foreach (var column in Columns)
        {
            column.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ColorDepthColumnViewModel.HasSelection))
                    OnPropertyChanged(nameof(HasSelection));
            };
        }

        ShowLegacy = showLegacy;
        UpdateLegacyVisibility();
    }

    /// <summary>A New Icon picker with the default sizes checked, only the default depths enabled and legacy depths hidden.</summary>
    public static SubImagePickerViewModel ForNewIcon() =>
        ForNewIcon(_ => IconResolutions.DefaultChecked, IconColorDepths.DefaultEnabled);

    /// <summary>
    /// A New Icon picker with the given checked sizes per depth and the given depths enabled. Legacy depths
    /// are shown when <paramref name="showLegacy"/> is set or when any of them is enabled.
    /// </summary>
    public static SubImagePickerViewModel ForNewIcon(
        Func<int, IReadOnlyCollection<int>> checkedSizes, IReadOnlyCollection<int> enabledDepths,
        bool showLegacy = false) =>
        new(
            "New Icon",
            "Choose the sizes to include for each color depth.",
            IconColorDepths.All.Select(bpp =>
                new ColorDepthColumnViewModel(bpp, checkedSizes(bpp), enabledDepths.Contains(bpp))),
            showLegacy || enabledDepths.Any(IconColorDepths.IsLegacy));

    /// <summary>
    /// An Add Sub-Images picker: the <paramref name="existing"/> entries are checked and locked, nothing else
    /// is checked, and the 32-bit column plus every depth the icon already uses are enabled. Legacy depths
    /// are shown when <paramref name="showLegacy"/> is set or when the icon already uses any of them.
    /// </summary>
    public static SubImagePickerViewModel ForAddSubImages(
        IReadOnlyCollection<(int Size, int Bpp)> existing, bool showLegacy = false) =>
        new(
            "Add Sub-Images",
            "Choose the sizes to add. Existing entries are locked.",
            IconColorDepths.All.Select(bpp =>
            {
                var existingSizes = existing.Where(e => e.Bpp == bpp).Select(e => e.Size).ToHashSet();
                bool enabled = IconColorDepths.DefaultEnabled.Contains(bpp) || existingSizes.Count > 0;
                return new ColorDepthColumnViewModel(bpp, [], enabled, existingSizes);
            }),
            showLegacy || existing.Any(e => IconColorDepths.IsLegacy(e.Bpp)));

    public string Title { get; }

    public string Description { get; }

    public IReadOnlyList<ColorDepthColumnViewModel> Columns { get; }

    /// <summary>Whether the legacy depth columns are shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegacyToggleText), nameof(LegacyToggleToolTip))]
    public partial bool ShowLegacy { get; set; }

    /// <summary>Arrow on the button that shows or hides the legacy depths (they open to the left).</summary>
    public string LegacyToggleText => ShowLegacy ? ">" : "<";

    /// <summary>Tooltip of the button that shows or hides the legacy depths.</summary>
    public string LegacyToggleToolTip => ShowLegacy ? "Hide legacy color depths" : "Show legacy color depths";

    /// <summary>True when at least one new size is checked in a visible, enabled column (enables the OK button).</summary>
    public bool HasSelection => VisibleColumns.Any(c => c.HasSelection);

    /// <summary>The column for <paramref name="bpp"/>.</summary>
    public ColorDepthColumnViewModel Column(int bpp) => Columns.Single(c => c.Bpp == bpp);

    /// <summary>The chosen new sub-image specs from the visible, enabled columns, ordered by color depth then size.</summary>
    public IReadOnlyList<(int Size, int Bpp)> BuildSpecs() =>
        [.. VisibleColumns.SelectMany(c => c.NewSizes.Select(size => (size, c.Bpp)))];

    private IEnumerable<ColorDepthColumnViewModel> VisibleColumns => Columns.Where(c => c.IsVisible);

    [RelayCommand]
    private void ToggleLegacy() => ShowLegacy = !ShowLegacy;

    partial void OnShowLegacyChanged(bool value)
    {
        UpdateLegacyVisibility();
        OnPropertyChanged(nameof(HasSelection));
    }

    private void UpdateLegacyVisibility()
    {
        foreach (var column in Columns.Where(c => c.IsLegacy))
            column.IsVisible = ShowLegacy;
    }
}
