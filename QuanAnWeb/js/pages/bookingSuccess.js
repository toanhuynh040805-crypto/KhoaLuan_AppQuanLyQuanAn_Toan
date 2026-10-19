document.addEventListener('DOMContentLoaded', () => {
    // 1. Lấy dữ liệu từ sessionStorage hoặc localStorage do trang đặt bàn truyền sang
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

    // 2. Lấy các phần tử hiển thị trên trang success.html
    const codeEl = document.getElementById('res-code');
    const nameEl = document.getElementById('res-name');
    const phoneEl = document.getElementById('res-phone');
    const datetimeEl = document.getElementById('res-datetime');
    const guestsEl = document.getElementById('res-guests');
    const noteEl = document.getElementById('res-note');

    // 3. Đổ dữ liệu thật vào giao diện nếu có
    if (reservation && typeof reservation === 'object') {
        // Đã đồng bộ: Ưu tiên lấy khóa chính Id / ReservationId từ Backend, sau đó mới fallback về mã ngẫu nhiên
        const rawId = reservation.id || reservation.Id || reservation.reservationId || reservation.ReservationId || reservation.reservationCode;
        const code = rawId ? `#${rawId}` : '---';
        
        const name = reservation.customerName || reservation.CustomerName || reservation.name || reservation.Name;
        const phone = reservation.customerPhone || reservation.CustomerPhone || reservation.phone || reservation.Phone;
        
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
    } else {
        // Xử lý hoàn toàn không có dữ liệu mẫu (Fallback sạch sẽ)
        if (codeEl) codeEl.textContent = '---';
        if (nameEl) nameEl.textContent = 'Chưa có thông tin';
        if (phoneEl) phoneEl.textContent = '---';
        if (datetimeEl) datetimeEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">calendar_today</span> Chưa chọn lịch`;
        if (guestsEl) guestsEl.innerHTML = `<span class="material-symbols-outlined text-on-surface-variant text-sm">group</span> ---`;
        if (noteEl) noteEl.textContent = 'Không có.';
    }
});