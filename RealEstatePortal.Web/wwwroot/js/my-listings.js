// The agent's listing table on the dashboard: status tabs, title search, and click-to-sort
// on the numeric columns. A locked listing carries a second, full-width row underneath it
// (the re-review appeal), so rows are handled in pairs — filtering or sorting must never
// separate an appeal form from the listing it belongs to.
(function () {
    var table = document.getElementById('myListingsTable');
    if (!table) return;

    var body = table.tBodies[0];
    var tabs = Array.prototype.slice.call(document.querySelectorAll('.filter-tab'));
    var search = document.getElementById('listingSearch');
    var noMatch = document.getElementById('noListingMatch');

    // Pair each listing row with the appeal row that follows it, if any.
    var items = Array.prototype.slice.call(body.rows)
        .filter(function (r) { return !r.classList.contains('locked-row'); })
        .map(function (row) {
            var next = row.nextElementSibling;
            return {
                row: row,
                extra: next && next.classList.contains('locked-row') ? next : null
            };
        });

    var activeFilter = 'all';

    function apply() {
        var term = (search && search.value || '').trim().toLowerCase();
        var visible = 0;

        items.forEach(function (item) {
            var data = item.row.dataset;
            var statusOk = activeFilter === 'all' || data.status === activeFilter;
            var titleOk = term === '' || (data.title || '').indexOf(term) !== -1;
            var show = statusOk && titleOk;
            item.row.hidden = !show;
            if (item.extra) item.extra.hidden = !show;
            if (show) visible++;
        });

        if (noMatch) noMatch.hidden = visible !== 0;
    }

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            tabs.forEach(function (t) { t.classList.remove('is-active'); });
            tab.classList.add('is-active');
            activeFilter = tab.dataset.filter;
            apply();
        });
    });

    if (search) search.addEventListener('input', apply);

    // --- Sorting -----------------------------------------------------------
    var headers = Array.prototype.slice.call(table.querySelectorAll('th.sortable'));
    var sortKey = null;
    var descending = true;

    headers.forEach(function (th) {
        th.setAttribute('role', 'button');
        th.setAttribute('tabindex', '0');

        function sort() {
            var key = th.dataset.sort;
            // Same column toggles direction; a new column starts at "biggest first",
            // which is what someone ranking their listings is looking for.
            descending = key === sortKey ? !descending : true;
            sortKey = key;

            items.sort(function (a, b) {
                var av = parseInt(a.row.dataset[key], 10) || 0;
                var bv = parseInt(b.row.dataset[key], 10) || 0;
                return descending ? bv - av : av - bv;
            });

            items.forEach(function (item) {
                body.appendChild(item.row);
                if (item.extra) body.appendChild(item.extra);
            });

            headers.forEach(function (h) {
                h.classList.remove('sorted-asc', 'sorted-desc');
                h.removeAttribute('aria-sort');
            });
            th.classList.add(descending ? 'sorted-desc' : 'sorted-asc');
            th.setAttribute('aria-sort', descending ? 'descending' : 'ascending');
        }

        th.addEventListener('click', sort);
        th.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); sort(); }
        });
    });
})();
