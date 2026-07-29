
window.feedInterop = {
    autoGrow: function (element) {
        element.style.height = 'auto';
        element.style.height = element.scrollHeight + 'px';
    },
    scrollToPost: function (postId) {
        const el = document.getElementById('post-' + postId);
        if (el) {
            // block: 'center' + nearest scroll container works for both the
            // page-level scroll (authenticated dashboard) and the nested
            // .auth-feed-column scroll (anonymous login-card layout) —
            // 'nearest' ensures it finds whichever ancestor actually scrolls,
            // rather than assuming it's always the window.
            el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
        }
    },
    getImageOffset: function (imgElement, clientX, clientY, currentZoom) {
        const rect = imgElement.getBoundingClientRect();
        const scale = currentZoom || 1;
        return {
            x: (clientX - rect.left) / scale,
            y: (clientY - rect.top) / scale
        };
    },
    // Backs the sticky-note bullet/checklist toolbar — reads the current
    // text selection range from a <textarea> so the C# side can figure out
    // which lines to prefix, same idea as Word's "apply to selected lines".
    getTextareaSelection: function (elementId) {
        const el = document.getElementById(elementId);
        if (!el) return { start: 0, end: 0 };
        return { start: el.selectionStart ?? 0, end: el.selectionEnd ?? 0 };
    }
};