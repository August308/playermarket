// D1 — kênh thông báo thời gian thực qua SignalR.
// Không bắt buộc: nếu máy chủ API chưa chạy hoặc thư viện chưa tải được thì
// trang vẫn hoạt động bình thường, chỉ mất phần cập nhật tức thời.
(function () {
    'use strict';

    var ketNoi = null;
    var daBao = false;

    function hienThongBao(noiDung, loai) {
        if (window.PMToast) {
            window.PMToast(noiDung, loai || 'tin');
        } else {
            console.log('[realtime]', noiDung);
        }
    }

    function capNhatHuyHieuDongBo(duLieu) {
        var o = document.getElementById('trangThaiDongBo');
        if (!o) return;
        o.textContent = 'Vừa đồng bộ: +' + duLieu.themMoi + ' / ~' + duLieu.capNhat;
        o.style.display = '';
    }

    function xuLy(loai, duLieu) {
        duLieu = duLieu || {};

        switch (loai) {
            case 'canhbao-gia':
                hienThongBao(
                    'Cảnh báo giá: ' + duLieu.hoTen + ' hiện ' +
                    window.PMGia(duLieu.giaTri) + ' — ' + duLieu.lyDo,
                    'canhbao');
                thongBaoTrinhDuyet('Cảnh báo giá cầu thủ', duLieu.hoTen + ': ' + duLieu.lyDo);
                break;

            case 'dong-bo-xong':
                hienThongBao(duLieu.thongDiep || 'Đã đồng bộ xong dữ liệu.',
                    duLieu.trangThai === 'ThatBai' ? 'loi' : 'thanhcong');
                capNhatHuyHieuDongBo(duLieu);
                break;

            case 'gia-thay-doi':
                // Nếu đang mở đúng trang cầu thủ đó thì cập nhật con số tại chỗ.
                var o = document.querySelector('[data-gia-cau-thu="' + duLieu.maCauThu + '"]');
                if (o) {
                    o.textContent = window.PMGia(duLieu.giaTri);
                    o.classList.add('nhap-nhay');
                    setTimeout(function () { o.classList.remove('nhap-nhay'); }, 1500);
                }
                break;

            default:
                break;
        }
    }

    function thongBaoTrinhDuyet(tieuDe, noiDung) {
        if (!('Notification' in window)) return;
        if (Notification.permission === 'granted') {
            try { new Notification(tieuDe, { body: noiDung, icon: '/icon-192.png' }); } catch (e) { }
        }
    }

    function khoiTao(tuyChon) {
        if (!tuyChon || !tuyChon.diaChiHub) return;
        if (typeof signalR === 'undefined') return;
        if (ketNoi) return;

        var xayDung = new signalR.HubConnectionBuilder()
            .withUrl(tuyChon.diaChiHub, {
                accessTokenFactory: function () {
                    // Khách chưa đăng nhập: kết nối ẩn danh (chỉ nhận thông báo chung).
                    if (!tuyChon.daDangNhap) return '';
                    // Lấy token từ máy chủ Web mỗi lần (kết nối / kết nối lại) — không in sẵn vào HTML.
                    return fetch('/Account/TokenRealtime', { credentials: 'same-origin', cache: 'no-store' })
                        .then(function (r) { return r.ok ? r.json() : { token: '' }; })
                        .then(function (kq) { return kq.token || ''; })
                        .catch(function () { return ''; });
                }
            })
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Error);

        ketNoi = xayDung.build();

        ketNoi.on('ThongBao', xuLy);

        ketNoi.onreconnected(function () {
            hienThongBao('Đã kết nối lại kênh thời gian thực.', 'thanhcong');
        });

        ketNoi.start().catch(function () {
            // Chỉ báo một lần để không làm phiền khi API chưa chạy.
            if (!daBao) {
                daBao = true;
                console.warn('[realtime] Chưa kết nối được kênh thời gian thực.');
            }
        });
    }

    function theoDoiCauThu(maCauThu) {
        if (ketNoi && ketNoi.state === signalR.HubConnectionState.Connected) {
            ketNoi.invoke('TheoDoiCauThu', maCauThu).catch(function () { });
        }
    }

    window.PMRealtime = {
        khoiTao: khoiTao,
        theoDoiCauThu: theoDoiCauThu
    };
})();
