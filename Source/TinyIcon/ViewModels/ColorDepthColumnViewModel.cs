using CommunityToolkit.Mvvm.ComponentModel;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>One colour-depth column of the sub-image picker: a checkable size per typical resolution.</summary>
public partial class ColorDepthColumnViewModel : ObservableObject
{
    public ColorDepthColumnViewModel(
        int bpp, IReadOnlyCollection<int> checkedSizes, bool isEnabled, IReadOnlyCollection<int>? existingSizes = null)
    {
        Bpp = bpp;
        Options =
        [
            .. IconResolutions.Typical.Select(size => new ResolutionOptionViewModel(
                size, checkedSizes.Contains(size), existingSizes?.Contains(size) ?? false)),
        ];
        IsEnabled = isEnabled;

        foreach (var option in Options)
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasSelection));
    }

    public int Bpp { get; }

    public string Header => $"{Bpp}-bit";

    public IReadOnlyList<ResolutionOptionViewModel> Options { get; }

    /// <summary>Whether this column contributes to the result. Checked sizes are kept while disabled.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial bool IsEnabled { get; set; }

    /// <summary>The sizes this column adds: checked, not already in the icon, and only while enabled.</summary>
    public IEnumerable<int> NewSizes =>
        IsEnabled ? Options.Where(o => o.IsSelected && !o.IsExisting).Select(o => o.Size) : [];

    /// <summary>True when this column adds at least one size.</summary>
    public bool HasSelection => NewSizes.Any();

    /// <summary>Every checked size (existing ones excluded), regardless of <see cref="IsEnabled"/>.</summary>
    public int[] CheckedSizes => [.. Options.Where(o => o.IsSelected && !o.IsExisting).Select(o => o.Size)];
}
