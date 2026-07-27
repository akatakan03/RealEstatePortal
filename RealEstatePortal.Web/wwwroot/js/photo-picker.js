// Accumulating photo picker with per-tile removal.
//
// A file <input> replaces its whole selection every time the dialog is used, so picking
// a second batch would drop the first. We keep the running selection in a DataTransfer
// and write it back to the input, so what the user sees in the previews is exactly what
// gets submitted — additions accumulate and each tile can be removed on its own.
function initPhotoPicker(inputId, areaId) {
    var input = document.getElementById(inputId);
    var area = document.getElementById(areaId);
    if (!input || !area) return;

    var picked = new DataTransfer();

    function render() {
        area.innerHTML = '';
        Array.prototype.forEach.call(picked.files, function (file, index) {
            var wrap = document.createElement('div');
            wrap.className = 'preview-item';

            var img = document.createElement('img');
            img.className = 'preview-thumb';
            img.src = URL.createObjectURL(file);

            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'preview-remove';
            remove.textContent = '✕';
            remove.addEventListener('click', function () { removeAt(index); });

            wrap.appendChild(img);
            wrap.appendChild(remove);
            area.appendChild(wrap);
        });
    }

    function removeAt(index) {
        var next = new DataTransfer();
        Array.prototype.forEach.call(picked.files, function (file, i) {
            if (i !== index) next.items.add(file);
        });
        picked = next;
        input.files = picked.files;
        render();
    }

    input.addEventListener('change', function () {
        // On change the input holds only the newest batch — fold it into the running set.
        Array.prototype.forEach.call(input.files, function (file) {
            if (file.type.indexOf('image/') === 0) picked.items.add(file);
        });
        input.files = picked.files;
        render();
    });
}
