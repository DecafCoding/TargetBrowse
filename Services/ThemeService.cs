using Microsoft.JSInterop;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services
{
    /// <summary>
    /// Service for managing application theme (light/dark mode).
    /// Uses localStorage for persistence and Bootstrap's data-bs-theme attribute.
    /// Handles pre-rendering by checking JavaScript availability.
    /// </summary>
    public class ThemeService : IThemeService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
        private string _currentTheme = "auto"; // Cache for pre-rendering

        public event Action<string>? ThemeChanged;

        public ThemeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            _moduleTask = new(() => _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "/js/theme.js").AsTask());
        }

        /// <summary>
        /// Gets the current theme from localStorage or defaults to 'auto'.
        /// Returns cached value during pre-rendering.
        /// </summary>
        public async Task<string> GetThemeAsync()
        {
            // Check if JavaScript is available (not during pre-rendering)
            if (!IsJavaScriptAvailable())
            {
                return _currentTheme;
            }

            try
            {
                var module = await _moduleTask.Value;
                var theme = await module.InvokeAsync<string>("getTheme");
                _currentTheme = theme; // Update cache
                return theme;
            }
            catch
            {
                // Fallback to cached value if JS fails
                return _currentTheme;
            }
        }

        /// <summary>
        /// Sets the theme and persists it to localStorage.
        /// Valid themes: 'light', 'dark', 'auto'
        /// Updates cache immediately for pre-rendering scenarios.
        /// </summary>
        public async Task SetThemeAsync(string theme)
        {
            // Validate and cache theme immediately
            if (theme != "light" && theme != "dark" && theme != "auto")
                theme = "auto";

            _currentTheme = theme;

            // Skip JavaScript during pre-rendering
            if (!IsJavaScriptAvailable())
            {
                ThemeChanged?.Invoke(theme);
                return;
            }

            try
            {
                var module = await _moduleTask.Value;
                await module.InvokeVoidAsync("setTheme", theme);
                ThemeChanged?.Invoke(theme);
            }
            catch
            {
                // Still notify of change even if persistence fails
                ThemeChanged?.Invoke(theme);
            }
        }

        /// <summary>
        /// Checks if JavaScript interop is available (not during pre-rendering).
        /// </summary>
        private bool IsJavaScriptAvailable()
        {
            try
            {
                // More reliable check for JavaScript availability
                return _jsRuntime is IJSInProcessRuntime ||
                       !_jsRuntime.GetType().Name.Contains("Unsupported");
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_moduleTask.IsValueCreated)
            {
                try
                {
                    var module = await _moduleTask.Value;
                    await module.DisposeAsync();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}