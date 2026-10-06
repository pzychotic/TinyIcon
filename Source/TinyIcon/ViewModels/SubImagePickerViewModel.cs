using CommunityToolkit.Mvvm.ComponentModel;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>
/// Backs the sub-image picker dialog, used both by New Icon (choose every sub-image of a new icon) and by
/// Add Sub-Images (choose extra sub-images; the ones already in the icon are shown checked and locked).
/// </summary>
public partial class SubImagePickerViewModel : ObservableObject
{
    private SubImagePickerViewModel(string title, string description, IEnumerable<ColorDepthColumnViewModel> columns)
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
    }

    /// <summary>A New Icon picker with the default sizes checked and only the default depths enabled.</summary>
    public static SubImagePickerViewModel ForNewIcon() =>
        ForNewIcon(_ => IconResolutions.DefaultChecked, IconColorDepths.DefaultEnabled);

    /// <summary>A New Icon picker with the given checked sizes per depth and the given depths enabled.</summary>
    public static SubImagePickerViewModel ForNewIcon(
        Func<int, IReadOnlyCollection<int>> checkedSizes, IReadOnlyCollection<int> enabledDepths) =>
        new(
            "New Icon",
            "Choose the resolutions to include for each color depth. A size can be selected under several depths to produce one sub-image per depth.",
            IconColorDepths.All.Select(bpp =>
                new ColorDepthColumnViewModel(bpp, checkedSizes(bpp), enabledDepths.Contains(bpp))));

    /// <summary>
    /// An Add Sub-Images picker: the <paramref name="existing"/> entries are checked and locked, nothing else
    /// is checked, and the 32-bit column plus every depth the icon already uses are enabled.
    /// </summary>
    public static SubImagePickerViewModel ForAddSubImages(IReadOnlyCollection<(int Size, int Bpp)> existing) =>
        new(
            "Add Sub-Images",
            "Choose the resolutions to add for each color depth. Entries already in the icon are shown checked and cannot be changed.",
            IconColorDepths.All.Select(bpp =>
            {
                var existingSizes = existing.Where(e => e.Bpp == bpp).Select(e => e.Size).ToHashSet();
                bool enabled = IconColorDepths.DefaultEnabled.Contains(bpp) || existingSizes.Count > 0;
                return new ColorDepthColumnViewModel(bpp, [], enabled, existingSizes);
            }));

    public string Title { get; }

    public string Description { get; }

    public IReadOnlyList<ColorDepthColumnViewModel> Columns { get; }

    /// <summary>True when at least one new size is checked in an enabled column (enables the OK button).</summary>
    public bool HasSelection => Columns.Any(c => c.HasSelection);

    /// <summary>The column for <paramref name="bpp"/>.</summary>
    public ColorDepthColumnViewModel Column(int bpp) => Columns.Single(c => c.Bpp == bpp);

    /// <summary>The chosen new sub-image specs from the enabled columns, ordered by color depth then size.</summary>
    public IReadOnlyList<(int Size, int Bpp)> BuildSpecs() =>
        [.. Columns.SelectMany(c => c.NewSizes.Select(size => (size, c.Bpp)))];
}
