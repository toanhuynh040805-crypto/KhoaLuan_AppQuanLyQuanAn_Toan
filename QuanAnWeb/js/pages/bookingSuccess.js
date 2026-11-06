import { bookingApi } from '../api/bookingApi.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

/**
 * Ánh xạ trạng thái đặt bàn động đồng bộ với Backend Enum & các trang tra cứu:
 * 5 | "ChoXacNhan" (hoặc undefined/trống do mới tạo): Chờ xác nhận (text-amber-600)
 * 0 | "DaXacNhan" / "DaDat": Đã xác nhận (text-emerald-600)
 * 1 | "DaNhanBan": Đã nhận bàn (text-blue-600)
 * 2 | "DaHuy": Đã hủy (text-gray-500)
 */
function getStatusMeta(status) {
    const s = String(status ?? '').trim();

    // 5 hoặc "ChoXacNhan" (hoặc undefined/rỗng do mới tạo) -> Chờ xác nhận (Vàng/Cam)
    if (!s || s === '5' || s === 'ChoXacNhan') {
        return {
            statusCode: 5,
            text: 'Chờ xác nhận',
            colorClass: 'text-amber-600'
        };
    }

    // 0 hoặc "DaXacNhan" / "DaDat" -> Đã xác nhận (Xanh lá)
    if (s === '0' || s === 'DaXacNhan' || s === 'DaDat') {
        return {
            statusCode: 0,
            text: 'Đã xác nhận',
            colorClass: 'text-emerald-600'
        };
    }

    // 1 hoặc "DaNhanBan" -> Đã nhận bàn (Xanh dương)
    if (s === '1' || s === 'DaNhanBan') {
        return {
            statusCode: 1,
            text: 'Đã nhận bàn',
            colorClass: 'text-blue-600'
        };
    }

    // 2 hoặc "DaHuy" -> Đã hủy (Xám)
    if (s === '2' || s === 'DaHuy') {
        return {
            statusCode: 2,
            text: 'Đã hủy',
            colorClass: 'text-gray-500'
        };
    }

    // Mặc định fallback về Chờ xác nhận
    return {
        statusCode: 5,
        text: 'Chờ xác nhận',
        colorClass: 'text-amber-600'
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    // Tự động đồng bộ cấu hình giữ bàn từ Backend để hiển thị chính xác
    await bookingApi.getConfig().catch(() => {});

    // 1. Lấy dữ liệu từ sessionStorage, localStorage hoặc URL parameters
    const urlParams = new URLSearchParams(window.location.search);
    const codeFromUrl = urlParams.get('code') || urlParams.get('id');
    const statusFromUrl = urlParams.get('status');

    let reservation = null;
    try {
        const rawSession = sessionStorage.getItem('gusto_booking_result');
        const rawLocal = localStorage.getItem('gusto_last_booking');
        
        if (rawSession) {
            reservation = JSON.parse(rawSession);
            sessionStorage.removeItem('gusto_booking_result');
        } else if (rawLocal) {
            reservation = JSON.parse(rawLocal);
        }
    } catch {
        reservation = null;
    }

    if (!reservation && codeFromUrl) {
        reservation = {
            reservationCode: codeFromUrl,
            status: statusFromUrl
        };
    }

    // 2. Lấy các phần tử hiển thị trên trang success.html
    const codeEl = document.getElementById('res-code');
    const statusEl = document.getElementById('res-status') || Array.from(document.querySelectorAll('p')).find(p => p.textContent.trim() === 'Trạng thái')?.nextElementSibling;
    const nameEl = document.getElementById('res-name');
    const phoneEl = document.getElementById('res-phone');
    const datetimeEl = document.getElementById('res-datetime');
    const guestsEl = document.getElementById('res-guests');
    const noteEl = document.getElementById('res-note');
    const holdPolicyEl = document.getElementById('res-hold-policy');

    // Hàm cập nhật trạng thái trên DOM
    function renderStatus(status) {
        const meta = getStatusMeta(status);
        if (statusEl) {
            statusEl.textContent = meta.text;
            statusEl.classList.remove('text-tertiary', 'text-amber-600', 'text-yellow-600', 'text-emerald-600', 'text-green-600', 'text-blue-600', 'text-gray-500', 'text-on-surface-variant');
            statusEl.classList.add(meta.colorClass);
        }
        return meta;
    }

    // 3. Đổ dữ liệu thật vào giao diện nếu có
    if (reservation && typeof reservation === 'object') {
        // Đã đồng bộ: Ưu tiên lấy khóa chính Id / ReservationId từ Backend, sau đó mới fallback về mã ngẫu nhiên
        const rawId = reservation.id || reservation.Id || reservation.reservationId || reservation.ReservationId || reservation.reservationCode || codeFromUrl;
        const code = rawId ? `#${rawId}` : '---';
        const cleanCode = String(rawId || '').replace(/^#/, '');
        
        const name = reservation.customerName || reservation.CustomerName || reservation.name || reservation.Name;
        const phone = reservation.customerPhone || reservation.CustomerPhone || reservation.phone || reservation.Phone;
        
        // Trạng thái đơn ban đầu: Đọc từ reservation hoặc tham số URL (mặc định ChoXacNhan / 5 khi vừa tạo)
        let currentStatus = reservation.status ?? reservation.Status ?? statusFromUrl;
        renderStatus(currentStatus);

        // Xử lý ngày và giờ linh hoạt (từ form hoặc từ chuỗi reservationTime của Backend)
        let dateVal = reservation.date || reservation.Date;
        let timeVal = reservation.time || reservation.Time;
        
        const isoTime = reservation.reservationTime || reservation.ReservationTime;
        if ((!dateVal || !timeVal) && isoTime) {
            const dt = new Date(isoTime);
            if (!isNaN(dt.getTime())) {
                dateVal = dt.toISOString().slice(0, 10);
                timeVal = dt.toTimeString().slice(0, 5);
            }
        }

        const guests = reservation.numberOfGuests || reservation.NumberOfGuests || reservation.guests;
        const note = reservation.note || reservation.Note;
        const durationMinutes = Number(
            reservation.durationMinutes ??
            reservation.DurationMinutes ??
            window.AppConfig?.bookingDuration ??
            30
        );
        const rawHoldUntil = reservation.holdUntil || reservation.HoldUntil;
        let holdUntil = rawHoldUntil ? new Date(rawHoldUntil) : null;

        if ((!holdUntil || Number.isNaN(holdUntil.getTime())) && dateVal && timeVal) {
            const start = new Date(`${dateVal}T${timeVal}:00`);
            if (!Number.isNaN(start.getTime())) {
                holdUntil = new Date(start.getTime() + durationMinutes * 60_000);
            }
        }

        if (codeEl) codeEl.textContent = code;
        if (nameEl) nameEl.textContent = name || 'Chưa cập nhật';
        if (phoneEl) phoneEl.textContent = phone || 'Chưa cập nhật';
        
        if (datetimeEl) {
            const formattedDate = dateVal ? dateVal.split('-').reverse().join('/') : '';
            datetimeEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">calendar_today</span> ${formattedDate ? formattedDate + ' - ' : ''}${timeVal || 'Chưa chọn giờ'}`;
        }
        
        if (guestsEl) {
            guestsEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">group</span> ${guests ? guests + ' Khách' : 'Chưa chọn số khách'}`;
        }
        
        if (noteEl) {
            noteEl.textContent = note ? `"${note}"` : 'Không có yêu cầu đặc biệt.';
        }

        if (holdPolicyEl) {
            const holdTimeText =
                holdUntil && !Number.isNaN(holdUntil.getTime())
                    ? holdUntil.toLocaleTimeString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                        hour12: false
                    })
                    : null;

            holdPolicyEl.textContent = holdTimeText
                ? `Vui lòng đến đúng giờ. Bàn được giữ tối đa ${durationMinutes} phút, đến ${holdTimeText}. Sau thời gian này nhà hàng có thể hủy lượt đặt nếu bạn chưa nhận bàn.`
                : `Vui lòng đến đúng giờ. Bàn được giữ tối đa ${durationMinutes} phút sau giờ hẹn.`;
        }

        // =====================================================================
        // 4. ĐỒNG BỘ REALTIME KHI WINFORM XÁC NHẬN HOẶC HỦY ĐẶT BÀN
        // =====================================================================
        // Khi nhân viên trên WinForm bấm Xác nhận/Hủy, Backend bắn sự kiện 'ReservationUpdated' qua SignalR Hub
        realtimeClient.on('ReservationUpdated', async (payload) => {
            const updatedId = String(payload?.reservationId ?? payload?.ReservationId ?? '');
            if (updatedId && (updatedId === cleanCode || updatedId === String(rawId))) {
                const incomingStatus = payload?.status ?? payload?.Status;
                if (incomingStatus !== undefined && incomingStatus !== null) {
                    currentStatus = incomingStatus;
                    renderStatus(currentStatus);
                    
                    // Lưu lại vào localStorage để khi khách F5 vẫn hiển thị đúng trạng thái mới
                    reservation.status = incomingStatus;
                    localStorage.setItem('gusto_last_booking', JSON.stringify(reservation));
                } else if (cleanCode && phone) {
                    // Fallback tra cứu qua API nếu payload không đính kèm trạng thái
                    try {
                        const updated = await bookingApi.lookupBooking(cleanCode, phone);
                        if (updated) {
                            currentStatus = updated.status ?? updated.Status;
                            renderStatus(currentStatus);
                            reservation.status = currentStatus;
                            localStorage.setItem('gusto_last_booking', JSON.stringify(reservation));
                        }
                    } catch {
                        // Silent
                    }
                }
            }
        });

        // 5. Polling dự phòng nhẹ (mỗi 10s) khi đơn còn ở trạng thái "Chờ xác nhận"
        let pollTimer = null;
        const startStatusPolling = () => {
            if (pollTimer || !cleanCode || !phone) return;
            pollTimer = setInterval(async () => {
                const s = String(currentStatus ?? '').trim();
                // Dừng polling nếu đơn đã chuyển sang Đã xác nhận / Đã hủy / Đã nhận bàn
                if (s !== '' && s !== '5' && s !== 'ChoXacNhan') {
                    clearInterval(pollTimer);
                    pollTimer = null;
                    return;
                }
                try {
                    const latest = await bookingApi.lookupBooking(cleanCode, phone);
                    if (latest) {
                        const newStatus = latest.status ?? latest.Status;
                        if (newStatus !== undefined && newStatus !== currentStatus) {
                            currentStatus = newStatus;
                            renderStatus(currentStatus);
                            reservation.status = currentStatus;
                            localStorage.setItem('gusto_last_booking', JSON.stringify(reservation));
                            
                            const updatedStr = String(newStatus ?? '').trim();
                            if (updatedStr !== '5' && updatedStr !== 'ChoXacNhan') {
                                clearInterval(pollTimer);
                                pollTimer = null;
                            }
                        }
                    }
                } catch {
                    // Silent
                }
            }, 10000);
        };

        startStatusPolling();

    } else {
        // Xử lý hoàn toàn không có dữ liệu mẫu (Fallback sạch sẽ)
        if (codeEl) codeEl.textContent = '---';
        if (statusEl) {
            statusEl.textContent = '---';
            statusEl.className = 'text-sm font-semibold text-on-surface-variant mt-1';
        }
        if (nameEl) nameEl.textContent = 'Chưa có thông tin';
        if (phoneEl) phoneEl.textContent = '---';
        if (datetimeEl) datetimeEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">calendar_today</span> Chưa chọn lịch`;
        if (guestsEl) guestsEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">group</span> ---`;
        if (noteEl) noteEl.textContent = 'Không có.';
    }
});
