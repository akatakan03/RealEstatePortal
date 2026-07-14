(function () {
    if (!window.signalR) return;

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("inquiryReceived", function (data) {
        showToast(data);
        bumpBadge();
    });

    connection.start().catch(function (err) { console.error("SignalR:", err); });

    function bumpBadge() {
        // Nudge the unread-inquiries badge in the nav up by one, live.
        var badge = document.querySelector(".badge-count");
        if (badge) {
            badge.textContent = (parseInt(badge.textContent, 10) || 0) + 1;
        } else {
            // No badge yet (was at zero) — create one next to the Inquiries link.
            var inqLink = document.querySelector('.nav-links a[href*="Inquiries"]');
            if (inqLink) {
                var span = document.createElement("span");
                span.className = "badge-count";
                span.textContent = "1";
                inqLink.appendChild(document.createTextNode(" "));
                inqLink.appendChild(span);
            }
        }
    }

    function showToast(data) {
        var wrap = document.getElementById("toastWrap");
        if (!wrap) {
            wrap = document.createElement("div");
            wrap.id = "toastWrap";
            wrap.className = "toast-wrap";
            document.body.appendChild(wrap);
        }
        var toast = document.createElement("a");
        toast.className = "toast";
        toast.href = data.url || "/Inquiries";
        toast.innerHTML =
            '<div class="toast-title">New inquiry</div>' +
            '<div class="toast-body"><strong>' + escapeHtml(data.fromName) +
            '</strong> asked about “' + escapeHtml(data.listingTitle) + '”</div>';
        wrap.appendChild(toast);

        setTimeout(function () { toast.classList.add("show"); }, 20);
        setTimeout(function () {
            toast.classList.remove("show");
            setTimeout(function () { toast.remove(); }, 300);
        }, 6000);
    }

    function escapeHtml(s) {
        var d = document.createElement("div");
        d.textContent = s == null ? "" : s;
        return d.innerHTML;
    }
})();