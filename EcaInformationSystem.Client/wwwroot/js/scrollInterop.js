window.scrollInterop = {
    scrollToTop: function () {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    },
    scrollToElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    },
    registerScrollListener: function (dotNetHelper) {
        const handler = function () {
            const shouldShow = window.scrollY > 400;
            dotNetHelper.invokeMethodAsync('OnScrollChanged', shouldShow);
        };
        window.addEventListener('scroll', handler);

        // Return a handle so Blazor can remove this exact listener on dispose
        return handler;
    },
    unregisterScrollListener: function (handler) {
        window.removeEventListener('scroll', handler);
    }
};