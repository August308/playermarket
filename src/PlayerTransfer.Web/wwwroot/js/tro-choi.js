/* C10 — trò chơi đoán giá cầu thủ. Chấm điểm ngay trên trình duyệt, gửi điểm lên máy chủ khi kết thúc. */

(function () {
    'use strict';

    var cauHoi = [], viTri = 0, tongDiem = 0, soDung = 0, tongLech = 0;

    function o(ma) { return document.getElementById(ma); }

    function mauTheoTen(ten) {
        return window.PM && window.PM.mauTheoTen ? window.PM.mauTheoTen(ten) : '#1d4ed8';
    }

    function chuCaiDau(ten) {
        return (ten || '?').split(' ').slice(0, 2).map(function (t) { return t[0]; }).join('').toUpperCase();
    }

    function batDau() {
        o('manBatDau').style.display = 'none';
        o('manKetThuc').style.display = 'none';
        o('manChoi').style.display = '';
        o('ketQuaCau').style.display = 'none';

        viTri = 0; tongDiem = 0; soDung = 0; tongLech = 0;
        o('diemHienTai').textContent = '0';

        fetch('/Game/CauHoi?soCau=5')
            .then(function (r) { return r.json(); })
            .then(function (ds) {
                if (!ds || !ds.length) {
                    if (window.PMToast) window.PMToast('Chưa có dữ liệu cầu thủ để chơi.', 'loi');
                    o('manBatDau').style.display = '';
                    o('manChoi').style.display = 'none';
                    return;
                }
                cauHoi = ds;
                o('tongSoCau').textContent = cauHoi.length;
                veCauHoi();
            });
    }

    function veCauHoi() {
        var c = cauHoi[viTri];

        o('soCauHienTai').textContent = viTri + 1;
        o('tenCauThu').textContent = c.hoTen;
        o('chiSoCauThu').textContent = c.chiSoTongHop;
        o('metaCauThu').innerHTML =
            '<span class="nhan nhan-vi-tri">' + (c.viTri || '') + '</span>' +
            '<span>' + c.tuoi + ' tuổi</span>' +
            '<span>' + (c.cauLacBo || '') + '</span>' +
            '<span>' + (c.quocTich || '') + '</span>';

        var anh = o('anhCauThu');
        anh.textContent = chuCaiDau(c.hoTen);
        anh.style.background = 'linear-gradient(140deg,' + mauTheoTen(c.hoTen) + ',#0f172a)';

        o('thanhDoan').value = 50;
        o('nhanDoan').textContent = '50 tr €';
        o('ketQuaCau').style.display = 'none';
        o('nutChot').textContent = 'Chốt đáp án';
        o('nutChot').disabled = false;
    }

    function chot() {
        var nut = o('nutChot');

        // Lần bấm thứ hai là "câu tiếp theo".
        if (nut.dataset.chuyen === '1') {
            nut.dataset.chuyen = '0';
            viTri++;
            if (viTri >= cauHoi.length) ketThuc();
            else veCauHoi();
            return;
        }

        var c = cauHoi[viTri];
        var doan = parseFloat(o('thanhDoan').value);
        var that = parseFloat(c.giaTriThat);

        var lech = that > 0 ? Math.abs(doan - that) / that * 100 : 100;
        tongLech += lech;

        // Điểm: sai 0% được 100 điểm, sai 50% trở lên được 0 điểm.
        var diem = Math.max(0, Math.round(100 - lech * 2));
        tongDiem += diem;

        var dung = lech <= 10;
        if (dung) soDung++;

        o('diemHienTai').textContent = tongDiem;

        var khung = o('ketQuaCau');
        khung.className = 'bao ' + (dung ? 'bao-ok' : 'bao-loi');
        khung.style.display = 'flex';
        khung.innerHTML =
            '<span>' + (dung ? '✓' : '✗') + '</span>' +
            '<span>Giá trị thật: <strong>' + window.PMGia(that) + '</strong> · ' +
            'bạn đoán ' + window.PMGia(doan) + ' · lệch ' + window.PMSo(lech, 0) + '% · ' +
            '<strong>+' + diem + ' điểm</strong></span>';

        if (dung && window.PMPhaoGiay && lech <= 3) window.PMPhaoGiay(40);

        nut.dataset.chuyen = '1';
        nut.textContent = viTri + 1 >= cauHoi.length ? 'Xem kết quả' : 'Câu tiếp theo →';
    }

    function ketThuc() {
        o('manChoi').style.display = 'none';
        o('manKetThuc').style.display = '';

        var lechTb = Math.round(tongLech / cauHoi.length);
        o('diemCuoi').textContent = tongDiem;
        o('tomTatCuoi').textContent =
            'Đoán đúng ' + soDung + '/' + cauHoi.length + ' cầu thủ · độ lệch trung bình ' + lechTb + '%';

        if (tongDiem >= cauHoi.length * 70 && window.PMPhaoGiay) window.PMPhaoGiay(120);

        fetch('/Game/LuuDiem', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': window.PMCsrf() },
            body: JSON.stringify({
                soCauDung: soDung,
                tongSoCau: cauHoi.length,
                diem: tongDiem,
                doLechTrungBinh: lechTb
            })
        })
            .then(function (r) { return r.json(); })
            .then(function (kq) {
                if (kq && kq.ok) {
                    if (window.PMToast) window.PMToast('Đã lưu điểm vào bảng xếp hạng.', 'ok');
                } else if (window.PMToast) {
                    window.PMToast('Đăng nhập để lưu điểm vào bảng xếp hạng.', '');
                }
            })
            .catch(function () { });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var nutBatDau = o('nutBatDau');
        if (!nutBatDau) return;

        nutBatDau.addEventListener('click', batDau);
        o('nutChoiLai').addEventListener('click', batDau);
        o('nutChot').addEventListener('click', chot);
    });
})();
