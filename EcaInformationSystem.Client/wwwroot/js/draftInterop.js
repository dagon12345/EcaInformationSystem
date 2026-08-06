window.draftInterop = {
    // Watches an element for any 'input'/'change' activity from its descendants
    // (capture phase, so it still fires even if a child stops propagation) and
    // calls back into .NET a short debounce after the user stops typing —
    // "real-time" autosave without writing to localStorage on every keystroke.
    watchFormActivity: function (dotNetHelper, elementId, debounceMs) {
        const el = document.getElementById(elementId);
        if (!el) return null;

        const state = { timerId: null };
        const handler = function () {
            if (state.timerId) clearTimeout(state.timerId);
            state.timerId = setTimeout(function () {
                state.timerId = null;
                dotNetHelper.invokeMethodAsync('OnFormActivityDetected');
            }, debounceMs);
        };

        el.addEventListener('input', handler, true);
        el.addEventListener('change', handler, true);

        return { el: el, handler: handler, state: state };
    },
    unwatchFormActivity: function (handle) {
        if (!handle) return;
        if (handle.state && handle.state.timerId) clearTimeout(handle.state.timerId);
        handle.el.removeEventListener('input', handle.handler, true);
        handle.el.removeEventListener('change', handle.handler, true);
    }
};
