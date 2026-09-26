using TinyIcon.Services;

namespace TinyIcon.Tests.TestSupport;

/// <summary>Scriptable <see cref="IDialogService"/> for driving <c>MainViewModel</c> without a UI.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public IReadOnlyList<(int Size, int Bpp)>? NewIconResult { get; set; }
    public string? OpenImageResult { get; set; }
    public string? OpenIconResult { get; set; }
    public string? SaveIconResult { get; set; }

    public int NewIconCalls { get; private set; }
    public int OpenImageCalls { get; private set; }
    public int OpenIconCalls { get; private set; }
    public int SaveIconCalls { get; private set; }
    public List<string> Errors { get; } = [];

    public IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog()
    {
        NewIconCalls++;
        return NewIconResult;
    }

    public IReadOnlyList<(int Size, int Bpp)>? AddSubImagesResult { get; set; }

    /// <summary>The existing entries passed to the last <see cref="ShowAddSubImagesDialog"/> call.</summary>
    public IReadOnlyCollection<(int Size, int Bpp)>? LastExisting { get; private set; }

    public int AddSubImagesCalls { get; private set; }

    public IReadOnlyList<(int Size, int Bpp)>? ShowAddSubImagesDialog(IReadOnlyCollection<(int Size, int Bpp)> existing)
    {
        AddSubImagesCalls++;
        LastExisting = existing;
        return AddSubImagesResult;
    }

    public string? OpenImageFile()
    {
        OpenImageCalls++;
        return OpenImageResult;
    }

    public string? OpenIconFile()
    {
        OpenIconCalls++;
        return OpenIconResult;
    }

    public string? SaveIconFile()
    {
        SaveIconCalls++;
        return SaveIconResult;
    }

    public void ShowError(string message) => Errors.Add(message);

    public void ShowAbout() { }
}
