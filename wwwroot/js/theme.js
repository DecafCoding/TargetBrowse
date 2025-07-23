// wwwroot/js/theme.js

/**
 * Theme management for Bootstrap dark mode support.
 * Handles theme persistence and applies Bootstrap's data-bs-theme attribute.
 */

const THEME_KEY = 'targetbrowse-theme';
const THEMES = {
    LIGHT: 'light',
    DARK: 'dark',
    AUTO: 'auto'
};

/**
 * Gets the current theme from localStorage or defaults to 'auto'
 */
export function getTheme() {
    try {
        return localStorage.getItem(THEME_KEY) || THEMES.AUTO;
    } catch {
        return THEMES.AUTO;
    }
}

/**
 * Sets the theme and applies it to the document
 */
export function setTheme(theme) {
    try {
        // Validate theme value
        if (!Object.values(THEMES).includes(theme)) {
            theme = THEMES.AUTO;
        }

        // Save to localStorage
        localStorage.setItem(THEME_KEY, theme);

        // Apply theme to document
        applyTheme(theme);
    } catch (error) {
        console.warn('Failed to set theme:', error);
    }
}

/**
 * Applies the theme to the document by setting data-bs-theme attribute
 */
function applyTheme(theme) {
    const htmlElement = document.documentElement;

    if (theme === THEMES.AUTO) {
        // Remove the attribute to let Bootstrap use system preference
        htmlElement.removeAttribute('data-bs-theme');
    } else {
        // Set explicit theme
        htmlElement.setAttribute('data-bs-theme', theme);
    }
}

/**
 * Gets the display name for a theme
 */
export function getThemeDisplayName(theme) {
    switch (theme) {
        case THEMES.LIGHT:
            return 'Light';
        case THEMES.DARK:
            return 'Dark';
        case THEMES.AUTO:
            return 'Auto';
        default:
            return 'Auto';
    }
}

/**
 * Gets the icon class for a theme
 */
export function getThemeIcon(theme) {
    switch (theme) {
        case THEMES.LIGHT:
            return 'bi-sun-fill';
        case THEMES.DARK:
            return 'bi-moon-fill';
        case THEMES.AUTO:
            return 'bi-circle-half';
        default:
            return 'bi-circle-half';
    }
}

/**
 * Initialize theme on page load
 */
export function initializeTheme() {
    const savedTheme = getTheme();
    applyTheme(savedTheme);
    return savedTheme;
}

// Auto-initialize when module loads
if (typeof window !== 'undefined') {
    // Initialize immediately if DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeTheme);
    } else {
        initializeTheme();
    }
}