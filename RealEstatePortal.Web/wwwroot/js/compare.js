// Listing comparison — selection lives entirely in the browser. Ids are kept in localStorage,
// capped at four, and the compare bar (in the layout) follows the buyer across the site.
(function () {
    var KEY = 'compareIds';
    var MAX = 4;

    function read() {
        try {
            var v = JSON.parse(localStorage.getItem(KEY));
            return Array.isArray(v) ? v.filter(function (x) { return Number.isInteger(x); }) : [];
        } catch (e) {
            return [];
        }
    }

    function write(ids) {
        localStorage.setItem(KEY, JSON.stringify(ids));
    }

    // Every URL carries the page's own language: the redirect that rescues bare URLs skips a
    // hardcoded guess and would cost a round trip.
    function lang() {
        return document.documentElement.lang || 'tr';
    }

    function compareUrl(ids) {
        return '/' + lang() + '/Listings/Compare?' +
            ids.map(function (id) { return 'ids=' + encodeURIComponent(id); }).join('&');
    }

    function refreshBar() {
        var bar = document.getElementById('compareBar');
        if (!bar) return;

        var ids = read();
        var countEl = bar.querySelector('[data-compare-count]');
        if (countEl) countEl.textContent = ids.length;

        var go = bar.querySelector('[data-compare-go]');
        if (go) {
            go.href = compareUrl(ids);
            // Comparing needs at least two; below that the action is inert.
            var ready = ids.length >= 2;
            go.classList.toggle('is-disabled', !ready);
            if (ready) { go.removeAttribute('aria-disabled'); }
            else { go.setAttribute('aria-disabled', 'true'); }
        }

        bar.hidden = ids.length === 0;
    }

    function syncButtons() {
        var ids = read();
        document.querySelectorAll('[data-compare-id]').forEach(function (btn) {
            var id = parseInt(btn.getAttribute('data-compare-id'), 10);
            btn.classList.toggle('is-active', ids.indexOf(id) !== -1);
        });
    }

    window.compareToggle = function (event, btn) {
        event.preventDefault();
        event.stopPropagation();   // don't follow the card's link

        var id = parseInt(btn.getAttribute('data-compare-id'), 10);
        if (!Number.isInteger(id)) return;

        var ids = read();
        var at = ids.indexOf(id);
        if (at !== -1) {
            ids.splice(at, 1);
        } else {
            if (ids.length >= MAX) return;   // at the cap: ignore rather than silently drop one
            ids.push(id);
        }

        write(ids);
        syncButtons();
        refreshBar();
    };

    // Used by the compare page's per-column remove. Drops the id and reloads; if fewer than two
    // remain there is nothing left to compare, so it returns to browse.
    window.compareRemoveAndReload = function (id) {
        var ids = read().filter(function (x) { return x !== id; });
        write(ids);
        window.location = ids.length < 2 ? '/' + lang() + '/Listings' : compareUrl(ids);
    };

    document.addEventListener('DOMContentLoaded', function () {
        var bar = document.getElementById('compareBar');
        if (bar) {
            var clear = bar.querySelector('[data-compare-clear]');
            if (clear) clear.addEventListener('click', function () {
                write([]);
                syncButtons();
                refreshBar();
            });

            var go = bar.querySelector('[data-compare-go]');
            if (go) go.addEventListener('click', function (e) {
                if (read().length < 2) e.preventDefault();
            });
        }

        syncButtons();
        refreshBar();
    });
})();
