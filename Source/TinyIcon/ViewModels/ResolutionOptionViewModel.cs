using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyIcon.ViewModels;

/// <summary>A checkable resolution entry in the New Icon dialog.</summary>
public partial class ResolutionOptionViewModel(int size, bool isSelected) : ObservableObject
{
    public int Size { get; } = size;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    public string Label => $"{Size}×{Size}";
}
