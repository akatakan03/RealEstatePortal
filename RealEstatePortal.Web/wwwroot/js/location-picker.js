function initLocationPicker(opts) {
    var latInput = document.getElementById(opts.latId);
    var lngInput = document.getElementById(opts.lngId);
    var addressInput = document.getElementById(opts.addressId);
    var hasInitial = opts.initialLat !== null && opts.initialLng !== null;

    var map = L.map(opts.mapId).setView(
        hasInitial ? [opts.initialLat, opts.initialLng] : [39.0, 35.0],  // Turkey overview
        hasInitial ? 15 : 6);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);

    var marker = null;

    function writeCoords(lat, lng) {
        latInput.value = lat.toFixed(6);   // dot-decimal, culture-safe
        lngInput.value = lng.toFixed(6);
    }

    function setMarker(lat, lng, recenter) {
        if (marker) {
            marker.setLatLng([lat, lng]);
        } else {
            marker = L.marker([lat, lng], { draggable: true }).addTo(map);
            marker.on('dragend', function () {
                var p = marker.getLatLng();
                writeCoords(p.lat, p.lng);
            });
        }
        writeCoords(lat, lng);
        if (recenter) map.setView([lat, lng], 15);
    }

    if (hasInitial) setMarker(opts.initialLat, opts.initialLng, false);

    // Click anywhere on the map to drop / move the pin.
    map.on('click', function (e) { setMarker(e.latlng.lat, e.latlng.lng, false); });

    // "Locate on map" geocodes the typed address as a starting guess.
    var btn = document.getElementById(opts.locateBtnId);
    btn.addEventListener('click', function () {
        var q = addressInput.value.trim();
        if (!q) { alert('Enter an address first.'); return; }
        btn.disabled = true;
        fetch('/Listings/Geocode?q=' + encodeURIComponent(q))
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (d.found) { setMarker(d.lat, d.lng, true); }
                else { alert('Could not find that address. Click the map to place the pin manually.'); }
            })
            .finally(function () { btn.disabled = false; });
    });
}