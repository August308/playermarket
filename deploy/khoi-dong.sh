#!/bin/bash
# Khởi động API (nội bộ) rồi giao diện (công khai) trong cùng một container.
# Nếu một trong hai tiến trình dừng, script thoát để nền tảng khởi động lại cả container.
set -u

CONG_WEB="${PORT:-10000}"
CONG_API=8081
DIA_CHI_CONG_KHAI="${RENDER_EXTERNAL_URL:-http://localhost:${CONG_WEB}}"

# Ổ đĩa của gói miễn phí không bền: mỗi lần container khởi động lại, thư mục dữ liệu trống
# và cơ sở dữ liệu được chép lại từ bản gốc trong ảnh. Nếu gắn ổ đĩa bền vào /app/dulieu
# (gói trả phí) thì tệp đã có sẽ được giữ nguyên.
mkdir -p /app/dulieu
if [ ! -f /app/dulieu/playertransfer.db ]; then
    cp /app/khoi-tao/playertransfer.db /app/dulieu/playertransfer.db
    echo "[khoi-dong] Đã chép cơ sở dữ liệu ban đầu vào /app/dulieu."
fi

dung_tat_ca() {
    kill "${PID_API:-}" "${PID_WEB:-}" 2>/dev/null
    wait 2>/dev/null
}
trap 'dung_tat_ca; exit 0' TERM INT

# Giới hạn bộ nhớ. Hai tiến trình .NET dùng chung một container; mặc định MỖI tiến trình tự cho
# phép mình dùng tới 75% RAM của container, cộng lại sẽ vượt 512 MB của gói miễn phí và bị nền tảng
# tắt đột ngột. Đặt trần heap riêng cho từng tiến trình (đơn vị: byte, viết dạng thập lục phân).
# Có thể nới qua biến môi trường khi dùng gói có nhiều RAM hơn.
HEAP_API="${HEAP_API:-0xB400000}"    # 180 MB
HEAP_WEB="${HEAP_WEB:-0x6400000}"    # 100 MB

# ---------- API: chỉ nghe trong container ----------
cd /app/api
DOTNET_GCHeapHardLimit="${HEAP_API}" \
ASPNETCORE_URLS="http://127.0.0.1:${CONG_API}" \
Database__Provider="Sqlite" \
ConnectionStrings__Default="Data Source=/app/dulieu/playertransfer.db" \
Seed__TongCauThu="0" \
BatSwagger="false" \
Web__BaseUrl="${DIA_CHI_CONG_KHAI}" \
Cors__Origins__0="${DIA_CHI_CONG_KHAI}" \
ForwardedHeaders__MangTinCay__0="127.0.0.0/8" \
dotnet PlayerTransfer.Api.dll &
PID_API=$!

# Chờ API sẵn sàng (tối đa 90 giây) để trang đầu tiên không báo "không kết nối được API".
for i in $(seq 1 90); do
    if (echo > "/dev/tcp/127.0.0.1/${CONG_API}") 2>/dev/null; then
        echo "[khoi-dong] API đã sẵn sàng sau ${i} giây."
        break
    fi
    if ! kill -0 "$PID_API" 2>/dev/null; then
        echo "[khoi-dong] API dừng ngay khi khởi động — xem lỗi phía trên (thường do thiếu biến Jwt__Key)."
        exit 1
    fi
    sleep 1
done

# ---------- Giao diện: cổng công khai ----------
cd /app/web
DOTNET_GCHeapHardLimit="${HEAP_WEB}" \
ASPNETCORE_URLS="http://0.0.0.0:${CONG_WEB}" \
ApiBaseUrl="http://127.0.0.1:${CONG_API}/" \
ForwardedHeaders__TinProxy="true" \
Realtime__Tat="true" \
dotnet PlayerTransfer.Web.dll &
PID_WEB=$!

# Một trong hai dừng thì dừng nốt tiến trình còn lại và thoát với mã lỗi.
wait -n "$PID_API" "$PID_WEB"
MA_THOAT=$?
echo "[khoi-dong] Một tiến trình đã dừng (mã ${MA_THOAT}); dừng container."
dung_tat_ca
exit "${MA_THOAT:-1}"
