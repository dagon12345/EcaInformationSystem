// Hover flyout for the sidebar's collapsed icon rail (NavMenu.razor). The
// rail's ancestor (.sidebar) needs overflow:hidden for its own width-collapse
// animation, which would clip a pure-CSS absolutely-positioned tooltip — so
// this uses one shared position:fixed element (fixed positioning escapes
// ancestor overflow clipping entirely), repositioned via getBoundingClientRect
// on hover instead. Event delegation on the sidebar itself (one listener,
// not one per nav item) so this doesn't care how many items/sections exist.
window.navFlyout = {
    _initialized: false,

    init() {
        if (this._initialized) return; // NavMenu can remount without a full page reload
        this._initialized = true;

        const sidebar = document.querySelector(".sidebar");
        const flyout = document.getElementById("nav-flyout");
        if (!sidebar || !flyout) return;

        let hideTimer = null;

        sidebar.addEventListener("mouseover", (e) => {
            if (!sidebar.classList.contains("collapsed")) return;

            const item = e.target.closest(".nav-item");
            if (!item || !sidebar.contains(item)) return;

            const label = item.querySelector(".nav-label");
            const text = (label?.textContent || item.getAttribute("title") || "").trim();
            if (!text) return;

            clearTimeout(hideTimer);

            const icon = item.querySelector(".nav-icon");
            const iconHtml = icon ? icon.outerHTML.replace(/class="nav-icon\b/, 'class="nav-flyout-icon') : "";

            const rect = item.getBoundingClientRect();
            flyout.innerHTML = iconHtml + "<span>" + text + "</span>";
            flyout.style.top = (rect.top + rect.height / 2) + "px";
            flyout.style.left = (rect.right + 10) + "px";
            flyout.classList.add("nav-flyout--show");
        });

        sidebar.addEventListener("mouseout", (e) => {
            const item = e.target.closest(".nav-item");
            if (!item) return;
            // Moving between children of the same item (icon -> label) isn't a real exit.
            if (item.contains(e.relatedTarget)) return;

            hideTimer = setTimeout(() => flyout.classList.remove("nav-flyout--show"), 40);
        });

        // Collapsing/expanding the sidebar mid-hover shouldn't leave a stale
        // flyout floating over the now-expanded labels.
        sidebar.addEventListener("transitionend", (e) => {
            if (e.propertyName === "width" && !sidebar.classList.contains("collapsed"))
                flyout.classList.remove("nav-flyout--show");
        });
    }
};
