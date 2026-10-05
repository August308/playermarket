/* Service worker — vỏ ngoại tuyến cho PlayerMarket (B8).
   Chiến lược: tài nguyên tĩnh lấy từ bộ đệm trước, trang HTML luôn ưu tiên mạng. */

const TEN_DEM = 'playermarket-v1';

const TAI_NGUYEN = [
    '/',
    '/css/site.css',
    '/css/chuyen-dong.css',
    '/js/site.js',
    '/js/chuyen-dong.js',
    '/js/bang-lenh.js',
    '/offline.html',
    '/manifest.webmanifest'
];

self.addEventListener('install', function (e) {
    e.waitUntil(
        caches.open(TEN_DEM)
            .then(function (dem) { return dem.addAll(TAI_NGUYEN); })
            .then(function () { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function (e) {
    e.waitUntil(
        caches.keys().then(function (ten) {
            return Promise.all(ten.filter(function (t) { return t !== TEN_DEM; })
                .map(function (t) { return caches.delete(t); }));
        }).then(function () { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function (e) {
    const yeuCau = e.request;

    if (yeuCau.method !== 'GET') return;

    const duong = new URL(yeuCau.url);
    if (duong.origin !== self.location.origin) return;

    // Trang HTML: ưu tiên mạng, hỏng thì trả trang ngoại tuyến.
    if (yeuCau.mode === 'navigate') {
        e.respondWith(
            fetch(yeuCau).catch(function () {
                return caches.match('/offline.html');
            })
        );
        return;
    }

    // Tài nguyên tĩnh: lấy bộ đệm trước, nền tải lại bản mới.
    e.respondWith(
        caches.match(yeuCau).then(function (daCo) {
            const tuMang = fetch(yeuCau).then(function (phanHoi) {
                if (phanHoi && phanHoi.status === 200) {
                    const ban = phanHoi.clone();
                    caches.open(TEN_DEM).then(function (dem) { dem.put(yeuCau, ban); });
                }
                return phanHoi;
            }).catch(function () { return daCo; });

            return daCo || tuMang;
        })
    );
});
