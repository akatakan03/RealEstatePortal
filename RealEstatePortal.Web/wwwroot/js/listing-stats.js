// Opens the per-listing stats panel in a dialog. The panel is server-rendered HTML fetched
// on demand, so the charts stay in Razor where the rest of them live and the dashboard page
// doesn't pay for stats nobody asked to see.
(function () {
    var modal = document.getElementById('statsModal');
    if (!modal) return;

    var body = document.getElementById('statsModalBody');
    var title = document.getElementById('statsModalTitle');
    var lastTrigger = null;
    var currentRequest = 0;

    // Every page URL carries a language segment. Taking it from <html lang> keeps the request
    // on the page's own language and avoids paying for a redirect on every open.
    var lang = document.documentElement.lang;

    function open(id, name, trigger) {
        lastTrigger = trigger;
        title.textContent = name ? name : 'Listing stats';
        body.innerHTML = '<p class="stats-modal-status">Loading…</p>';

        if (typeof modal.showModal === 'function') modal.showModal();
        else modal.setAttribute('open', '');

        // Ignore anything that comes back for a listing the agent has already moved on from.
        var request = ++currentRequest;

        fetch('/' + lang + '/Dashboard/ListingStats/' + encodeURIComponent(id), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (res) {
                if (!res.ok) throw new Error(res.status === 404
                    ? 'That listing is no longer available.'
                    : 'Could not load the stats.');
                return res.text();
            })
            .then(function (html) {
                if (request !== currentRequest) return;
                body.innerHTML = html;
            })
            .catch(function (err) {
                if (request !== currentRequest) return;
                body.innerHTML = '<p class="stats-modal-status is-error"></p>';
                body.firstChild.textContent = err.message;
            });
    }

    function close() {
        currentRequest++;              // drop any answer still in flight
        if (typeof modal.close === 'function') modal.close();
        else modal.removeAttribute('open');
        body.innerHTML = '';
        if (lastTrigger) lastTrigger.focus();
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-stats-for]');
        if (trigger) {
            open(trigger.dataset.statsFor, trigger.dataset.statsTitle, trigger);
            return;
        }

        if (e.target.closest('[data-stats-close]')) close();
    });

    // Clicking the backdrop closes: the dialog element itself is the backdrop's hit area,
    // so a click that lands on it rather than on its contents means "outside".
    modal.addEventListener('click', function (e) {
        if (e.target === modal) close();
    });

    modal.addEventListener('close', function () {
        body.innerHTML = '';
    });
})();
