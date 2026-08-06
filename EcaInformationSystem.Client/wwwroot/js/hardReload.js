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
