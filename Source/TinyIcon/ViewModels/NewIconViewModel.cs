using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>Backs the New Icon dialog: which (size, bpp) sub-images the icon will contain.</summary>
public partial class NewIconViewModel : ObservableObject
{
    public NewIconViewModel()
    {
        Bpp24 = CreateOptions();
        Bpp32 = CreateOptions();

        foreach (var option in Bpp24.Concat(Bpp32))
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasSelection));
    }

    public IReadOnlyList<ResolutionOptionViewModel> Bpp24 { get; }
    public IReadOnlyList<ResolutionOptionViewModel> Bpp32 { get; }

    /// <summary>True when at least one resolution is checked (enables the OK button).</summary>
    public bool HasSelection => Bpp24.Any(o => o.IsSelected) || Bpp32.Any(o => o.IsSelected);

    /// <summary>The chosen sub-image specs, ordered by colour depth then size.</summary>
    public IReadOnlyList<(int Size, int Bpp)> BuildSpecs() =>
    [
        .. Bpp24.Where(o => o.IsSelected).Select(o => (o.Size, 24)),
        .. Bpp32.Where(o => o.IsSelected).Select(o => (o.Size, 32)),
    ];

    private static List<ResolutionOptionViewModel> CreateOptions() =>
    [
        .. IconResolutions.Typical.Select(
            size => new ResolutionOptionViewModel(size, IconResolutions.DefaultChecked.Contains(size))),
    ];
}
