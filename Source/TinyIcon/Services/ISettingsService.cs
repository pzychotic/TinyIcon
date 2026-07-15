using TinyIcon.Models;

namespace TinyIcon.Services;

public interface ISettingsService
{
    AppSettings? Load();
    void Save(AppSettings settings);
}
