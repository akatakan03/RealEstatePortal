// The agent's listing table on the dashboard: status tabs and title search. A locked listing
// carries a second, full-width row underneath it (the re-review appeal), so rows are handled
// in pairs — filtering must never leave an appeal form behind without its listing.
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

})();
