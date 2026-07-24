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
