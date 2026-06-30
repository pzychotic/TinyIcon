namespace TinyIcon.Services;

/// <summary>Abstracts file/dialog interactions so the view models stay free of view types.</summary>
public interface IDialogService
{
    /// <summary>Shows the New Icon dialog; returns the chosen (size, bpp) specs, or null if cancelled.</summary>
    IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog();

    /// <summary>Shows an open-file dialog for images; returns the selected path, or null if cancelled.</summary>
    string? OpenImageFile();

    /// <summary>Shows a save-file dialog for an .ico file; returns the selected path, or null if cancelled.</summary>
    string? SaveIconFile();

    /// <summary>Shows an error message to the user.</summary>
    void ShowError(string message);
}
