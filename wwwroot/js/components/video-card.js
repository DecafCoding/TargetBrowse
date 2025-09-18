/**
 * SHARED VIDEO CARD JAVASCRIPT
 * Used by SuggestionCard, TopicVideoCard, and VideoCard
 * Handles YouTube thumbnail error fallbacks
 */

window.VideoCardHelpers = window.VideoCardHelpers || {};

/**
 * Handles thumbnail loading errors with progressive fallback
 * Tries different YouTube thumbnail qualities until one loads
 * @param {HTMLImageElement} img - The image element that failed to load
 * @param {string} videoId - The YouTube video ID
 */
window.VideoCardHelpers.handleThumbnailError = function(img, videoId) {
    // Prevent infinite error loops
    if (img.dataset.errorCount) {
        img.dataset.errorCount = parseInt(img.dataset.errorCount) + 1;
    } else {
        img.dataset.errorCount = "1";
    }

    // Stop after 4 attempts to prevent infinite loops
    if (parseInt(img.dataset.errorCount) > 4) {
        console.warn(`Failed to load thumbnail for video ${videoId} after 4 attempts`);
        // Set a placeholder or hide the image
        img.style.display = 'none';
        return;
    }

    // Progressive fallback through different thumbnail qualities
    if (img.dataset.attempt === 'maxres') {
        // Try high quality
        img.src = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
        img.dataset.attempt = 'hq';
    } else if (img.dataset.attempt === 'hq') {
        // Try medium quality
        img.src = `https://img.youtube.com/vi/${videoId}/mqdefault.jpg`;
        img.dataset.attempt = 'mq';
    } else if (img.dataset.attempt === 'mq') {
        // Try standard quality
        img.src = `https://img.youtube.com/vi/${videoId}/default.jpg`;
        img.dataset.attempt = 'default';
    } else if (img.dataset.attempt === 'default') {
        // Try SD default (older fallback)
        img.src = `https://img.youtube.com/vi/${videoId}/sddefault.jpg`;
        img.dataset.attempt = 'sd';
    } else if (!img.dataset.attempt) {
        // First error - try maximum resolution
        img.src = `https://img.youtube.com/vi/${videoId}/maxresdefault.jpg`;
        img.dataset.attempt = 'maxres';
    }
};

/**
 * Legacy function names for backward compatibility
 * These can be removed once all components are updated
 */
function handleThumbnailError(img, videoId) {
    window.VideoCardHelpers.handleThumbnailError(img, videoId);
}

function handleTopicThumbnailError(img, videoId) {
    window.VideoCardHelpers.handleThumbnailError(img, videoId);
}

/**
 * Initialize video card functionality when DOM is ready
 */
document.addEventListener('DOMContentLoaded', function() {
    // Add any global video card initialization here if needed
    
    // Optional: Preload common thumbnail sizes for better UX
    // This could be implemented if performance becomes an issue
});

/**
 * Utility function to format video duration
 * @param {number} seconds - Duration in seconds
 * @returns {string} Formatted duration (e.g., "10:30", "1:05:30")
 */
window.VideoCardHelpers.formatDuration = function(seconds) {
    if (!seconds || seconds <= 0) return '';
    
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    
    if (hours > 0) {
        return `${hours}:${minutes.toString().padStart(2, '0')}:${remainingSeconds.toString().padStart(2, '0')}`;
    } else {
        return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
    }
};

/**
 * Utility function to truncate text with ellipsis
 * @param {string} text - Text to truncate
 * @param {number} maxLength - Maximum length before truncation
 * @returns {string} Truncated text
 */
window.VideoCardHelpers.truncateText = function(text, maxLength) {
    if (!text || text.length <= maxLength) return text;
    return text.substring(0, maxLength).trim() + '...';
};