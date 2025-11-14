/**
 * Handles channel thumbnail loading errors by replacing failed images with a placeholder.
 * This prevents broken image icons when YouTube CDN thumbnails are unavailable.
 *
 * @param {HTMLImageElement} img - The image element that failed to load
 */
function handleChannelThumbnailError(img) {
    // Prevent infinite loop if placeholder also fails
    if (img.src.includes('channel-placeholder.svg')) {
        return;
    }

    // Set the onerror to null to prevent multiple calls
    img.onerror = null;

    // Replace with local placeholder
    img.src = '/images/channel-placeholder.svg';

    // Add a class to indicate this is a placeholder (useful for styling)
    img.classList.add('thumbnail-placeholder');
}
