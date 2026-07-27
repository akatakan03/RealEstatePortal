// Loan calculator — pure client-side arithmetic, recomputed as the buyer types. Turkish housing
// loans are equal-instalment (annuity) and, unlike consumer loans, carry no KKDF/BSMV, so the
// plain annuity formula is the whole story here.
(function () {
    function num(el, fallback) {
        var v = parseFloat(el && el.value);
        return isFinite(v) ? v : fallback;
    }

    function clamp(v, lo, hi) {
        return Math.min(Math.max(v, lo), hi);
    }

    function setMoney(root, sel, value, formatter, currency) {
        var el = root.querySelector(sel);
        if (!el) return;
        el.textContent = isFinite(value)
            ? formatter.format(Math.round(value)) + ' ' + currency
            : '—';
    }

    function recalc(root) {
        var lang = document.documentElement.lang || 'tr';
        var currency = root.getAttribute('data-currency') || '';
        var price = parseFloat(root.getAttribute('data-price')) || 0;
        var formatter = new Intl.NumberFormat(lang, { maximumFractionDigits: 0 });

        var downPct = clamp(num(root.querySelector('[data-m-down]'), 0), 0, 100);
        var months = parseInt(root.querySelector('[data-m-term]').value, 10) || 0;
        // A negative rate makes no sense; treat it as zero. Percent -> fraction.
        var monthlyRate = Math.max(num(root.querySelector('[data-m-rate]'), 0), 0) / 100;

        var loan = Math.max(price - price * downPct / 100, 0);

        var payment;
        if (months <= 0 || loan <= 0) {
            payment = 0;
        } else if (monthlyRate === 0) {
            payment = loan / months;               // no interest: principal split evenly
        } else {
            var factor = Math.pow(1 + monthlyRate, months);
            payment = loan * monthlyRate * factor / (factor - 1);
        }

        var total = payment * months;
        var interest = Math.max(total - loan, 0);

        setMoney(root, '[data-m-loan]', loan, formatter, currency);
        setMoney(root, '[data-m-payment]', payment, formatter, currency);
        setMoney(root, '[data-m-total]', total, formatter, currency);
        setMoney(root, '[data-m-interest]', interest, formatter, currency);
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-mortgage]').forEach(function (root) {
            root.addEventListener('input', function () { recalc(root); });
            root.addEventListener('change', function () { recalc(root); });
            recalc(root);   // fill the figures on first paint, before any interaction
        });
    });
})();
