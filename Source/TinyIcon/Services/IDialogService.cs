namespace TinyIcon.Services;

/// <summary>Abstracts file/dialog interactions so the view models stay free of view types.</summary>
public interface IDialogService
{
    /// <summary>Shows the New Icon dialog; returns the chosen (size, bpp) specs, or null if canceled.</summary>
    IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog();

    /// <summary>
    /// Shows the Add Sub-Images dialog with the <paramref name="existing"/> entries locked; returns the chosen
    /// new (size, bpp) specs, or null if canceled.
    /// </summary>
    IReadOnlyList<(int Size, int Bpp)>? ShowAddSubImagesDialog(IReadOnlyCollection<(int Size, int Bpp)> existing);

    /// <summary>Shows an open-file dialog for images; returns the selected path, or null if canceled.</summary>
    string? OpenImageFile();

    /// <summary>Shows an open-file dialog for an .ico file; returns the selected path, or null if canceled.</summary>
    string? OpenIconFile();

    /// <summary>Shows a save-file dialog for an .ico file; returns the selected path, or null if canceled.</summary>
    string? SaveIconFile();

    /// <summary>Shows an error message to the user.</summary>
    void ShowError(string message);

    /// <summary>Shows the About dialog.</summary>
    void ShowAbout();
}
