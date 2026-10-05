/* B1 — Bảng lệnh Ctrl+K: tìm cầu thủ và điều hướng bằng bàn phím. */

(function () {
    'use strict';

    var LENH_TRANG = [
        { ten: 'Trang chủ', duong: '/', bt: '🏠', nhom: 'Điều hướng' },
        { ten: 'Khám phá cầu thủ', duong: '/Players', bt: '🔎', nhom: 'Điều hướng' },
        { ten: 'Mô hình định giá', duong: '/Analytics', bt: '📈', nhom: 'Phân tích' },
        { ten: 'Hỏi bằng câu tiếng Việt', duong: '/Analytics/Hoi', bt: '💬', nhom: 'Phân tích' },
        { ten: 'Mạng lưới chuyển nhượng', duong: '/Analytics/Mang', bt: '🕸️', nhom: 'Phân tích' },
        { ten: 'Cân đối câu lạc bộ', duong: '/Analytics/CauLacBo', bt: '💰', nhom: 'Phân tích' },
        { ten: 'Biến động giá', duong: '/Analytics/BienDong', bt: '📉', nhom: 'Phân tích' },
        { ten: 'Đối chiếu hai nguồn định giá', duong: '/Analytics/DoiChieu', bt: '🔬', nhom: 'Phân tích' },
        { ten: 'Kiểm chứng dữ liệu', duong: '/Analytics/KiemChung', bt: '🧪', nhom: 'Phân tích' },
        { ten: 'So sánh cầu thủ', duong: '/Compare', bt: '⚖️', nhom: 'Điều hướng' },
        { ten: 'Thống kê thị trường', duong: '/Statistics', bt: '📊', nhom: 'Điều hướng' },
        { ten: 'Chế độ tuyển trạch', duong: '/Scouting', bt: '🔭', nhom: 'Điều hướng' },
        { ten: 'Trò chơi đoán giá', duong: '/Game', bt: '🎯', nhom: 'Điều hướng' },
        { ten: 'So sánh hai đội hình', duong: '/Squad/SoSanh', bt: '⚔️', nhom: 'Điều hướng' },
        { ten: 'Đội hình ảo', duong: '/Squad', bt: '🧩', nhom: 'Điều hướng' },
        { ten: 'Danh sách theo dõi', duong: '/Watchlist', bt: '★', nhom: 'Điều hướng' },
        { ten: 'Cầu thủ đắt giá nhất', duong: '/Players?SapXep=giatri_giam', bt: '💎', nhom: 'Lối tắt' },
        { ten: 'Tài năng trẻ dưới 22 tuổi', duong: '/Players?TuoiDen=22&SapXep=giatri_giam', bt: '🌱', nhom: 'Lối tắt' },
        { ten: 'Sắp hết hợp đồng', duong: '/Players?SapHetHopDong=true', bt: '⏳', nhom: 'Lối tắt' },
        { ten: 'Cầu thủ tự do', duong: '/Players?CauThuTuDo=true', bt: '🆓', nhom: 'Lối tắt' }
    ];

    /* Lệnh thao tác: chạy hàm tại chỗ thay vì mở trang khác. */
    var LENH_THAO_TAC = [
        {
            ten: 'Đổi giao diện sáng / tối', bt: '◐', nhom: 'Lệnh',
            chay: function () { var n = document.getElementById('nutGiaoDien'); if (n) n.click(); }
        },
        {
            ten: 'Bật chế độ trình chiếu (phím P)', bt: '🖵', nhom: 'Lệnh',
            chay: function () { window.PMTrinhChieu && PMTrinhChieu.dao(); }
        },
        {
            ten: 'Bật / tắt hiệu ứng chuyển động', bt: '✦', nhom: 'Lệnh',
            chay: function () {
                if (!window.PMChuyenDong) return;
                PMChuyenDong.dangTat() ? PMChuyenDong.bat() : PMChuyenDong.tat();
                window.PMToast && PMToast(PMChuyenDong.dangTat() ? 'Đã tắt hiệu ứng.' : 'Đã bật hiệu ứng.', 'ok');
            }
        },
        {
            ten: 'Lên đầu trang', bt: '↑', nhom: 'Lệnh',
            chay: function () { window.scrollTo({ top: 0, behavior: 'smooth' }); }
        }
    ];

    /* Lịch sử: bốn mục mở gần đây nhất, hiện ngay khi vừa bật bảng lệnh. */
    var KHOA_LS = 'pm-lenh-gan-day';

    function docLichSu() {
        try { return JSON.parse(localStorage.getItem(KHOA_LS) || '[]'); } catch (e) { return []; }
    }

    function ghiLichSu(m) {
        if (!m || !m.duong) return;
        try {
            var ds = docLichSu().filter(function (x) { return x.duong !== m.duong; });
            ds.unshift({ ten: m.ten, duong: m.duong, bt: m.bt, nhom: 'Gần đây' });
            localStorage.setItem(KHOA_LS, JSON.stringify(ds.slice(0, 4)));
        } catch (e) { }
    }

    function danhSachGoc() {
        return docLichSu().concat(LENH_TRANG).concat(LENH_THAO_TAC);
    }

    var nen, oNhap, dsMuc, muc = [], viTri = 0, hen = null;

    function dung() {
        nen = document.getElementById('bangLenhNen');
        if (!nen) return false;
        oNhap = document.getElementById('oBangLenh');
        dsMuc = document.getElementById('dsBangLenh');
        return true;
    }

    function mo() {
        if (!nen) return;
        nen.classList.add('mo-ra');
        oNhap.value = '';
        veDanhSach(danhSachGoc());
        setTimeout(function () { oNhap.focus(); }, 40);
    }

    function dong() {
        if (nen) nen.classList.remove('mo-ra');
    }

    function veDanhSach(ds) {
        muc = ds;
        viTri = 0;

        if (!ds.length) {
            dsMuc.innerHTML = '<div class="lenh-nhom">Không có kết quả</div>';
            return;
        }

        var html = '';
        var nhomTruoc = null;

        ds.forEach(function (m, i) {
            if (m.nhom !== nhomTruoc) {
                html += '<div class="lenh-nhom">' + m.nhom + '</div>';
                nhomTruoc = m.nhom;
            }
            html += '<div class="lenh-muc' + (i === 0 ? ' chon' : '') + '" data-i="' + i + '" data-duong="' + m.duong + '">' +
                '<span>' + (m.bt || '•') + '</span><span>' + m.ten + '</span>' +
                (m.phu ? '<span class="mo">' + m.phu + '</span>' : '') +
                (i === 0 ? '<span class="phim">Enter</span>' : '') + '</div>';
        });

        dsMuc.innerHTML = html;
    }

    function danhDau() {
        Array.prototype.forEach.call(dsMuc.querySelectorAll('.lenh-muc'), function (o) {
            var chon = parseInt(o.getAttribute('data-i'), 10) === viTri;
            o.classList.toggle('chon', chon);
            if (chon) o.scrollIntoView({ block: 'nearest' });
        });
    }

    function diToi(i) {
        var m = muc[i];
        if (!m) return;

        if (typeof m.chay === 'function') { dong(); m.chay(); return; }
        if (!m.duong) return;

        ghiLichSu(m);
        window.PMTienTrinh && PMTienTrinh.batDau();
        window.location.href = m.duong;
    }

    function timKiem(tuKhoa) {
        var khoa = boDau(tuKhoa);

        var trang = LENH_TRANG.concat(LENH_THAO_TAC).filter(function (m) {
            return boDau(m.ten).indexOf(khoa) !== -1;
        });

        if (khoa.length < 2) { veDanhSach(danhSachGoc()); return; }

        fetch('/Home/GoiY?tuKhoa=' + encodeURIComponent(tuKhoa))
            .then(function (r) { return r.json(); })
            .then(function (ds) {
                var cauThu = (ds || []).map(function (x) {
                    return {
                        ten: x.hoTen,
                        phu: x.cauLacBo + ' · ' + window.PMGia(x.giaTriHienTai),
                        duong: '/Players/ChiTiet/' + x.maCauThu,
                        bt: '👤',
                        nhom: 'Cầu thủ'
                    };
                });
                veDanhSach(cauThu.concat(trang));
            })
            .catch(function () { veDanhSach(trang); });
    }

    function boDau(chuoi) {
        return (chuoi || '').normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd').toLowerCase().trim();
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (!dung()) return;

        var nutMo = document.getElementById('nutBangLenh');
        if (nutMo) nutMo.addEventListener('click', mo);

        oNhap.addEventListener('input', function () {
            clearTimeout(hen);
            var giaTri = oNhap.value.trim();
            hen = setTimeout(function () { timKiem(giaTri); }, 220);
        });

        dsMuc.addEventListener('click', function (e) {
            var m = e.target.closest('.lenh-muc');
            if (m) diToi(parseInt(m.getAttribute('data-i'), 10));
        });

        nen.addEventListener('click', function (e) { if (e.target === nen) dong(); });
    });

    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            nen && nen.classList.contains('mo-ra') ? dong() : mo();
            return;
        }

        if (!nen || !nen.classList.contains('mo-ra')) return;

        if (e.key === 'Escape') { e.preventDefault(); dong(); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); viTri = Math.min(viTri + 1, muc.length - 1); danhDau(); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); viTri = Math.max(viTri - 1, 0); danhDau(); }
        else if (e.key === 'Enter') { e.preventDefault(); diToi(viTri); }
    });
})();
