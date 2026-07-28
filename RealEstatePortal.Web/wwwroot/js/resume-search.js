// "Pick up where you left off" for the listings browser.
//
// Entirely client-side (localStorage) so it works for anonymous visitors and keeps no personal
// data on the server. When the visitor lands on the browse page WITH an active search, we quietly
// remember the exact query string. When they later return to a clean browse page (no filters), we
// offer — never force — to restore that last search via a small dismissible bar.
(function () {
    var bar = document.getElementById('resumeBar');
    if (!bar || !window.localStorage) return;

    var KEY = 'rep:lastBrowse';
    var MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000; // forget searches older than 30 days

    // Params that constitute a real search worth remembering. Sort/paging alone don't count —
    // landing on page 2 of nothing shouldn't be offered back later.
    var FILTER_KEYS = ['q', 'Keyword', 'ListingType', 'PropertyType', 'MaxPrice', 'MinPrice',
        'Heating', 'Internet', 'MinBedrooms', 'MaxDues', 'Furnished', 'Parking', 'Balcony', 'CenterLat'];

    var params = new URLSearchParams(window.location.search);
    var hasActiveSearch = FILTER_KEYS.some(function (k) {
        var v = params.get(k);
        return v !== null && v !== '';
    });

    function read() {
        try {
            var raw = window.localStorage.getItem(KEY);
            if (!raw) return null;
            var data = JSON.parse(raw);
            if (!data || !data.query || !data.ts) return null;
            if (Date.now() - data.ts > MAX_AGE_MS) { window.localStorage.removeItem(KEY); return null; }
            return data;
        } catch (e) { return null; }
    }

    if (hasActiveSearch) {
        // Remember this search (summary is rendered server-side, so enum labels are localized).
        try {
            window.localStorage.setItem(KEY, JSON.stringify({
                query: window.location.search,
                summary: bar.getAttribute('data-summary') || '',
                ts: Date.now()
            }));
        } catch (e) { /* storage full or blocked — resume is best-effort */ }
        return; // never prompt while the visitor is already mid-search
    }

    // Clean browse page: offer to restore, if we have something recent.
    var saved = read();
    if (!saved) return;

    var summaryEl = bar.querySelector('.resume-summary');
    if (summaryEl) summaryEl.textContent = saved.summary || bar.getAttribute('data-fallback') || '';

    var continueBtn = bar.querySelector('.resume-continue');
    if (continueBtn) {
        continueBtn.addEventListener('click', function () {
            window.location = window.location.pathname + saved.query;
        });
    }

    var dismissBtn = bar.querySelector('.resume-dismiss');
    if (dismissBtn) {
        dismissBtn.addEventListener('click', function () {
            try { window.localStorage.removeItem(KEY); } catch (e) { /* ignore */ }
            bar.hidden = true;
        });
    }

    bar.hidden = false;
})();
