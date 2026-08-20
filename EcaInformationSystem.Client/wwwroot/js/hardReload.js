// Makes the in-app "Refresh Now" buttons (SystemUpdateBanner, UnifiedNotificationBell)
// behave like Ctrl+Shift+R instead of a plain location.reload(), which can still
// serve a stale blazor.boot.json / cached .dll & .wasm files from disk cache.
window.hardReload = function () {
    // Flag read once by the boot loader in index.html — tells it to fetch every
    // framework resource with { cache: 'reload' } (bypass disk cache) for this
    // one load only.
    sessionStorage.setItem('ecaForceHardReload', '1');

    // Defensive: this app doesn't register a service worker today, but if one
    // is ever added, its Cache Storage entries would otherwise survive this.
    if (window.caches && caches.keys) {
        caches.keys().then(function (names) {
            names.forEach(function (name) { caches.delete(name); });
        });
    }

    window.location.reload();
};

// Plain reload used by the "Refresh Now" prompts (SystemUpdates page,
// UnifiedNotificationBell, FocalLayout banner) instead of hardReload() above.
// Safe because wwwroot/web.config already serves index.html, blazor.boot.json,
// blazor.webassembly.js and dotnet.js with no-cache headers while the
// content-hashed _framework/*.dll/.wasm files are cached for a year — so a
// normal reload always re-fetches the current manifest and only downloads
// the files that actually changed, instead of the entire runtime every time.
// Lighter (no wholesale cache wipe / forced bypass-cache fetches, which are
// slow and can behave inconsistently on mobile Safari) and just as correct.
window.syncReload = function () {
    window.location.reload();
};
