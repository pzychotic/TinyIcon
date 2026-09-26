using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyIcon.ViewModels;

/// <summary>A checkable resolution entry in the sub-image picker dialog.</summary>
public partial class ResolutionOptionViewModel(int size, bool isSelected, bool isExisting = false) : ObservableObject
{
    public int Size { get; } = size;

    /// <summary>
    /// True when the icon already contains this entry: it is shown checked but cannot be toggled,
    /// and is never part of the picker's result.
    /// </summary>
    public bool IsExisting { get; } = isExisting;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected || isExisting;

    public string Label => $"{Size}×{Size}";

    public string? ToolTip => IsExisting ? "Already in the icon" : null;
}
