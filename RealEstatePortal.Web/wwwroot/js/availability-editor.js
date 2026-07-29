// Dynamic rows for the agent availability editor: multiple time ranges per weekday (to leave a
// midday gap) and a list of one-off date exceptions. Fields submit as parallel arrays
// (winDay/winStart/winEnd and offDate/offStart/offEnd), so adding or removing a row is just DOM
// work — no index bookkeeping.
(function () {
    var form = document.getElementById('availabilityForm');
    if (!form) return;

    var windowTpl = document.getElementById('windowTemplate');
    var exceptionTpl = document.getElementById('exceptionTemplate');
    var exceptionList = document.getElementById('exceptionList');

    // Add a weekly-hours row to the day whose "+ Add hours" was clicked.
    form.addEventListener('click', function (e) {
        var addWindow = e.target.closest('.avail-add-window');
        if (addWindow) {
            var dayBlock = addWindow.closest('.avail-day');
            var row = windowTpl.content.firstElementChild.cloneNode(true);
            // Stamp the day onto the hidden field so the row belongs to the right weekday.
            row.querySelector('input[name="winDay"]').value = dayBlock.getAttribute('data-day');
            dayBlock.querySelector('.avail-windows').appendChild(row);
            return;
        }

        if (e.target.id === 'addException') {
            var ex = exceptionTpl.content.firstElementChild.cloneNode(true);
            exceptionList.appendChild(ex);
            return;
        }

        var remove = e.target.closest('.avail-remove');
        if (remove) {
            var wrapper = remove.closest('.avail-window, .avail-exception');
            if (wrapper) wrapper.remove();
        }
    });
})();
