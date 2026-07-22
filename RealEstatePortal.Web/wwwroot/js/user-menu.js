// Toggles the avatar dropdown in the navbar. Closes on outside click or Escape.
(function () {
    var menu = document.querySelector('[data-user-menu]');
    if (!menu) return;

    var trigger = menu.querySelector('[data-user-menu-trigger]');
    var panel = menu.querySelector('[data-user-menu-panel]');
    if (!trigger || !panel) return;

    function open() {
        menu.classList.add('is-open');
        trigger.setAttribute('aria-expanded', 'true');
    }

    function close() {
        menu.classList.remove('is-open');
        trigger.setAttribute('aria-expanded', 'false');
    }

    function isOpen() {
        return menu.classList.contains('is-open');
    }

    trigger.addEventListener('click', function (e) {
        e.stopPropagation();
        isOpen() ? close() : open();
    });

    document.addEventListener('click', function (e) {
        if (isOpen() && !menu.contains(e.target)) close();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && isOpen()) {
            close();
            trigger.focus();
        }
    });
})();
