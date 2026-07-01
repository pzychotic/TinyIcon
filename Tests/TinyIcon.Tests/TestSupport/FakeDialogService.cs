using TinyIcon.Services;

namespace TinyIcon.Tests.TestSupport;

/// <summary>Scriptable <see cref="IDialogService"/> for driving <c>MainViewModel</c> without a UI.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public IReadOnlyList<(int Size, int Bpp)>? NewIconResult { get; set; }
    public string? OpenImageResult { get; set; }
    public string? SaveIconResult { get; set; }

    public int NewIconCalls { get; private set; }
    public int OpenImageCalls { get; private set; }
    public int SaveIconCalls { get; private set; }
    public List<string> Errors { get; } = [];

    public IReadOnlyList<(int Size, int Bpp)>? ShowNewIconDialog()
    {
        NewIconCalls++;
        return NewIconResult;
    }

    public string? OpenImageFile()
    {
        OpenImageCalls++;
        return OpenImageResult;
    }

    public string? SaveIconFile()
    {
        SaveIconCalls++;
        return SaveIconResult;
    }

    public void ShowError(string message) => Errors.Add(message);
}
