// wwwroot/js/printInterop.js
// Scoped print — marks <body> so @media print can isolate just #annex-b-print-target,
// even when Annex A's print target is also present on the same page (e.g. the details
// offcanvas, or the Create/Edit form's Annex B preview). The marker is removed after the
// print dialog closes so normal (unmarked) printing elsewhere isn't affected.
window.printAnnexBOnly = function () {
    document.body.classList.add('printing-annex-b');

    const cleanup = () => document.body.classList.remove('printing-annex-b');
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    // Fallback in case 'afterprint' doesn't fire (some browsers/print-preview flows).
    setTimeout(cleanup, 3000);
};

// Same isolation trick as printAnnexBOnly, scoped to the Liveness Check and
// Transaction Account Form instead (#liveness-print-target).
window.printLivenessOnly = function () {
    document.body.classList.add('printing-liveness');

    const cleanup = () => document.body.classList.remove('printing-liveness');
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};

// Same isolation trick as printAnnexBOnly, scoped to Annex M (Liveness Check
// and Confirmation of Transaction Account Form — old-form variant, #annex-m-print-target).
window.printAnnexMOnly = function () {
    document.body.classList.add('printing-annex-m');

    const cleanup = () => document.body.classList.remove('printing-annex-m');
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};

// Same isolation trick as printAnnexBOnly, scoped to Annex N (Transaction
// Account Form — old-form variant, #annex-n-print-target).
window.printAnnexNOnly = function () {
    document.body.classList.add('printing-annex-n');

    const cleanup = () => document.body.classList.remove('printing-annex-n');
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};

// Same isolation trick as the others, scoped to the Daily Accomplishment
// Report print preview (#dar-print-target) — PLUS the same DOM relocation
// printCdrOnly() uses below, for the same reason: even though the DAR editor
// isn't inside a modal, it still lives inside the app's main-content column,
// which has its own overflow-y:auto/fixed-height scroll container (same
// clipping problem a modal body causes). That's why only ~1 page ever came
// out no matter what CSS position was tried directly on .dar-doc — the
// clipping happens on an ANCESTOR. Moving #dar-print-target to be a direct
// child of <body> (and #app to display:none, so its now-hidden height
// doesn't push the DAR down by a blank leading page) sidesteps all of that,
// exactly like printCdrOnly().
//
// PLUS a temporary <style> injection forcing "@page { size: A4 landscape }" —
// the rest of the app relies on a single unnamed @page rule (A4 portrait, in
// annexa-form.css) for every other document, and CSS named pages (an earlier
// "@page dar-page {...}" + "page: dar-page" on .dar-doc) turned out to be
// unreliable in Chromium's print engine here. An unnamed @page rule injected
// right before print — and removed right after — safely overrides the
// portrait default only for the duration of this one print job, without
// touching how any other document prints.
window.printDarOnly = function () {
    const target = document.getElementById('dar-print-target');
    let originalParent = null;
    let originalNextSibling = null;

    if (target) {
        originalParent = target.parentNode;
        originalNextSibling = target.nextSibling;
        document.body.appendChild(target);
    }

    const appRoot = document.getElementById('app');
    const appRootPreviousDisplay = appRoot ? appRoot.style.display : null;
    if (appRoot) appRoot.style.display = 'none';

    document.body.classList.add('printing-dar');

    const styleTag = document.createElement('style');
    styleTag.id = 'dar-print-page-size';
    styleTag.textContent = '@page { size: A4 landscape; margin: 8mm 12mm 8mm 8mm; }';
    document.head.appendChild(styleTag);

    const cleanup = () => {
        document.body.classList.remove('printing-dar');
        styleTag.remove();
        if (appRoot) appRoot.style.display = appRootPreviousDisplay || '';
        if (target && originalParent) {
            originalParent.insertBefore(target, originalNextSibling);
        }
    };
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};

// Measures the DAR's hidden off-screen row-by-row layout (rendered by
// DarPrintDocument.razor at #dar-measure-root — see its "MEASUREMENT PASS"
// comment) and hands the real pixel heights back to Blazor so pagination can
// be computed from actual rendered content instead of a guessed row count.
// Returns null if the measurement root isn't in the DOM yet.
window.measureDarLayout = function (rootId) {
    const root = document.getElementById(rootId);
    if (!root) return null;

    const rowHeights = Array.from(root.querySelectorAll('[data-dar-row]'))
        .map(el => el.getBoundingClientRect().height);

    const heightOf = (selector) => {
        const el = root.querySelector(selector);
        return el ? el.getBoundingClientRect().height : 0;
    };

    return {
        rowHeights: rowHeights,
        headerHeight: heightOf('[data-dar-header]'),
        theadHeight: heightOf('[data-dar-thead]'),
        footerHeight: heightOf('[data-dar-footer]'),
        signoffHeight: heightOf('[data-dar-signoff]')
    };
};

// Same isolation trick as the others, scoped to the CDR (Cash Disbursements
// Record) print preview (#cdr-print-target) — PLUS a DOM relocation, which
// the single-page forms above don't need.
//
// The CDR lives inside LiquidationModal's HxModal, which — like every
// Bootstrap modal — has a scrollable body (overflow-y:auto / max-height) and
// often a transform on .modal-dialog for its slide-in animation. Both of
// those clip/reposition any descendant regardless of that descendant's own
// position value: overflow clipping applies to positioned descendants too,
// and a transformed ancestor becomes the containing block for position:fixed
// children (so "fixed" doesn't actually escape to the real page origin the
// way it does for the un-transformed Annex B/Liveness modals). That combo is
// why a 26-page CDR only ever printed whatever ~1 page happened to be
// scrolled into view, no matter what CSS position was tried on the target
// itself — the clipping happens on an ANCESTOR, before the target's own
// styling gets a say.
//
// Physically moving #cdr-print-target to be a direct child of <body> sidesteps
// all of that: it's no longer inside the modal's scroll/transform context at
// all, so it flows and paginates normally. It's moved back to its original
// spot afterward so Blazor's component tree isn't disturbed.
// Same relocation trick as printDarOnly/printCdrOnly, scoped to the DTR
// (Daily Time Record, #dtr-print-target) print preview.
window.printDtrOnly = function () {
    const target = document.getElementById('dtr-print-target');
    let originalParent = null;
    let originalNextSibling = null;

    if (target) {
        originalParent = target.parentNode;
        originalNextSibling = target.nextSibling;
        document.body.appendChild(target);
    }

    const appRoot = document.getElementById('app');
    const appRootPreviousDisplay = appRoot ? appRoot.style.display : null;
    if (appRoot) appRoot.style.display = 'none';

    document.body.classList.add('printing-dtr');

    const cleanup = () => {
        document.body.classList.remove('printing-dtr');
        if (appRoot) appRoot.style.display = appRootPreviousDisplay || '';
        if (target && originalParent) {
            originalParent.insertBefore(target, originalNextSibling);
        }
    };
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};

window.printCdrOnly = function () {
    const target = document.getElementById('cdr-print-target');
    let originalParent = null;
    let originalNextSibling = null;

    if (target) {
        originalParent = target.parentNode;
        originalNextSibling = target.nextSibling;
        document.body.appendChild(target);
    }

    // ✅ ALSO needed — visibility:hidden (the isolation mechanism used above
    // and by printAnnexBOnly/printLivenessOnly) hides content but does NOT
    // remove it from layout; the box still reserves its full height. With
    // #cdr-print-target now the LAST child of <body>, the entire hidden
    // Blazor app root (#app — navbar, sidebar, the whole SPA) still occupies
    // its normal height above it, pushing the CDR down by exactly one blank
    // leading page. display:none actually removes #app from layout instead
    // of just hiding it, so nothing precedes the CDR content once it's moved.
    const appRoot = document.getElementById('app');
    const appRootPreviousDisplay = appRoot ? appRoot.style.display : null;
    if (appRoot) appRoot.style.display = 'none';

    document.body.classList.add('printing-cdr');

    const cleanup = () => {
        document.body.classList.remove('printing-cdr');
        if (appRoot) appRoot.style.display = appRootPreviousDisplay || '';
        if (target && originalParent) {
            originalParent.insertBefore(target, originalNextSibling);
        }
    };
    window.addEventListener('afterprint', cleanup, { once: true });

    window.print();

    setTimeout(cleanup, 3000);
};
