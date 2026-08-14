// Leaflet wrapper for the "Registered Region" map on the Statistics page.
// Styled to feel like Google Maps (Voyager basemap + a Map/Satellite layer
// toggle in the same corner Google puts it, scale bar, fullscreen, recenter)
// and actually shades the user's registered region's real boundary — not
// just a pin — using free basemap tiles + a free PH region-boundary GeoJSON
// shipped in wwwroot/data.
window.regionMapInterop = {
    _maps: {},

    // geoJsonUrl: same-origin path to a FeatureCollection with one region
    // polygon (fetched here directly — the injected HttpClient's
    // BaseAddress points at the API server, not this wasm host, so it can't
    // reach wwwroot static files). Null/404 falls back to a plain marker.
    // municipalityListUrl: same-origin path to a small JSON array of
    // {name, province, lat, lng} — ONLY the municipalities that belong to
    // this user's own region. The search box below matches purely against
    // this local, pre-scoped list (no geocoding API call of any kind), so
    // there is no way to type your way into another region or country —
    // the data simply doesn't exist to match against.
    async init(elementId, lat, lng, zoom, label, geoJsonUrl, municipalityListUrl) {
      try {
        this.destroy(elementId);

        const el = document.getElementById(elementId);
        if (!el) {
            console.error(`regionMapInterop: container #${elementId} not found in the DOM — the card may not have rendered yet`);
            return;
        }
        if (typeof L === "undefined") {
            console.error("regionMapInterop: Leaflet (window.L) is not loaded — the CDN <script> tag may have failed or not finished yet");
            return;
        }

        // zoomControl: true already creates and positions the +/- control
        // (top-left, Leaflet's default) — a second explicit L.control.zoom()
        // call here was creating a visibly duplicate +/- pair on the map.
        const map = L.map(elementId, {
            zoomControl: true,
            attributionControl: false,
            scrollWheelZoom: true
        }).setView([lat, lng], zoom);

        L.control.scale({ position: "bottomleft", metric: true, imperial: false }).addTo(map);

        // Google-Maps-style road basemap (free, no key) + a satellite
        // alternative, switchable via the same corner Google uses.
        // crossOrigin: true — the production host serves
        // Cross-Origin-Embedder-Policy: require-corp (see wwwroot/web.config),
        // which blocks any cross-origin image loaded in the default "no-cors"
        // mode Leaflet otherwise uses for tiles, regardless of what CORS
        // headers the tile server sends. Both CARTO and ArcGIS already send
        // Access-Control-Allow-Origin: * — this just makes the <img> tiles
        // request them in actual CORS mode so the browser can see that and
        // allow it, instead of silently dropping every tile in production
        // (worked in local dev only because dev doesn't set that header).
        const roadLayer = L.tileLayer(
            "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png",
            { maxZoom: 19, subdomains: "abcd", crossOrigin: true }
        ).addTo(map);

        const satelliteLayer = L.tileLayer(
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
            { maxZoom: 19, crossOrigin: true }
        );

        L.control.layers(
            { "Map": roadLayer, "Satellite": satelliteLayer },
            null,
            { position: "topright", collapsed: false }
        ).addTo(map);

        // Fullscreen toggle — Leaflet.fullscreen plugin, free/CDN, mirrors
        // the fullscreen button Google Maps embeds show top-right.
        // forcePseudoFullscreen: true — CSS-only "fake" fullscreen (fixed
        // position covering the viewport) instead of the real browser
        // Fullscreen API. The native API exits on its own the moment focus
        // leaves the fullscreened element or its DOM subtree mutates —
        // which is exactly what happens on every drag/zoom (Leaflet
        // reparents tile <img> elements as you pan), so the browser was
        // kicking us out of fullscreen mid-interaction. Pseudo-fullscreen
        // sidesteps that entirely since there's no native API involved.
        // NOTE: the plugin's actual option key is "forcePseudoFullscreen"
        // (not "pseudoFullscreen") — using the wrong key silently falls
        // back to real native fullscreen, which was the bug before this fix.
        // forceSeparateButton: true — by default the plugin merges its
        // button into the SAME bar as the zoom control (wherever that's
        // positioned), ignoring the "position" option below entirely. That
        // put a second, oddly-placed +/- -looking cluster on the map. Forcing
        // a separate button makes it its own tidy widget at "topright",
        // next to the Map/Satellite toggle where it visually belongs.
        if (typeof L.control.fullscreen === "function") {
            L.control.fullscreen({
                position: "topright",
                title: "Fullscreen",
                forcePseudoFullscreen: true,
                forceSeparateButton: true
            }).addTo(map);

            // Pseudo-fullscreen is plain CSS, not the browser's real
            // Fullscreen API — so the browser's built-in "ESC always exits
            // fullscreen" behavior doesn't apply here. Wire ESC up manually
            // so it still works the way people expect, but only listen
            // while actually in fullscreen so it doesn't swallow ESC
            // elsewhere on the page (e.g. closing an unrelated dropdown).
            const onEscape = (e) => {
                if (e.key === "Escape") map.toggleFullscreen();
            };
            map.on("enterFullscreen", () => document.addEventListener("keydown", onEscape));
            map.on("exitFullscreen", () => document.removeEventListener("keydown", onEscape));
            this._escHandlers[elementId] = onEscape;
        } else {
            console.error("regionMapInterop: L.control.fullscreen is not available — leaflet.fullscreen plugin failed to load");
        }

        let boundaryLayer = null;
        if (geoJsonUrl) {
            try {
                const resp = await fetch(geoJsonUrl);
                if (!resp.ok) throw new Error(`${resp.status} ${resp.statusText}`);
                const geoJson = await resp.json();
                boundaryLayer = L.geoJSON(geoJson, {
                    style: {
                        color: "#0C447C",
                        weight: 2,
                        fillColor: "#378ADD",
                        fillOpacity: 0.3
                    }
                }).addTo(map);
                boundaryLayer.bindTooltip(label, { sticky: true, direction: "top" });
                map.fitBounds(boundaryLayer.getBounds(), { padding: [16, 16] });
            } catch (e) {
                console.error("regionMapInterop: failed to load region GeoJSON", e);
                boundaryLayer = null;
            }
        }

        if (!boundaryLayer) {
            L.marker([lat, lng]).addTo(map).bindPopup(label).openPopup();
        }

        // Tracks whether the user has manually panned/zoomed, so the
        // ResizeObserver below (added for the SPA-navigation sizing bug)
        // doesn't yank their view back to the fitted boundary every time the
        // container's size ticks — only while they haven't touched it yet.
        let userInteracted = false;
        map.on("dragstart wheel dblclick", () => { userInteracted = true; });

        const resetView = () => {
            userInteracted = false;
            if (boundaryLayer) {
                map.fitBounds(boundaryLayer.getBounds(), { padding: [16, 16] });
            } else {
                map.setView([lat, lng], zoom);
            }
        };

        // Municipality search — scoped strictly to this region's own
        // municipality list (see the JSDoc note above on why that's safe).
        // Plain <input list="..."> + <datalist> gives free browser-native
        // autocomplete/keyboard handling without pulling in a search-UI
        // library just for this.
        let municipalityList = [];
        if (municipalityListUrl) {
            try {
                const resp = await fetch(municipalityListUrl);
                if (resp.ok) municipalityList = await resp.json();
            } catch (e) {
                console.error("regionMapInterop: failed to load municipality list", e);
            }
        }

        let datalistId = null;
        if (municipalityList.length > 0) {
            datalistId = `${elementId}-munis`;
            const datalist = document.createElement("datalist");
            datalist.id = datalistId;
            for (const m of municipalityList) {
                const opt = document.createElement("option");
                opt.value = m.name;
                datalist.appendChild(opt);
            }
            el.appendChild(datalist);
        }

        // One combined widget — search box (when this region has data for
        // it) plus a reset/recenter button, styled as a single Google-Maps-
        // style floating pill instead of two separate cramped controls.
        const SearchResetControl = L.Control.extend({
            options: { position: "topleft" },
            onAdd() {
                const wrap = L.DomUtil.create("div", "leaflet-control regionmap-searchbar");
                L.DomEvent.disableClickPropagation(wrap);
                L.DomEvent.disableScrollPropagation(wrap);

                let searchMarker = null;
                if (datalistId) {
                    const input = L.DomUtil.create("input", "regionmap-search-input", wrap);
                    input.type = "search";
                    input.placeholder = "Search municipality…";
                    input.autocomplete = "off";
                    input.setAttribute("list", datalistId);

                    const goToMunicipality = (query) => {
                        const needle = query.trim().toLowerCase();
                        const match = municipalityList.find(m => m.name.toLowerCase() === needle);
                        if (!match) return;

                        userInteracted = true;
                        map.flyTo([match.lat, match.lng], 12);

                        if (searchMarker) map.removeLayer(searchMarker);
                        searchMarker = L.marker([match.lat, match.lng]).addTo(map)
                            .bindPopup(`<strong>${match.name}</strong><br>${match.province}`)
                            .openPopup();
                    };

                    L.DomEvent.on(input, "keydown", (e) => {
                        if (e.key === "Enter") goToMunicipality(input.value);
                    });
                    L.DomEvent.on(input, "change", () => goToMunicipality(input.value));
                }

                // Reset/recenter — jumps back to the region's fitted view
                // and clears any search pin, the same job Google Maps' "my
                // location" button does for a fixed point of interest.
                const resetBtn = L.DomUtil.create("a", "regionmap-reset-btn", wrap);
                resetBtn.href = "#";
                resetBtn.title = "Reset to region view";
                resetBtn.setAttribute("aria-label", "Reset to region view");
                resetBtn.innerHTML = "&#8635;";
                L.DomEvent.on(resetBtn, "click", (e) => {
                    L.DomEvent.stop(e);
                    if (searchMarker) {
                        map.removeLayer(searchMarker);
                        searchMarker = null;
                    }
                    resetView();
                });

                return wrap;
            }
        });
        new SearchResetControl().addTo(map);

        this._maps[elementId] = map;

        // Watch the container's actual pixel size instead of guessing with a
        // fixed delay. A one-shot setTimeout worked on first page load but
        // not reliably on SPA back-navigation (different layout/paint timing
        // returning to the page), leaving the map stuck rendered into a
        // stale/zero-size canvas until a hard reload. ResizeObserver fires
        // every time the container's real size changes — including the
        // flex row still settling right after mount — so invalidateSize()
        // (and re-fitting the boundary) always runs against the real size.
        let lastWidth = 0;
        const observer = new ResizeObserver((entries) => {
            const width = entries[0]?.contentRect?.width ?? 0;
            if (width === lastWidth) return;
            lastWidth = width;
            map.invalidateSize();
            if (boundaryLayer && width > 0 && !userInteracted) {
                map.fitBounds(boundaryLayer.getBounds(), { padding: [16, 16] });
            }
        });
        observer.observe(el);

        this._observers[elementId] = observer;

        console.log(`regionMapInterop: map #${elementId} initialized ok`);
      } catch (e) {
        console.error(`regionMapInterop: init(#${elementId}) threw`, e);
        throw e;
      }
    },

    _observers: {},
    _escHandlers: {},

    destroy(elementId) {
        const observer = this._observers[elementId];
        if (observer) {
            observer.disconnect();
            delete this._observers[elementId];
        }

        // Navigating away while the card is mid-fullscreen shouldn't leave a
        // dangling document-level keydown listener referencing a map that's
        // about to be removed.
        const onEscape = this._escHandlers[elementId];
        if (onEscape) {
            document.removeEventListener("keydown", onEscape);
            delete this._escHandlers[elementId];
        }

        const map = this._maps[elementId];
        if (map) {
            map.remove();
            delete this._maps[elementId];
        }
    }
};
