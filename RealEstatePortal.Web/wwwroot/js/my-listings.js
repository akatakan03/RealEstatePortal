// Client-side filtering for the agent's "My listings" table: status tabs + title search.
(function () {
    var table = document.getElementById('myListingsTable');
    if (!table) return;

    var tabs = Array.prototype.slice.call(document.querySelectorAll('.filter-tab'));
    var search = document.getElementById('listingSearch');
    var rows = Array.prototype.slice.call(table.tBodies[0].rows);
    var noMatch = document.getElementById('noListingMatch');

    var activeFilter = 'all';

    function apply() {
        var term = (search.value || '').trim().toLowerCase();
        var visible = 0;

        rows.forEach(function (row) {
            var statusOk = activeFilter === 'all' || row.dataset.status === activeFilter;
            var titleOk = term === '' || (row.dataset.title || '').indexOf(term) !== -1;
            var show = statusOk && titleOk;
            row.hidden = !show;
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
