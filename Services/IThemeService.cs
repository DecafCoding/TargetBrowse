namespace TargetBrowse.Services
{
    public interface IThemeService
    {
        Task<string> GetThemeAsync();
        Task SetThemeAsync(string theme);
        event Action<string>? ThemeChanged;
    }
}