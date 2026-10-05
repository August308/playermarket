/* Đọc màu từ biến CSS lúc chạy, để biểu đồ và SVG luôn khớp chế độ sáng/tối
   hiện hành thay vì dán cứng mã màu. Trả về rgba khi truyền độ mờ. */
window.PMMau = function (bien, doMo) {
    var v = getComputedStyle(document.documentElement).getPropertyValue(bien).trim();
    if (!v) return doMo == null ? '#64809a' : 'rgba(100,128,154,' + doMo + ')';
    if (doMo == null) return v;

    if (v.charAt(0) === '#') {
        var h = v.slice(1);
        if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
        var n = parseInt(h, 16);
        return 'rgba(' + ((n >> 16) & 255) + ',' + ((n >> 8) & 255) + ',' + (n & 255) + ',' + doMo + ')';
    }
    return v;
};

/* Định dạng giá trị thị trường (triệu euro) GIỐNG HỆT phía máy chủ (DinhDangSo.TrieuEuro):
   dấu phẩy thập phân, bỏ số 0 thừa, giá trị dưới 1 giữ 2 chữ số. Trước đây dùng toFixed(0)
   nên cầu thủ 0,3 tr € hiện thành "0 tr €". */
window.PMSo = function (giaTri, soLeToiDa) {
    var so = Number(giaTri) || 0;
    var le = soLeToiDa == null ? 1 : soLeToiDa;
    if (Math.abs(so) < 1) le += 1;
    var he = Math.pow(10, le);
    so = Math.round((Math.abs(so) + 1e-9) * he) / he * (so < 0 ? -1 : 1);

    var phan = String(Math.abs(so)).split('.');
    var nguyen = phan[0].replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    return (so < 0 ? '-' : '') + nguyen + (phan[1] ? ',' + phan[1] : '');
};

window.PMGia = function (trieuEuro) {
    var so = Number(trieuEuro) || 0;
    if (Math.abs(so) >= 1000) return window.PMSo(so / 1000, 2) + ' tỷ €';
    return window.PMSo(so, 1) + ' tr €';
};

/* Biểu đồ Chart.js dùng chung.
   PMBieuDo(idCanvas, taoCauHinh): vẽ biểu đồ từ hàm taoCauHinh() và TỰ VẼ LẠI khi người dùng đổi
   sáng/tối. Vì cấu hình được dựng lại mỗi lần, mọi màu đọc bằng PMMau() luôn khớp giao diện hiện hành.
   Trước đây các trang thống kê, so sánh, dự báo dán cứng mã màu của giao diện cũ và chỉ đọc
   chế độ sáng/tối đúng một lần lúc tải trang. */
window.PMBangMau = function (i, doMo) {
    return window.PMMau('--bieu-' + ((i % 8) + 1), doMo);
};

window.PMBieuDo = function (idCanvas, taoCauHinh) {
    var bieuDo = null;

    function ve() {
        var o = document.getElementById(idCanvas);
        if (!o || typeof Chart === 'undefined') return;

        // Mặc định chung: chữ, lưới, phông và kiểu số Việt Nam (0,4 thay vì 0.4).
        Chart.defaults.color = window.PMMau('--chu-2');
        Chart.defaults.borderColor = window.PMMau('--vien');
        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        Chart.defaults.locale = 'vi-VN';

        if (bieuDo) bieuDo.destroy();
        bieuDo = new Chart(o, taoCauHinh());
    }

    ve();
    document.addEventListener('pmDoiGiaoDien', ve);
    return { veLai: ve };
};

/* Mã chống giả mạo yêu cầu (CSRF): máy chủ từ chối mọi lệnh POST không kèm mã này. */
window.PMCsrf = function () {
    var the = document.querySelector('meta[name="pm-csrf"]');
    return the ? the.getAttribute('content') : '';
};

/* HTMX: gắn mã vào mọi yêu cầu không phải GET. */
document.addEventListener('htmx:configRequest', function (e) {
    if ((e.detail.verb || '').toLowerCase() !== 'get') e.detail.headers['RequestVerificationToken'] = window.PMCsrf();
});

/* PlayerMarket — kịch bản giao diện: đổi chế độ sáng/tối, gợi ý tìm kiếm, bộ lọc tức thì. */

(function () {
    'use strict';

    var KHOA_GIAO_DIEN = 'pm-giao-dien';

    function apDungGiaoDien(giaoDien) {
        document.documentElement.setAttribute('data-theme', giaoDien);
        var nut = document.getElementById('nutGiaoDien');
        if (nut) {
            nut.textContent = giaoDien === 'dark' ? '☀' : '🌙';
            nut.title = giaoDien === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối';
        }
    }

    // Áp dụng sớm để tránh nháy màu khi tải trang.
    try {
        apDungGiaoDien(localStorage.getItem(KHOA_GIAO_DIEN) || 'light');
    } catch (e) {
        apDungGiaoDien('light');
    }

    /* Ảnh cầu thủ lấy từ máy chủ ngoài (Transfermarkt) và cờ từ flagcdn có thể bị chặn hoặc lỗi mạng.
       Sự kiện 'error' của ảnh không nổi bọt nên phải bắt ở pha capture. Đăng ký ngay trong <head>,
       trước khi trình duyệt đọc tới thẻ <img> đầu tiên. */
    document.addEventListener('error', function (e) {
        var anh = e.target;
        if (!anh || anh.tagName !== 'IMG') return;

        if (anh.hasAttribute('data-chu')) {
            var mau = anh.getAttribute('data-mau') || '#0f7a3d';
            var o = document.createElement('span');
            o.className = anh.className + ' anh-thay-the';
            o.setAttribute('style', (anh.getAttribute('style') || '') +
                ';background:linear-gradient(140deg,' + mau + ',color-mix(in srgb,' + mau + ' 55%,#000))');
            o.textContent = anh.getAttribute('data-chu');
            if (anh.alt) o.setAttribute('aria-label', anh.alt);
            anh.replaceWith(o);
        } else if (anh.classList.contains('co')) {
            anh.style.display = 'none';      // cờ quốc gia: ẩn hẳn, tên nước vẫn hiện bên cạnh
        }
    }, true);

    /* Thao tác không hoàn tác được (xóa, đăng xuất mọi thiết bị) phải hỏi lại. Lời nhắc nằm trong
       thuộc tính data-xac-nhan nên tên cầu thủ có dấu nháy (O'Riley) không làm hỏng đoạn mã. */
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var loiNhac = form && form.getAttribute && form.getAttribute('data-xac-nhan');
        if (loiNhac && !window.confirm(loiNhac)) {
            e.preventDefault();
            e.stopPropagation();
        }
    }, true);

    function khoiTaoMenuDiDong() {
        var nut = document.getElementById('nutMenu');
        var menu = document.getElementById('menuChinh');
        if (!nut || !menu) return;

        function dat(mo) {
            menu.classList.toggle('mo', mo);
            nut.setAttribute('aria-expanded', mo ? 'true' : 'false');
            nut.setAttribute('aria-label', mo ? 'Đóng menu' : 'Mở menu');
            nut.textContent = mo ? '✕' : '☰';
        }

        nut.addEventListener('click', function () { dat(!menu.classList.contains('mo')); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') dat(false); });
    }

    document.addEventListener('DOMContentLoaded', function () {
        khoiTaoMenuDiDong();
        khoiTaoGiaoDien();
        khoiTaoGoiY();
        khoiTaoBoLoc();
        khoiTaoNganKeo();
        napHuyHieuCanhBao();
    });

    /* ---------------- Huy hiệu cảnh báo giá ---------------- */
    function napHuyHieuCanhBao() {
        if (document.body.getAttribute('data-dang-nhap') !== '1') return;

        var huyHieu = document.getElementById('huyHieuCanhBao');
        if (!huyHieu) return;

        fetch('/Watchlist/SoCanhBao')
            .then(function (r) { return r.json(); })
            .then(function (kq) {
                if (kq && kq.so > 0) {
                    huyHieu.textContent = kq.so;
                    huyHieu.title = kq.so + ' cầu thủ đã chạm ngưỡng cảnh báo giá';
                    huyHieu.style.display = '';
                }
            })
            .catch(function () { });
    }

    /* ---------------- Đổi chế độ sáng/tối ---------------- */
    function khoiTaoGiaoDien() {
        var nut = document.getElementById('nutGiaoDien');
        if (!nut) return;

        apDungGiaoDien(document.documentElement.getAttribute('data-theme') || 'light');

        nut.addEventListener('click', function () {
            var moi = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            apDungGiaoDien(moi);
            try { localStorage.setItem(KHOA_GIAO_DIEN, moi); } catch (e) { }
            // Báo cho biểu đồ và các thành phần vẽ bằng màu đọc từ CSS để vẽ lại.
            document.dispatchEvent(new CustomEvent('pmDoiGiaoDien', { detail: { giaoDien: moi } }));
        });
    }

    /* ---------------- Gợi ý tìm kiếm ---------------- */
    function khoiTaoGoiY() {
        var o = document.getElementById('oTimKiem');
        var hop = document.getElementById('hopGoiY');
        if (!o || !hop) return;

        var hen = null;

        o.addEventListener('input', function () {
            clearTimeout(hen);
            var tuKhoa = o.value.trim();

            if (tuKhoa.length < 2) { hop.style.display = 'none'; return; }

            hen = setTimeout(function () {
                fetch('/Home/GoiY?tuKhoa=' + encodeURIComponent(tuKhoa))
                    .then(function (r) { return r.json(); })
                    .then(function (ds) {
                        if (!ds || !ds.length) { hop.style.display = 'none'; return; }

                        hop.innerHTML = ds.map(function (x) {
                            var chuCai = (x.hoTen || '?').split(' ').slice(0, 2)
                                .map(function (t) { return t[0]; }).join('').toUpperCase();
                            var mau = mauTheoTen(x.hoTen);

                            var anh = x.anhDaiDien
                                ? '<img class="anh-cau-thu anh-nho" style="--co:32px" loading="lazy" alt="" src="' +
                                  thoat(x.anhDaiDien) + '" data-chu="' + thoat(chuCai) + '" data-mau="' + mau + '">'
                                : '<span class="anh-cau-thu anh-nho anh-thay-the" style="--co:32px;background:' + mau + '">' +
                                  thoat(chuCai) + '</span>';

                            return '<a href="/Players/ChiTiet/' + encodeURIComponent(x.maCauThu) + '">' + anh +
                                '<span style="flex:1;min-width:0"><span class="ten">' + thoat(x.hoTen) + '</span>' +
                                '<span class="mo" style="display:block">' + thoat(x.cauLacBo || '') + '</span></span>' +
                                '<span class="nhan nhan-vi-tri">' + thoat(x.viTriVietTat || '') + '</span>' +
                                '<span class="gia-chu" style="font-size:13px">' + window.PMGia(x.giaTriHienTai) + '</span></a>';
                        }).join('');

                        hop.style.display = 'block';
                    })
                    .catch(function () { hop.style.display = 'none'; });
            }, 300);
        });

        document.addEventListener('click', function (e) {
            if (!o.contains(e.target) && !hop.contains(e.target)) hop.style.display = 'none';
        });

        o.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') hop.style.display = 'none';
        });
    }

    function mauTheoTen(ten) {
        // Tám ô màu phân loại, lấy theo thứ tự cố định từ biến CSS nên tự
        // đổi theo chế độ sáng/tối mà vẫn giữ đúng bộ đã kiểm định.
        var bang = [PMMau('--bieu-1'), PMMau('--bieu-2'), PMMau('--bieu-3'), PMMau('--bieu-4'),
                    PMMau('--bieu-5'), PMMau('--bieu-6'), PMMau('--bieu-7'), PMMau('--bieu-8')];
        var tong = 0;
        for (var i = 0; i < (ten || '').length; i++) tong += ten.charCodeAt(i);
        return bang[Math.abs(tong) % bang.length];
    }

    function thoat(chuoi) {
        return String(chuoi || '').replace(/[&<>"']/g, function (k) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[k];
        });
    }

    /* ---------------- Bộ lọc tức thì ---------------- */
    function khoiTaoBoLoc() {
        var form = document.getElementById('formLoc');
        var vung = document.getElementById('vungKetQua');
        if (!form || !vung) return;

        var hen = null;
        var lanGoi = 0;

        function napKetQua(trang) {
            var oTrang = form.querySelector('input[name="Trang"]');
            if (oTrang) oTrang.value = trang || 1;

            var duLieu = new URLSearchParams(new FormData(form)).toString();
            var lan = ++lanGoi;

            veKhungXuong();

            fetch('/Players/LuoiKetQua?' + duLieu)
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    if (lan !== lanGoi) return;      // bỏ qua phản hồi cũ đến muộn
                    vung.innerHTML = html;
                    if (window.PMKhoiTaoHieuUng) window.PMKhoiTaoHieuUng(vung);
                    history.replaceState(null, '', '/Players?' + duLieu);
                })
                .catch(function () {
                    vung.innerHTML = '<div class="khoi rong">' +
                        '<div class="rong-bieu-tuong">⚠</div><p>Không tải được kết quả, vui lòng thử lại.</p></div>';
                });
        }

        // A4 — khung xương hiện ngay để che thời gian chờ
        function veKhungXuong() {
            var the = '<div class="xuong-the">' +
                '<div style="display:flex;gap:12px">' +
                '<div class="xuong xuong-tron"></div>' +
                '<div style="flex:1;display:flex;flex-direction:column;gap:8px">' +
                '<div class="xuong xuong-dong"></div><div class="xuong xuong-dong ngan"></div></div></div>' +
                '<div class="xuong xuong-dong" style="height:20px;width:60%;margin-top:auto"></div>' +
                '<div class="xuong xuong-dong" style="height:31px"></div></div>';

            vung.innerHTML = '<div class="khung-xuong" aria-hidden="true">' + the.repeat(8) + '</div>';
        }

        function hoanNap(trang) {
            clearTimeout(hen);
            hen = setTimeout(function () { napKetQua(trang); }, 220);
        }

        // Chip bấm là chọn
        form.addEventListener('click', function (e) {
            var chip = e.target.closest('.chip');
            if (chip && form.contains(chip)) {
                var o = chip.querySelector('input');
                if (o) {
                    o.checked = !o.checked;
                    chip.classList.toggle('chon', o.checked);
                    hoanNap(1);
                }
                return;
            }

            var nutTrang = e.target.closest('[data-trang]');
            if (nutTrang) {
                e.preventDefault();
                napKetQua(parseInt(nutTrang.getAttribute('data-trang'), 10));
                window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        });

        // Ô nhập, ô chọn, hộp tích
        form.addEventListener('change', function (e) {
            if (e.target.closest('.chip')) return;
            hoanNap(1);
        });

        form.addEventListener('input', function (e) {
            if (e.target.type === 'range' || e.target.classList.contains('o-tim-trong-nhom')) return;
            hoanNap(1);
        });

        // Phân trang nằm ngoài form
        vung.addEventListener('click', function (e) {
            var nutTrang = e.target.closest('[data-trang]');
            if (!nutTrang) return;
            e.preventDefault();
            napKetQua(parseInt(nutTrang.getAttribute('data-trang'), 10));
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });

        // Ô tìm nhanh bên trong nhóm câu lạc bộ / quốc tịch
        Array.prototype.forEach.call(form.querySelectorAll('.o-tim-trong-nhom'), function (o) {
            o.addEventListener('input', function () {
                var tuKhoa = boDau(o.value);
                var hop = document.getElementById(o.getAttribute('data-hop'));
                if (!hop) return;

                Array.prototype.forEach.call(hop.querySelectorAll('.dong-chon'), function (dong) {
                    var ten = boDau(dong.getAttribute('data-ten') || '');
                    dong.classList.toggle('an', tuKhoa.length > 0 && ten.indexOf(tuKhoa) === -1);
                });
            });
        });

        // Thanh trượt hai đầu
        Array.prototype.forEach.call(form.querySelectorAll('.truot-cap'), function (cap) {
            var thap = cap.querySelector('.truot-thap');
            var cao = cap.querySelector('.truot-cao');
            var phan = cap.querySelector('.truot-phan');
            var nhan = document.getElementById(cap.getAttribute('data-nhan'));
            var donVi = cap.getAttribute('data-don-vi') || '';

            function veLai() {
                var min = parseFloat(thap.min), max = parseFloat(thap.max);
                var a = parseFloat(thap.value), b = parseFloat(cao.value);

                if (a > b) { var t = a; a = b; b = t; thap.value = a; cao.value = b; }

                phan.style.left = ((a - min) / (max - min) * 100) + '%';
                phan.style.width = ((b - a) / (max - min) * 100) + '%';
                if (nhan) nhan.textContent = a + donVi + ' — ' + b + donVi;
            }

            thap.addEventListener('input', function () { veLai(); hoanNap(1); });
            cao.addEventListener('input', function () { veLai(); hoanNap(1); });
            veLai();
        });

        // Nút xóa bộ lọc
        var nutXoa = document.getElementById('nutXoaLoc');
        if (nutXoa) {
            nutXoa.addEventListener('click', function () {
                form.reset();
                Array.prototype.forEach.call(form.querySelectorAll('.chip'), function (c) {
                    c.classList.remove('chon');
                });
                Array.prototype.forEach.call(form.querySelectorAll('.truot-cap'), function (cap) {
                    var thap = cap.querySelector('.truot-thap');
                    var cao = cap.querySelector('.truot-cao');
                    thap.value = thap.min;
                    cao.value = cao.max;
                    thap.dispatchEvent(new Event('input'));
                });
                napKetQua(1);
            });
        }

        function boDau(chuoi) {
            return (chuoi || '').normalize('NFD').replace(/[̀-ͯ]/g, '')
                .replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
        }
    }

    /* ---------------- B3. Ngăn kéo giỏ so sánh ---------------- */
    function khoiTaoNganKeo() {
        var nen = document.getElementById('nganKeoNen');
        var keo = document.getElementById('nganKeo');
        var noiDung = document.getElementById('noiDungGio');
        if (!nen || !keo || !noiDung) return;

        function nap() {
            noiDung.innerHTML = '<div class="rong" style="padding:24px"><div class="xuong xuong-dong"></div></div>';
            fetch('/Compare/Gio')
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    noiDung.innerHTML = html;
                    if (window.htmx) window.htmx.process(noiDung);
                })
                .catch(function () { noiDung.innerHTML = '<p class="phu">Không tải được giỏ so sánh.</p>'; });
        }

        function mo() { nen.classList.add('mo'); nap(); }
        function dong() { nen.classList.remove('mo'); }

        var nutMo = document.getElementById('nutMoNganKeo');
        var nutDong = document.getElementById('nutDongNganKeo');

        if (nutMo) nutMo.addEventListener('click', mo);
        if (nutDong) nutDong.addEventListener('click', dong);

        nen.addEventListener('click', function (e) { if (e.target === nen) dong(); });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && nen.classList.contains('mo')) dong();
        });

        // Kéo thẻ cầu thủ thả vào ngăn kéo để thêm vào giỏ
        document.addEventListener('dragstart', function (e) {
            var the = e.target.closest('.the-khung-3d');
            if (!the) return;

            var o = the.querySelector('input[name="maCauThu"]');
            if (!o) return;

            e.dataTransfer.setData('text/plain', o.value);

            var ten = the.querySelector('.the-ten');
            if (ten) e.dataTransfer.setData('text/ten', ten.textContent.trim());

            e.dataTransfer.effectAllowed = 'copy';
            the.classList.add('dang-keo');

            // Chỉ mở ngăn kéo khi giỏ nổi chưa hiện, tránh che mất vùng thả.
            var gio = document.getElementById('gioSoSanh');
            if (!gio || gio.style.display === 'none') { nen.classList.add('mo'); nap(); }
        });

        document.addEventListener('dragend', function (e) {
            var the = e.target.closest('.the-khung-3d');
            if (the) the.classList.remove('dang-keo');
            keo.classList.remove('dang-tha');
        });

        keo.addEventListener('dragover', function (e) { e.preventDefault(); keo.classList.add('dang-tha'); });
        keo.addEventListener('dragleave', function () { keo.classList.remove('dang-tha'); });

        /* Gửi một cầu thủ vào giỏ so sánh và cập nhật số đếm trên giỏ nổi. */
        function themVaoGio(ma, tenHienThi) {
            if (!ma) return;

            var duLieu = new URLSearchParams();
            duLieu.append('maCauThu', ma);

            fetch('/Compare/Them', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'HX-Request': 'true',
                    'RequestVerificationToken': window.PMCsrf()
                },
                body: duLieu.toString()
            }).then(function (r) {
                if (!r.ok) throw new Error('loi');
                nap();
                capNhatSoGio(1);
                window.PMToast && PMToast('Đã thêm ' + (tenHienThi || 'cầu thủ') + ' vào giỏ so sánh.', 'ok');
            }).catch(function () {
                window.PMToast && PMToast('Không thêm được vào giỏ so sánh.', 'loi');
            });
        }

        /* Giỏ nổi hiện số lượng; cộng thêm tại chỗ để không phải tải lại trang. */
        function capNhatSoGio(them) {
            var gio = document.getElementById('gioSoSanh');
            var o = document.getElementById('soTrongGio');
            if (!o) return;

            var so = Math.min(parseInt(o.textContent, 10) + them, 4);
            o.textContent = so;
            o.classList.remove('vua-doi');
            void o.offsetWidth;                 // ép trình duyệt chạy lại hiệu ứng
            o.classList.add('vua-doi');

            if (gio && so > 0) gio.style.display = '';
        }

        keo.addEventListener('drop', function (e) {
            e.preventDefault();
            keo.classList.remove('dang-tha');
            themVaoGio(e.dataTransfer.getData('text/plain'), e.dataTransfer.getData('text/ten'));
        });

        /* Thả thẳng vào giỏ nổi — đường ngắn hơn, không cần mở ngăn kéo. */
        var gioNoi = document.getElementById('gioSoSanh');

        if (gioNoi) {
            gioNoi.addEventListener('dragover', function (e) {
                e.preventDefault();
                gioNoi.classList.add('dang-nhan');
            });

            gioNoi.addEventListener('dragleave', function (e) {
                if (!gioNoi.contains(e.relatedTarget)) gioNoi.classList.remove('dang-nhan');
            });

            gioNoi.addEventListener('drop', function (e) {
                e.preventDefault();
                gioNoi.classList.remove('dang-nhan');
                themVaoGio(e.dataTransfer.getData('text/plain'), e.dataTransfer.getData('text/ten'));
            });
        }
    }

    /* ---------------- Tiện ích dùng chung ---------------- */
    window.PM = {
        moHopThoai: function (ma) {
            var h = document.getElementById(ma);
            if (h) h.classList.add('mo');
        },
        dongHopThoai: function (ma) {
            var h = document.getElementById(ma);
            if (h) h.classList.remove('mo');
        },
        mauTheoTen: mauTheoTen
    };

    document.addEventListener('click', function (e) {
        if (e.target.classList && e.target.classList.contains('phu-nen')) {
            e.target.classList.remove('mo');
        }
    });
})();
