/* =====================================================================
   PlayerMarket — ĐIỀU KHIỂN CHUYỂN ĐỘNG, phiên bản 3.0

   Đi kèm chuyen-dong.css v3. Mọi hiệu ứng đều hỏi tatChuyenDong() trước,
   nên người dùng tắt hiệu ứng ở chân trang là toàn bộ tệp này đứng yên.

   Cổng ra bên ngoài (các tệp khác gọi vào):
     PMChuyenDong.bat / .tat / .dangTat
     PMToast(nội dung, loại)         loại: 'ok' | 'loi' | rỗng
     PMPhaoGiay(số lượng)
     PMKhoiTaoHieuUng(gốc)           gọi lại sau khi thay nội dung động
     PMTienTrinh.batDau / .ketThuc
   ===================================================================== */

(function () {
    'use strict';

    var KHOA_CD = 'pm-chuyen-dong';

    function tatChuyenDong() {
        try {
            if (localStorage.getItem(KHOA_CD) === 'tat') return true;
        } catch (e) { }
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function apDungTrangThai() {
        document.body.classList.toggle('tat-chuyen-dong', tatChuyenDong());
    }

    window.PMChuyenDong = {
        dangTat: tatChuyenDong,
        bat: function () {
            try { localStorage.setItem(KHOA_CD, 'bat'); } catch (e) { }
            apDungTrangThai();
        },
        tat: function () {
            try { localStorage.setItem(KHOA_CD, 'tat'); } catch (e) { }
            apDungTrangThai();
            // Hiện ngay mọi phần tử đang chờ hiệu ứng, tránh để trang trắng.
            document.querySelectorAll('.hien-dan').forEach(function (o) { o.classList.add('da-hien'); });
        }
    };

    /* ===================== 1. THÔNG BÁO NỔI ========================= */

    function khay() {
        var k = document.getElementById('khayToast');
        if (!k) {
            k = document.createElement('div');
            k.id = 'khayToast';
            k.className = 'khay-toast';
            document.body.appendChild(k);
        }
        return k;
    }

    window.PMToast = function (noiDung, loai) {
        var t = document.createElement('div');
        t.className = 'toast ' + (loai || '');
        t.setAttribute('role', 'status');

        var bt = document.createElement('span');
        bt.textContent = loai === 'loi' ? '⚠' : loai === 'ok' ? '✓' : 'ℹ';
        var chu = document.createElement('span');
        chu.textContent = noiDung;          // dùng textContent để không chèn được HTML lạ
        chu.style.marginLeft = '8px';

        t.appendChild(bt);
        t.appendChild(chu);
        khay().appendChild(t);

        setTimeout(function () {
            t.classList.add('dang-tat');
            setTimeout(function () { t.remove(); }, 300);
        }, 3600);
    };

    /* ================== 2. SỐ ĐẾM TĂNG DẦN ========================= */

    function chayDem(o) {
        if (o.dataset.daDem === '1') return;
        o.dataset.daDem = '1';

        var dich = parseFloat(o.getAttribute('data-dem'));
        if (isNaN(dich)) return;

        var soLe = parseInt(o.getAttribute('data-le') || '0', 10);
        var duoi = o.getAttribute('data-duoi') || '';
        // Chữ do máy chủ định dạng sẵn ("0,05 tr €", "1,25 tỷ €"). Hiệu ứng chỉ chạy các khung
        // trung gian; khung cuối trả lại đúng chuỗi này để con số không bị làm tròn khác đi.
        var chuGoc = o.textContent.trim();

        function ve(gt) {
            o.textContent = gt.toLocaleString('vi-VN', {
                minimumFractionDigits: soLe, maximumFractionDigits: soLe
            }) + duoi;
        }

        if (tatChuyenDong()) { if (!chuGoc) ve(dich); return; }

        var batDau = performance.now(), thoiGian = 1000;

        function buoc(hienTai) {
            var ty = Math.min((hienTai - batDau) / thoiGian, 1);
            if (ty < 1) {
                ve(dich * (1 - Math.pow(1 - ty, 3)));   // ease-out cubic
                requestAnimationFrame(buoc);
            } else if (chuGoc) {
                o.textContent = chuGoc;
            } else {
                ve(dich);
            }
        }

        requestAnimationFrame(buoc);
    }

    /* ============ 3. HIỆN DẦN KHI CUỘN + BIỂU ĐỒ VẼ DẦN ============ */

    var theoDoi = null;

    function kichHoat(o) {
        o.classList.add('da-hien');

        if (o.hasAttribute('data-dem')) chayDem(o);
        if (o.classList.contains('thanh-day') || o.classList.contains('dau-cs')) {
            var r = o.getAttribute('data-rong');
            if (r !== null) o.style.width = r + '%';
        }
        if (o.classList.contains('duong-ve') || o.classList.contains('cot-ve')) o.classList.add('da-ve');

        if (o.querySelectorAll) {
            o.querySelectorAll('[data-dem]').forEach(chayDem);
            o.querySelectorAll('.duong-ve, .cot-ve').forEach(function (x) { x.classList.add('da-ve'); });
        }
    }

    function khoiTaoHienDan(goc) {
        goc = goc || document;

        var dsHien = goc.querySelectorAll('.hien-dan:not(.da-hien)');
        var dsDem = goc.querySelectorAll('[data-dem]:not([data-da-dem])');
        var dsThanh = goc.querySelectorAll('[data-rong]');
        var dsVe = goc.querySelectorAll('.duong-ve:not(.da-ve), .cot-ve:not(.da-ve)');

        if (tatChuyenDong()) {
            [dsHien, dsDem, dsThanh, dsVe].forEach(function (ds) { ds.forEach(kichHoat); });
            return;
        }

        if (!theoDoi) {
            theoDoi = new IntersectionObserver(function (muc) {
                muc.forEach(function (m) {
                    if (!m.isIntersecting) return;
                    kichHoat(m.target);
                    theoDoi.unobserve(m.target);
                });
            }, { threshold: .1, rootMargin: '0px 0px -50px 0px' });
        }

        // Lệch pha theo thứ tự để nội dung chảy vào lần lượt thay vì bật cùng lúc.
        dsHien.forEach(function (o, i) {
            o.style.setProperty('--tre', ((i % 6) * 60) + 'ms');
            theoDoi.observe(o);
        });

        [dsDem, dsThanh, dsVe].forEach(function (ds) {
            ds.forEach(function (o) { theoDoi.observe(o); });
        });
    }

    /* ============== 4. THẺ NGHIÊNG 3D THEO CON TRỎ ================== */

    function khoiTaoThe3D(goc) {
        if (tatChuyenDong()) return;
        if (window.matchMedia('(hover: none)').matches) return;     // bỏ qua trên cảm ứng

        (goc || document).querySelectorAll('.the-khung-3d:not([data-3d])').forEach(function (khung) {
            khung.setAttribute('data-3d', '1');
            var the = khung.querySelector('.the-3d');
            if (!the) return;

            khung.addEventListener('pointermove', function (e) {
                var o = khung.getBoundingClientRect();
                var x = (e.clientX - o.left) / o.width;
                var y = (e.clientY - o.top) / o.height;

                the.style.setProperty('--chuot-x', (x * 100).toFixed(1) + '%');
                the.style.setProperty('--chuot-y', (y * 100).toFixed(1) + '%');
                the.style.setProperty('--nghieng-y', ((x - .5) * 9).toFixed(2) + 'deg');
                the.style.setProperty('--nghieng-x', ((.5 - y) * 9).toFixed(2) + 'deg');
            });

            khung.addEventListener('pointerleave', function () {
                the.style.setProperty('--nghieng-x', '0deg');
                the.style.setProperty('--nghieng-y', '0deg');
            });
        });
    }

    /* ================= 5. THANH TIẾN TRÌNH TRANG =================== */

    var thanh = null, henTT = null;

    function thanhTT() {
        if (!thanh) {
            thanh = document.createElement('div');
            thanh.className = 'thanh-tien-trinh';
            document.body.appendChild(thanh);
        }
        return thanh;
    }

    window.PMTienTrinh = {
        batDau: function () {
            if (tatChuyenDong()) return;
            var t = thanhTT();
            clearTimeout(henTT);
            t.classList.remove('xong');
            t.style.width = '0';
            requestAnimationFrame(function () { t.style.width = '72%'; });
        },
        ketThuc: function () {
            if (!thanh) return;
            thanh.style.width = '100%';
            henTT = setTimeout(function () {
                thanh.classList.add('xong');
                setTimeout(function () { if (thanh) thanh.style.width = '0'; }, 320);
            }, 180);
        }
    };

    // Nối vào HTMX và vào thao tác rời trang thường.
    document.addEventListener('htmx:beforeRequest', function () { PMTienTrinh.batDau(); });
    document.addEventListener('htmx:afterRequest', function () { PMTienTrinh.ketThuc(); });

    window.addEventListener('beforeunload', function () { PMTienTrinh.batDau(); });
    window.addEventListener('pageshow', function () { PMTienTrinh.ketThuc(); });

    /* ==================== 6. CHẾ ĐỘ TRÌNH CHIẾU ==================== */
    /* Dành cho lúc bảo vệ: phóng to chữ, ẩn thanh điều hướng và giỏ,
       chỉ để lại nội dung chính cho người ngồi cuối phòng đọc được. */

    var KHOA_TC = 'pm-trinh-chieu';

    function datTrinhChieu(bat) {
        document.body.classList.toggle('trinh-chieu', bat);
        try { localStorage.setItem(KHOA_TC, bat ? '1' : '0'); } catch (e) { }

        var nut = document.getElementById('nutTrinhChieu');
        if (nut) nut.setAttribute('aria-pressed', bat ? 'true' : 'false');

        if (bat && !document.querySelector('.huy-trinh-chieu')) {
            var h = document.createElement('button');
            h.type = 'button';
            h.className = 'nut nut-nho huy-trinh-chieu';
            h.textContent = 'Thoát trình chiếu (Esc)';
            h.addEventListener('click', function () { datTrinhChieu(false); });
            document.body.appendChild(h);
        }
    }

    window.PMTrinhChieu = {
        bat: function () { datTrinhChieu(true); },
        tat: function () { datTrinhChieu(false); },
        dao: function () { datTrinhChieu(!document.body.classList.contains('trinh-chieu')); }
    };

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && document.body.classList.contains('trinh-chieu')) {
            datTrinhChieu(false);
            return;
        }

        // Phím P bật/tắt, nhưng không cướp phím khi người dùng đang gõ.
        if ((e.key === 'p' || e.key === 'P') && !e.ctrlKey && !e.metaKey && !e.altKey) {
            var o = document.activeElement;
            if (o && /^(INPUT|TEXTAREA|SELECT)$/.test(o.tagName)) return;
            if (o && o.isContentEditable) return;
            e.preventDefault();
            PMTrinhChieu.dao();
        }
    });

    /* ======================= 7. PHÁO GIẤY ========================== */

    window.PMPhaoGiay = function (soLuong) {
        if (tatChuyenDong()) return;

        var doc = getComputedStyle(document.documentElement);
        var mau = [
            doc.getPropertyValue('--chinh').trim() || '#0f7a3d',
            doc.getPropertyValue('--nhan').trim() || '#b7d900',
            '#ffffff', '#f59e0b'
        ];

        for (var i = 0; i < (soLuong || 90); i++) {
            var m = document.createElement('div');
            m.className = 'phao';
            m.style.left = Math.random() * 100 + 'vw';
            m.style.background = mau[Math.floor(Math.random() * mau.length)];
            m.style.animationDuration = (1.8 + Math.random() * 1.4) + 's';
            m.style.animationDelay = (Math.random() * .5) + 's';
            m.style.opacity = .55 + Math.random() * .45;
            document.body.appendChild(m);

            (function (phan) { setTimeout(function () { phan.remove(); }, 4200); })(m);
        }
    };

    /* ================== 8. KHỞI TẠO VÀ TÁI KHỞI TẠO ================ */

    function khoiTaoTatCa(goc) {
        khoiTaoHienDan(goc);
        khoiTaoThe3D(goc);
    }

    function batDau() {
        document.documentElement.classList.remove('khong-js');
        apDungTrangThai();

        // Nối ngay tại đây thay vì trong _Layout, để nút vẫn chạy ở mọi trang
        // kể cả khi trang đó không kèm kịch bản riêng.
        var nutTC = document.getElementById('nutTrinhChieu');
        if (nutTC && !nutTC.dataset.daNoi) {
            nutTC.dataset.daNoi = '1';
            nutTC.addEventListener('click', function () { PMTrinhChieu.dao(); });
        }

        try { if (localStorage.getItem(KHOA_TC) === '1') datTrinhChieu(true); } catch (e) { }

        khoiTaoTatCa(document);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', batDau);
    } else {
        batDau();
    }

    document.addEventListener('htmx:afterSwap', function (e) { khoiTaoTatCa(e.target); });

    window.PMKhoiTaoHieuUng = khoiTaoTatCa;
})();
