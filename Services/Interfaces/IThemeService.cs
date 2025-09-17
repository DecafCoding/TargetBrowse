namespace TargetBrowse.Services.Interfaces
{
    public interface IThemeService
    {
        Task<string> GetThemeAsync();
        Task SetThemeAsync(string theme);
        event Action<string>? ThemeChanged;
    }
}