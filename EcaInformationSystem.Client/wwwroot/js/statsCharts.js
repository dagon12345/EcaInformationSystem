// Real, animated Chart.js charts for the Statistics page — replaces the old
// hand-rolled CSS bar charts (fixed-height divs with inline `style="height:%"`)
// with actual canvas charts (tooltips, legends, smooth entrance animation),
// for a page that's meant to look good in a live presentation. Chart.js
// requires destroying a canvas's previous Chart instance before drawing a
// new one on it (otherwise it silently duplicates/glitches), so every
// instance created here is tracked in `_instances` keyed by canvas id.
window.statsCharts = {
    _instances: {},

    _destroy(canvasId) {
        const existing = this._instances[canvasId];
        if (existing) {
            existing.destroy();
            delete this._instances[canvasId];
        }
    },

    renderPaymentStatus(canvasId, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") return;
        this._destroy(canvasId);

        this._instances[canvasId] = new Chart(canvas, {
            type: "doughnut",
            data: {
                labels: ["Paid", "Unpaid", "Pending", "N/A"],
                datasets: [{
                    data: [data.paid, data.unpaid, data.pending, data.notApplicable],
                    backgroundColor: ["#22c55e", "#ef4444", "#f59e0b", "#94a3b8"],
                    borderColor: "#fff",
                    borderWidth: 2,
                    hoverOffset: 10
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "62%",
                animation: { animateRotate: true, animateScale: true, duration: 900, easing: "easeOutCubic" },
                plugins: {
                    legend: { position: "bottom", labels: { padding: 16, font: { size: 12, weight: "600" } } },
                    tooltip: {
                        callbacks: {
                            label(ctx) {
                                const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                                const pct = total > 0 ? Math.round((ctx.parsed / total) * 100) : 0;
                                return `${ctx.label}: ${ctx.parsed.toLocaleString("en-US")} (${pct}%)`;
                            }
                        }
                    }
                }
            }
        });
    },

    renderAgeDistribution(canvasId, items) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") return;
        this._destroy(canvasId);

        const ageColors = { 80: "#0d6efd", 85: "#198754", 90: "#ffc107", 95: "#fd7e14", 100: "#dc3545" };

        this._instances[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: items.map(i => `Age ${i.age}`),
                datasets: [{
                    label: "Grantees",
                    data: items.map(i => i.count),
                    backgroundColor: items.map(i => ageColors[i.age] || "#0d6efd"),
                    borderRadius: 6,
                    maxBarThickness: 56
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 800, easing: "easeOutCubic" },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(ctx) { return `${ctx.parsed.y.toLocaleString("en-US")} grantees`; }
                        }
                    }
                },
                scales: {
                    y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#eef2f7" } },
                    x: { grid: { display: false } }
                }
            }
        });
    },

    renderMilestoneYears(canvasId, years) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") return;
        this._destroy(canvasId);

        this._instances[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: years.map(y => y.year.toString()),
                datasets: [
                    { label: "80", data: years.map(y => y.age80), backgroundColor: "#0d6efd" },
                    { label: "85", data: years.map(y => y.age85), backgroundColor: "#198754" },
                    { label: "90", data: years.map(y => y.age90), backgroundColor: "#ffc107" },
                    { label: "95", data: years.map(y => y.age95), backgroundColor: "#fd7e14" },
                    { label: "100", data: years.map(y => y.age100), backgroundColor: "#dc3545" }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 800, easing: "easeOutCubic" },
                plugins: {
                    legend: { position: "bottom", labels: { boxWidth: 12, font: { size: 11 } } }
                },
                scales: {
                    x: { stacked: true, grid: { display: false } },
                    y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#eef2f7" } }
                }
            }
        });
    },

    // Suggested addition — the Payroll Quarter Breakdown table had a peso
    // column with no chart alongside it, so "which quarter cost the most"
    // took row-by-row scanning. A simple bar answers that at a glance.
    renderQuarterDisbursement(canvasId, quarters) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") return;
        this._destroy(canvasId);

        this._instances[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: quarters.map(q => q.label),
                datasets: [{
                    label: "Disbursed",
                    data: quarters.map(q => q.disbursed),
                    backgroundColor: "#0891b2",
                    borderRadius: 6,
                    maxBarThickness: 48
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 800, easing: "easeOutCubic" },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(ctx) { return `₱${ctx.parsed.y.toLocaleString("en-US")}`; }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: "#eef2f7" },
                        ticks: { callback: (v) => "₱" + v.toLocaleString("en-US") }
                    },
                    x: { grid: { display: false } }
                }
            }
        });
    },

    // Liveness Verified vs Ready for EFT, side by side as two 2-slice
    // doughnuts sharing one chart (grouped bar so both fit one canvas) —
    // same visual language as renderPaymentStatus (doughnut, same easing/
    // tooltip pattern), just as a horizontal bar since a single doughnut
    // can't cleanly show two independent yes/no metrics at once.
    renderLivenessEft(canvasId, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") return;
        this._destroy(canvasId);

        this._instances[canvasId] = new Chart(canvas, {
            type: "bar",
            data: {
                labels: ["Liveness Verified", "Ready for EFT"],
                datasets: [
                    {
                        label: "Yes",
                        data: [data.livenessVerified, data.readyForEft],
                        backgroundColor: "#22c55e",
                        borderRadius: 6,
                        maxBarThickness: 48
                    },
                    {
                        label: "No / Not Set",
                        data: [data.total - data.livenessVerified, data.total - data.readyForEft],
                        backgroundColor: "#e2e8f0",
                        borderRadius: 6,
                        maxBarThickness: 48
                    }
                ]
            },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 800, easing: "easeOutCubic" },
                plugins: {
                    legend: { position: "bottom", labels: { boxWidth: 12, font: { size: 11 } } },
                    tooltip: {
                        callbacks: {
                            label(ctx) {
                                const pct = data.total > 0 ? Math.round((ctx.parsed.x / data.total) * 100) : 0;
                                return `${ctx.dataset.label}: ${ctx.parsed.x.toLocaleString("en-US")} (${pct}%)`;
                            }
                        }
                    }
                },
                scales: {
                    x: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#eef2f7" } },
                    y: { stacked: true, grid: { display: false } }
                }
            }
        });
    }
};
