// A minimal rich-text editor for the listing description: bold, italic, and the two list
// kinds. It wraps an existing <textarea> — the textarea stays in the form and keeps its name,
// so nothing about model binding or validation changes; we just mirror the editable HTML into
// it. Whatever ends up here is re-sanitized on the server, so the editor only has to be pleasant,
// not trusted.
function initRichEditor(textareaId, labels) {
    var ta = document.getElementById(textareaId);
    if (!ta) return;
    labels = labels || {};

    var wrap = document.createElement('div');
    wrap.className = 'rte';

    var bar = document.createElement('div');
    bar.className = 'rte-toolbar';

    var area = document.createElement('div');
    area.className = 'rte-area form-control';
    area.contentEditable = 'true';
    area.setAttribute('role', 'textbox');
    area.setAttribute('aria-multiline', 'true');
    area.innerHTML = ta.value || '';

    // Make Enter produce <p> (an allowed tag) rather than a bare <div>, so paragraphs survive.
    try { document.execCommand('defaultParagraphSeparator', false, 'p'); } catch (e) { /* older browsers */ }

    var commands = [
        { cmd: 'bold', text: 'B', className: 'rte-bold', label: labels.bold },
        { cmd: 'italic', text: 'I', className: 'rte-italic', label: labels.italic },
        { cmd: 'insertUnorderedList', text: '•', className: '', label: labels.bullets },
        { cmd: 'insertOrderedList', text: '1.', className: '', label: labels.numbers }
    ];

    var buttons = commands.map(function (c) {
        var b = document.createElement('button');
        b.type = 'button'; // never submit the form
        b.className = 'rte-btn ' + c.className;
        b.textContent = c.text;
        b.title = c.label || c.cmd;
        b.setAttribute('aria-label', c.label || c.cmd);
        // mousedown, not click: keep the selection in the editable area instead of stealing focus.
        b.addEventListener('mousedown', function (e) {
            e.preventDefault();
            area.focus();
            document.execCommand(c.cmd, false, null);
            sync();
            refreshActive();
        });
        bar.appendChild(b);
        return { el: b, cmd: c.cmd };
    });

    function isEmpty() {
        // An "empty" editor can still hold <br> or <p></p>; judge by the text, not the markup.
        return area.textContent.replace(/​/g, '').trim().length === 0;
    }

    function sync() {
        // Blank editors must submit "" so the NotEmpty rule fires instead of passing "<p><br></p>".
        ta.value = isEmpty() ? '' : area.innerHTML;
    }

    function refreshActive() {
        buttons.forEach(function (b) {
            var on = false;
            try { on = document.queryCommandState(b.cmd); } catch (e) { /* ignore */ }
            b.el.classList.toggle('is-active', on);
        });
    }

    area.addEventListener('input', sync);
    area.addEventListener('blur', sync);
    area.addEventListener('keyup', refreshActive);
    area.addEventListener('mouseup', refreshActive);

    // Place the editor where the textarea is, then tuck the textarea inside (hidden) as the field.
    ta.parentNode.insertBefore(wrap, ta);
    wrap.appendChild(bar);
    wrap.appendChild(area);
    wrap.appendChild(ta);
    ta.hidden = true;
    ta.setAttribute('aria-hidden', 'true');
    ta.tabIndex = -1;

    sync();
}
