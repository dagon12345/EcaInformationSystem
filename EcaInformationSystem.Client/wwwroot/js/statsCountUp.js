// Animates the Statistics page's summary-card numbers counting up from 0 to
// their loaded value instead of just popping in — purely a presentation
// touch, triggered once per successful report load (see Statistics.razor).
window.statsCountUp = {
    run(rootId) {
        const root = document.getElementById(rootId);
        if (!root) return;

        const els = root.querySelectorAll("[data-countup]");
        const duration = 700;
        const easeOutCubic = t => 1 - Math.pow(1 - t, 3);

        els.forEach(el => {
            const target = parseFloat(el.getAttribute("data-countup"));
            if (isNaN(target)) return;

            const prefix = el.getAttribute("data-countup-prefix") || "";
            const suffix = el.getAttribute("data-countup-suffix") || "";
            const isInteger = Number.isInteger(target);
            const start = performance.now();

            const tick = (now) => {
                const elapsed = now - start;
                const progress = Math.min(1, elapsed / duration);
                const eased = easeOutCubic(progress);
                const current = target * eased;
                const formatted = isInteger
                    ? Math.round(current).toLocaleString("en-US")
                    : current.toLocaleString("en-US", { maximumFractionDigits: 0 });

                el.textContent = `${prefix}${formatted}${suffix}`;

                if (progress < 1) {
                    requestAnimationFrame(tick);
                }
            };

            requestAnimationFrame(tick);
        });
    }
};
