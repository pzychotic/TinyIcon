using CommunityToolkit.Mvvm.ComponentModel;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>Backs the New Icon dialog: which (size, bpp) sub-images the icon will contain.</summary>
public partial class NewIconViewModel : ObservableObject
{
    public NewIconViewModel()
        : this(IconResolutions.DefaultChecked, IconResolutions.DefaultChecked)
    {
    }

    public NewIconViewModel(
        IReadOnlyCollection<int> checked24, IReadOnlyCollection<int> checked32,
        bool enabled24 = false, bool enabled32 = true)
    {
        Bpp24 = CreateOptions(checked24);
        Bpp32 = CreateOptions(checked32);
        _bpp24Enabled = enabled24;
        _bpp32Enabled = enabled32;

        foreach (var option in Bpp24.Concat(Bpp32))
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasSelection));
    }

    public IReadOnlyList<ResolutionOptionViewModel> Bpp24 { get; }
    public IReadOnlyList<ResolutionOptionViewModel> Bpp32 { get; }

    /// <summary>Whether the 24-bpp column contributes to the icon. Checked sizes are kept while disabled.</summary>
    [ObservableProperty]
    private bool _bpp24Enabled;

    /// <summary>Whether the 32-bpp column contributes to the icon. Checked sizes are kept while disabled.</summary>
    [ObservableProperty]
    private bool _bpp32Enabled;

    /// <summary>True when at least one resolution is checked in an enabled column (enables the OK button).</summary>
    public bool HasSelection =>
        (Bpp24Enabled && Bpp24.Any(o => o.IsSelected)) || (Bpp32Enabled && Bpp32.Any(o => o.IsSelected));

    /// <summary>The chosen sub-image specs from the enabled columns, ordered by colour depth then size.</summary>
    public IReadOnlyList<(int Size, int Bpp)> BuildSpecs() =>
    [
        .. Bpp24.Where(o => Bpp24Enabled && o.IsSelected).Select(o => (o.Size, 24)),
        .. Bpp32.Where(o => Bpp32Enabled && o.IsSelected).Select(o => (o.Size, 32)),
    ];

    partial void OnBpp24EnabledChanged(bool value) => OnPropertyChanged(nameof(HasSelection));

    partial void OnBpp32EnabledChanged(bool value) => OnPropertyChanged(nameof(HasSelection));

    private static List<ResolutionOptionViewModel> CreateOptions(IReadOnlyCollection<int> checkedSizes) =>
    [
        .. IconResolutions.Typical.Select(
            size => new ResolutionOptionViewModel(size, checkedSizes.Contains(size))),
    ];
}
