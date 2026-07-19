
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
    }
};