// Opens the "book a viewing" panel in a dialog. The open-slot list is server-rendered HTML fetched
// the first time the dialog is opened, so the listing page doesn't pay for it up front. Slots cover
// the next two weeks (the booking horizon); the server is the authority on which are still free.
(function () {
    var modal = document.getElementById('apptModal');
    if (!modal) return;

    var body = document.getElementById('apptModalBody');
    var lang = document.documentElement.lang;
    var loadedFor = null;   // listing id whose slots are already in the dialog
    var lastTrigger = null;

    function open(id, trigger) {
        lastTrigger = trigger;
        if (typeof modal.showModal === 'function') modal.showModal();
        else modal.setAttribute('open', '');

        if (loadedFor === id) return; // already loaded — just reopen

        body.innerHTML = '<p class="appt-modal-status">…</p>';
        fetch('/' + lang + '/Appointments/Slots?listingId=' + encodeURIComponent(id))
            .then(function (r) { return r.status === 200 ? r.text() : null; })
            .then(function (html) {
                if (html === null) { body.innerHTML = '<p class="appt-modal-status is-error"></p>'; return; }
                body.innerHTML = html;
                loadedFor = id;
                wireSlots(body);
            })
            .catch(function () { body.innerHTML = '<p class="appt-modal-status is-error"></p>'; });
    }

    function close() {
        if (typeof modal.close === 'function') modal.close();
        else modal.removeAttribute('open');
        if (lastTrigger) lastTrigger.focus();
    }

    // Highlight the chosen slot and enable the submit button. Runs after the partial is injected.
    function wireSlots(root) {
        var form = root.querySelector('#apptForm');
        if (!form) return;
        var hidden = form.querySelector('#apptStart');
        var submit = form.querySelector('#apptSubmit');
        form.querySelectorAll('.appt-slot').forEach(function (btn) {
            btn.addEventListener('click', function () {
                form.querySelectorAll('.appt-slot').forEach(function (b) { b.classList.remove('is-selected'); });
                btn.classList.add('is-selected');
                hidden.value = btn.getAttribute('data-start');
                if (submit) submit.disabled = false;
            });
        });
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-appt-for]');
        if (trigger) { open(trigger.getAttribute('data-appt-for'), trigger); return; }
        if (e.target.closest('[data-appt-close]')) close();
    });

    // A click that lands on the dialog element itself (its backdrop area) means "outside".
    modal.addEventListener('click', function (e) {
        if (e.target === modal) close();
    });
})();
