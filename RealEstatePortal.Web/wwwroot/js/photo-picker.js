// Accumulating photo picker with per-tile removal and an in-place crop editor.
//
// A file <input> replaces its whole selection every time the dialog is used, so picking
// a second batch would drop the first. We keep the running selection in a DataTransfer
// and write it back to the input, so what the user sees in the previews is exactly what
// gets submitted.
//
// The server keeps the whole image (it only downscales, never crops), but the cover is
// shown through a fixed 16/9 frame with object-fit: cover, so a tall photo gets
// centre-cropped — often through the wrong part. The editor lets the customer frame that
// crop themselves: it exports the framed region as a new file that already matches the
// display ratio, so the cover shows exactly what they chose.
function initPhotoPicker(inputId, areaId, options) {
    var input = document.getElementById(inputId);
    var area = document.getElementById(areaId);
    if (!input || !area) return;

    options = options || {};
    var aspect = options.aspect || 16 / 9;
    var labels = Object.assign({
        edit: 'Edit photo', title: 'Adjust photo', hint: '', zoom: 'Zoom',
        apply: 'Apply', reset: 'Reset', cancel: 'Cancel'
    }, options.labels || {});

    var picked = new DataTransfer();
    var editor = createEditor(aspect, labels);

    function render() {
        area.innerHTML = '';
        Array.prototype.forEach.call(picked.files, function (file, index) {
            var wrap = document.createElement('div');
            wrap.className = 'preview-item';

            var img = document.createElement('img');
            img.className = 'preview-thumb';
            img.src = URL.createObjectURL(file);
            img.onload = function () { URL.revokeObjectURL(img.src); };

            var edit = document.createElement('button');
            edit.type = 'button';
            edit.className = 'preview-edit';
            edit.title = labels.edit;
            edit.setAttribute('aria-label', labels.edit);
            edit.textContent = '✎';
            edit.addEventListener('click', function () {
                editor.open(picked.files[index], function (newFile) { replaceAt(index, newFile); });
            });

            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'preview-remove';
            remove.title = labels.cancel;
            remove.textContent = '✕';
            remove.addEventListener('click', function () { removeAt(index); });

            wrap.appendChild(img);
            wrap.appendChild(edit);
            wrap.appendChild(remove);
            area.appendChild(wrap);
        });
    }

    function rebuild(mapFn) {
        var next = new DataTransfer();
        Array.prototype.forEach.call(picked.files, mapFn.bind(null, next));
        picked = next;
        input.files = picked.files;
        render();
    }

    function removeAt(index) {
        rebuild(function (next, file, i) { if (i !== index) next.items.add(file); });
    }

    function replaceAt(index, newFile) {
        rebuild(function (next, file, i) { next.items.add(i === index ? newFile : file); });
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

// A self-contained pan-and-zoom cropper rendered onto a canvas. One instance is shared by
// every tile of a picker; open() rebinds it to whichever file is being edited.
function createEditor(aspect, labels) {
    var FRAME_W = 500, FRAME_H = Math.round(FRAME_W / aspect);
    var MAX_OUT_W = 1600; // matches the server's display max edge — no point exporting larger

    var overlay = document.createElement('div');
    overlay.className = 'ph-modal';
    overlay.hidden = true;
    overlay.innerHTML =
        '<div class="ph-modal-box" role="dialog" aria-modal="true">' +
        '  <h3 class="ph-modal-title"></h3>' +
        '  <p class="ph-hint"></p>' +
        '  <div class="ph-stage"><canvas class="ph-canvas"></canvas></div>' +
        '  <label class="ph-zoom"><span class="ph-zoom-label"></span>' +
        '    <input type="range" class="ph-zoom-range" min="1" max="4" step="0.01" value="1"></label>' +
        '  <div class="ph-modal-actions">' +
        '    <button type="button" class="btn btn-ghost ph-cancel"></button>' +
        '    <button type="button" class="btn btn-primary ph-apply"></button>' +
        '  </div>' +
        '</div>';
    document.body.appendChild(overlay);

    overlay.querySelector('.ph-modal-title').textContent = labels.title;
    overlay.querySelector('.ph-hint').textContent = labels.hint;
    overlay.querySelector('.ph-zoom-label').textContent = labels.zoom;
    overlay.querySelector('.ph-cancel').textContent = labels.cancel;
    overlay.querySelector('.ph-apply').textContent = labels.apply;

    var canvas = overlay.querySelector('.ph-canvas');
    var zoomRange = overlay.querySelector('.ph-zoom-range');
    var ctx = canvas.getContext('2d');
    var dpr = window.devicePixelRatio || 1;
    canvas.width = FRAME_W * dpr;
    canvas.height = FRAME_H * dpr;
    canvas.style.aspectRatio = aspect;

    var img = new Image();
    var srcUrl = null;
    var baseScale = 1, offX = 0, offY = 0, prevScale = 1;
    var currentFile = null, onDone = null;

    function scale() { return baseScale * parseFloat(zoomRange.value); }

    function clamp() {
        // Keep the image covering the whole frame — no empty gaps at the edges.
        var w = img.width * scale(), h = img.height * scale();
        offX = Math.min(0, Math.max(FRAME_W - w, offX));
        offY = Math.min(0, Math.max(FRAME_H - h, offY));
    }

    function draw() {
        clamp();
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, FRAME_W, FRAME_H);
        ctx.drawImage(img, offX, offY, img.width * scale(), img.height * scale());
    }

    img.onload = function () {
        baseScale = Math.max(FRAME_W / img.width, FRAME_H / img.height);
        zoomRange.value = 1;
        // Centre the image in the frame.
        offX = (FRAME_W - img.width * baseScale) / 2;
        offY = (FRAME_H - img.height * baseScale) / 2;
        prevScale = baseScale;
        draw();
    };

    // --- panning (pointer events cover mouse + touch) ---
    var dragging = false, lastX = 0, lastY = 0;
    canvas.addEventListener('pointerdown', function (e) {
        dragging = true; lastX = e.clientX; lastY = e.clientY;
        canvas.setPointerCapture(e.pointerId);
    });
    canvas.addEventListener('pointermove', function (e) {
        if (!dragging) return;
        var rect = canvas.getBoundingClientRect();
        var k = FRAME_W / rect.width; // CSS px -> logical px
        offX += (e.clientX - lastX) * k;
        offY += (e.clientY - lastY) * k;
        lastX = e.clientX; lastY = e.clientY;
        draw();
    });
    function endDrag() { dragging = false; }
    canvas.addEventListener('pointerup', endDrag);
    canvas.addEventListener('pointercancel', endDrag);

    canvas.addEventListener('wheel', function (e) {
        e.preventDefault();
        var v = parseFloat(zoomRange.value) * (e.deltaY < 0 ? 1.08 : 0.92);
        zoomRange.value = Math.min(parseFloat(zoomRange.max), Math.max(parseFloat(zoomRange.min), v));
        zoomRange.dispatchEvent(new Event('input'));
    }, { passive: false });

    zoomRange.addEventListener('input', function () {
        // Zoom around the frame centre so the focus point stays put. The range value has
        // already changed, so the pre-zoom scale comes from prevScale, not scale().
        var s1 = scale();
        var cx = (FRAME_W / 2 - offX) / prevScale, cy = (FRAME_H / 2 - offY) / prevScale;
        offX = FRAME_W / 2 - cx * s1;
        offY = FRAME_H / 2 - cy * s1;
        prevScale = s1;
        draw();
    });

    function close() {
        overlay.hidden = true;
        if (srcUrl) { URL.revokeObjectURL(srcUrl); srcUrl = null; }
    }

    overlay.querySelector('.ph-cancel').addEventListener('click', close);
    overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });

    overlay.querySelector('.ph-apply').addEventListener('click', function () {
        var s = scale();
        var srcX = -offX / s, srcY = -offY / s, srcW = FRAME_W / s, srcH = FRAME_H / s;
        var outW = Math.min(Math.round(srcW), MAX_OUT_W);
        var outH = Math.round(outW / aspect);

        var out = document.createElement('canvas');
        out.width = outW; out.height = outH;
        out.getContext('2d').drawImage(img, srcX, srcY, srcW, srcH, 0, 0, outW, outH);
        out.toBlob(function (blob) {
            var base = (currentFile.name || 'photo').replace(/\.[^.]+$/, '');
            var file = new File([blob], base + '.jpg', { type: 'image/jpeg' });
            if (onDone) onDone(file);
            close();
        }, 'image/jpeg', 0.9);
    });

    return {
        open: function (file, done) {
            currentFile = file; onDone = done;
            if (srcUrl) URL.revokeObjectURL(srcUrl);
            srcUrl = URL.createObjectURL(file);
            img.src = srcUrl;
            overlay.hidden = false;
        }
    };
}
