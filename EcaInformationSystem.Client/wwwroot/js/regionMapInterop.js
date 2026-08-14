// Leaflet wrapper for the "Registered Region" map on the Statistics page.
// Styled to feel like Google Maps (Voyager basemap + a Map/Satellite layer
// toggle in the same corner Google puts it, scale bar, fullscreen, recenter)
// and actually shades the user's registered region's real boundary — not
// just a pin — using free basemap tiles + a free PH region-boundary GeoJSON
// shipped in wwwroot/data.
// PSGC-official municipality names (what the app's own database uses, e.g.
// "City of Butuan") and the free boundary/centroid dataset's names (e.g.
// "Butuan City") disagree on word order for every "City" entry — same place,
// different string. Stripping the "city of "/" city" wrapper before
// comparing makes both sides normalize to the same "butuan" and match.
function normalizeMuniName(name) {
    return (name || "")
        .toLowerCase()
        .trim()
        .replace(/^city of\s+/, "")
        .replace(/\s+city$/, "");
}

window.regionMapInterop = {
    _maps: {},

    // geoJsonUrl: same-origin path to a FeatureCollection with one region
    // polygon (fetched here directly — the injected HttpClient's
    // BaseAddress points at the API server, not this wasm host, so it can't
    // reach wwwroot static files). Null/404 falls back to a plain marker.
    // municipalityListUrl: same-origin path to a GeoJSON FeatureCollection
    // (name/province/lat/lng + boundary geometry per feature) — ONLY the
    // municipalities that belong to this user's own region. The search box
    // below matches purely against this local, pre-scoped list (no
    // geocoding API call of any kind), so there is no way to type your way
    // into another region or country — the data simply doesn't exist to
    // match against.
    async init(elementId, lat, lng, zoom, label, geoJsonUrl, municipalityListUrl) {
      try {
        this.destroy(elementId);

        const el = document.getElementById(elementId);
        if (!el) {
            // Throwing (instead of a silent return) is deliberate: this
            // rejects the C# InvokeVoidAsync call, which is what lets
            // StatisticsUserRegionMap.razor's retry loop actually detect the
            // failure and try again. A silent return here used to leave the
            // map permanently blank on the timing races where the container
            // isn't painted yet, since Blazor was never told anything failed.
            throw new Error(`container #${elementId} not found in the DOM yet`);
        }
        if (typeof L === "undefined") {
            throw new Error("Leaflet (window.L) is not loaded — the CDN <script> tag may have failed or not finished yet");
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

        // Municipality list — scoped strictly to this region's own
        // municipalities (see the JSDoc note above on why that's safe), used
        // by focusMunicipality() when Statistics.razor's Apply includes a
        // Municipality filter. The file is a GeoJSON FeatureCollection (name/
        // province/lat/lng in
        // each feature's properties, plus its actual boundary geometry) so a
        // focused municipality can be shaded like the region is, not just
        // pinned with a marker.
        let municipalityList = [];
        if (municipalityListUrl) {
            try {
                const resp = await fetch(municipalityListUrl);
                if (resp.ok) {
                    const geoJson = await resp.json();
                    municipalityList = (geoJson.features || []).map(f => ({
                        ...f.properties,
                        geometry: f.geometry
                    }));
                }
            } catch (e) {
                console.error("regionMapInterop: failed to load municipality list", e);
            }
        }

        // Shared "jump to a municipality" logic — driven externally via
        // regionMapInterop.focusMunicipality(), which Statistics.razor calls
        // when the Municipality filter is part of an applied filter (the
        // in-map search box that used to also call this was removed — this
        // stays the single code path for that jump behavior).
        let searchMarker = null;
        let municipalityFocusLayer = null;
        const focusMunicipality = (query) => {
            const needle = normalizeMuniName(query);
            const match = municipalityList.find(m => normalizeMuniName(m.name) === needle);
            if (!match) return false;

            userInteracted = true;

            if (municipalityFocusLayer) { map.removeLayer(municipalityFocusLayer); municipalityFocusLayer = null; }
            if (searchMarker) { map.removeLayer(searchMarker); searchMarker = null; }

            if (match.geometry) {
                // Same shading treatment as the region boundary, just a
                // different color so it reads as "zoomed into a place inside
                // the region" rather than "this is the whole region."
                municipalityFocusLayer = L.geoJSON(
                    { type: "Feature", geometry: match.geometry, properties: {} },
                    { style: { color: "#B8860B", weight: 3, fillColor: "#FFD54F", fillOpacity: 0.4 } }
                ).addTo(map);
                map.fitBounds(municipalityFocusLayer.getBounds(), { padding: [24, 24] });
            } else {
                // No boundary shipped for this one — just zoom to it, the pin
                // below still marks exactly where.
                map.flyTo([match.lat, match.lng], 12);
            }

            // Pin + always-visible name label — shown whenever a Municipality
            // filter is applied, not just on hover/click, so it's obvious at
            // a glance which municipality the map zoomed to.
            searchMarker = L.marker([match.lat, match.lng]).addTo(map)
                .bindTooltip(`${match.name}, ${match.province}`, {
                    permanent: true,
                    direction: "top",
                    offset: [0, -36],
                    className: "regionmap-muni-label"
                })
                .openTooltip();
            return true;
        };
        this._focusFns[elementId] = focusMunicipality;

        // No floating in-map reset button anymore (removed for a cleaner
        // map) — the header's reload button and the Clear button in the
        // filter panel (via regionMapInterop.resetToRegion below) are the
        // only ways back to the fitted region view now.
        //
        // clearFocusAndReset is still exposed to the outside world as
        // regionMapInterop.resetToRegion, so clicking Clear in the filter
        // panel can snap the map back to the whole-region view.
        const clearFocusAndReset = () => {
            if (searchMarker) {
                map.removeLayer(searchMarker);
                searchMarker = null;
            }
            if (municipalityFocusLayer) {
                map.removeLayer(municipalityFocusLayer);
                municipalityFocusLayer = null;
            }
            resetView();
        };
        this._resetFns[elementId] = clearFocusAndReset;

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
    _focusFns: {},
    _resetFns: {},

    // Called from Statistics.razor after Apply, when the applied filter set
    // includes a Municipality — pans/zooms the map to it exactly like typing
    // it into the in-map search box would. No-ops (returns false) if the map
    // isn't initialized yet or the name doesn't match anything in this
    // region's municipality list (e.g. the filtered municipality belongs to
    // a different region than the one this map is showing).
    focusMunicipality(elementId, municipalityName) {
        const fn = this._focusFns[elementId];
        return fn ? fn(municipalityName) : false;
    },

    // Called from Statistics.razor's Clear button — snaps the map back to
    // the fitted whole-region view and clears any municipality pin, the same
    // as clicking the in-map reset button.
    resetToRegion(elementId) {
        const fn = this._resetFns[elementId];
        if (fn) fn();
    },

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

        delete this._focusFns[elementId];
        delete this._resetFns[elementId];

        const map = this._maps[elementId];
        if (map) {
            map.remove();
            delete this._maps[elementId];
        }
    }
};
