// Autosave + draft recovery for long forms. Entirely client-side (localStorage), so it works
// without any server round-trip and keeps no half-finished data on the server. Two promises to the
// user: (1) never lose typing to an accidental tab close, and (2) never surprise them — a saved
// draft is *offered*, never silently poured back into the form.
//
// initFormAutosave(formId, {
//   formKey,        // storage key suffix (defaults to formId); use a per-record key for edit forms
//   statusId,       // optional element that shows "saved · HH:MM"
//   maxAgeDays,     // forget drafts older than this (default 7)
//   labels: { title, restore, discard, saved, photosNote }
// })
function initFormAutosave(formId, options) {
    options = options || {};
    var form = document.getElementById(formId);
    if (!form || !window.localStorage) return;

    var key = 'rep:draft:' + (options.formKey || formId);
    var maxAge = (options.maxAgeDays || 7) * 86400000;
    var labels = options.labels || {};
    var statusEl = options.statusId ? document.getElementById(options.statusId) : null;
    var lang = document.documentElement.lang || undefined;

    // Fields worth saving: named controls, minus files (can't serialize), the anti-forgery token,
    // buttons, and anything explicitly opted out with data-no-autosave.
    function savableFields() {
        return Array.prototype.filter.call(form.elements, function (el) {
            if (!el.name) return false;
            if (el.type === 'file' || el.type === 'submit' || el.type === 'button' || el.type === 'reset') return false;
            if (el.name === '__RequestVerificationToken') return false;
            if (el.hasAttribute('data-no-autosave')) return false;
            return true;
        });
    }

    function serialize() {
        var data = {};
        savableFields().forEach(function (el) {
            if (el.type === 'checkbox') data[el.name] = el.checked;
            else if (el.type === 'radio') { if (el.checked) data[el.name] = el.value; }
            else data[el.name] = el.value;
        });
        return data;
    }

    // The form's state at load — server defaults, or posted values after a validation error. We
    // only save (and only offer to restore) when something differs from this, so a pristine form
    // never produces a phantom "you have a draft" prompt.
    var initial = JSON.stringify(serialize());

    function applyData(data) {
        savableFields().forEach(function (el) {
            if (!(el.name in data)) return;
            var val = data[el.name];
            if (el.type === 'checkbox') {
                el.checked = !!val;
            } else if (el.type === 'radio') {
                el.checked = (el.value === val);
            } else {
                el.value = val;
                // A rich-text editor mirrors a hidden <textarea>; push the value into its visible
                // editable area too, otherwise the restore wouldn't be seen.
                if (el.tagName === 'TEXTAREA') {
                    var rte = el.closest('.rte');
                    if (rte) {
                        var area = rte.querySelector('.rte-area');
                        if (area) area.innerHTML = val || '';
                    }
                }
            }
            // No synthetic change event on purpose: native controls already show their restored
            // value, and firing change here could, for example, make the address field re-geocode
            // and move the map pin away from the coordinates we just restored.
        });
    }

    // ---- saving --------------------------------------------------------------------------------

    var saveTimer = null;

    function saveNow() {
        var current = serialize();
        if (JSON.stringify(current) === initial) {
            // Back to pristine — drop any draft rather than storing a no-op.
            try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ }
            return;
        }
        try {
            window.localStorage.setItem(key, JSON.stringify({ data: current, ts: Date.now() }));
            showSaved();
        } catch (e) { /* storage full or blocked — best effort */ }
    }

    function scheduleSave() {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(saveNow, 600);
    }

    function showSaved() {
        if (!statusEl) return;
        var t = new Date().toLocaleTimeString(lang, { hour: '2-digit', minute: '2-digit' });
        statusEl.textContent = (labels.saved || 'Draft saved') + ' · ' + t;
        statusEl.classList.add('is-on');
    }

    // Typing anywhere in the form schedules a save; the rich-text editor edits a contenteditable
    // div (not a form field), so listen for input that bubbles up from it too.
    form.addEventListener('input', scheduleSave);
    form.addEventListener('change', scheduleSave);

    // Flush immediately when the tab is being hidden or closed — this is the moment the whole
    // feature exists for.
    window.addEventListener('pagehide', saveNow);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') saveNow();
    });

    // A real submission is no longer a draft. Clear on submit so a published listing doesn't leave
    // a stale "unsaved draft" behind on the next visit.
    form.addEventListener('submit', function () {
        try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ }
    });

    // ---- recovery ------------------------------------------------------------------------------

    function readDraft() {
        try {
            var raw = window.localStorage.getItem(key);
            if (!raw) return null;
            var d = JSON.parse(raw);
            if (!d || !d.data || !d.ts) return null;
            if (Date.now() - d.ts > maxAge) { window.localStorage.removeItem(key); return null; }
            return d;
        } catch (e) { return null; }
    }

    function offerRestore(draft) {
        var when = new Date(draft.ts).toLocaleString(lang, {
            day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
        });

        var bar = document.createElement('div');
        bar.className = 'draft-banner';

        var text = document.createElement('div');
        text.className = 'draft-banner-text';
        var strong = document.createElement('strong');
        strong.textContent = labels.title || 'You have an unsaved draft';
        text.appendChild(strong);
        text.appendChild(document.createTextNode(' · ' + when));
        if (labels.photosNote) {
            var note = document.createElement('div');
            note.className = 'draft-banner-note';
            note.textContent = labels.photosNote;
            text.appendChild(note);
        }

        var actions = document.createElement('div');
        actions.className = 'draft-banner-actions';

        var restore = document.createElement('button');
        restore.type = 'button';
        restore.className = 'btn btn-primary btn-sm';
        restore.textContent = labels.restore || 'Restore';
        restore.addEventListener('click', function () {
            applyData(draft.data);
            bar.remove();
            showSaved();
        });

        var discard = document.createElement('button');
        discard.type = 'button';
        discard.className = 'btn btn-ghost btn-sm';
        discard.textContent = labels.discard || 'Discard';
        discard.addEventListener('click', function () {
            try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ }
            bar.remove();
        });

        actions.appendChild(restore);
        actions.appendChild(discard);
        bar.appendChild(text);
        bar.appendChild(actions);
        form.insertBefore(bar, form.firstChild);
    }

    var saved = readDraft();
    // Only offer a restore when the draft actually differs from what's already on the form (a
    // draft identical to the current state, e.g. right after a validation-error re-render, is not
    // worth prompting about).
    if (saved && JSON.stringify(saved.data) !== initial) {
        offerRestore(saved);
    }
}
