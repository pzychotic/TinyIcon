using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyIcon.ViewModels;

/// <summary>A checkable resolution entry in the New Icon dialog.</summary>
public partial class ResolutionOptionViewModel : ObservableObject
{
    public ResolutionOptionViewModel(int size, bool isSelected)
    {
        Size = size;
        IsSelected = isSelected;
    }

    public int Size { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string Label => $"{Size}×{Size}";
}
