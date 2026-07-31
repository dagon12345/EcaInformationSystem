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
