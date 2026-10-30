import { bookingApi } from '../api/bookingApi.js?auth-session-2';
// Đã sửa: Import thêm hàm getReservationStatusLabel để dùng hiển thị trạng thái
import { isReservationCancellable, renderReservationDetails, getReservationStatusLabel } from '../components/bookingView.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

const form = document.querySelector('[data-booking-lookup-form]');
const statusElement = document.querySelector('[data-lookup-status]');
const resultElement = document.querySelector('[data-lookup-result]');

if (form && statusElement && resultElement) {
    setupSessionNavigation();
    const submitButton = form.querySelector('[data-lookup-submit]');
    let pending = false;
    let currentReservation = null;

    function showMessage(message, isError = false) {
        statusElement.textContent = message;
        statusElement.classList.toggle('text-error', isError);
        statusElement.classList.toggle('text-on-surface-variant', !isError);
    }

    function renderLookupReservation(reservation, code, phone) {
        if (!reservation || typeof reservation !== 'object') return;

        currentReservation = reservation;
        resultElement.classList.remove('hidden');
        resultElement.replaceChildren();
        renderReservationDetails(resultElement, reservation);

        if (!isReservationCancellable(reservation)) {
            const status = reservation.status ?? reservation.Status;
            const note = document.createElement('p');
            note.className = 'mt-6 text-body-sm text-on-surface-variant';
            
            // Đã sửa: Sử dụng hàm getReservationStatusLabel thay vì in số nguyên
            const statusLabel = getReservationStatusLabel(status);
            note.textContent = Number.isInteger(Number(status)) ? `Trạng thái hiện tại: ${statusLabel}.` : 'Không đủ điều kiện hủy theo trạng thái đặt bàn hiện tại.';
            
            resultElement.append(note);
            return;
        }

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'mt-6 h-11 rounded-lg bg-error-container px-5 font-label-md text-error disabled:opacity-50';
        button.textContent = 'Hủy đặt bàn';
        button.disabled = pending;
        button.addEventListener('click', async () => {
            if (pending || !window.confirm('Bạn có chắc muốn hủy đặt bàn này?')) return;

            pending = true;
            button.disabled = true;
            showMessage('Đang gửi yêu cầu hủy...');

            try {
                await bookingApi.cancelBooking(code, phone);
                // Giả lập trạng thái đã hủy (2) nếu API không trả về object mới
                currentReservation = { ...reservation, status: 2, Status: 2 }; 
                resultElement.replaceChildren();
                renderReservationDetails(resultElement, currentReservation);
                showMessage('Đã hủy đặt bàn thành công.', false);
                
                // Ẩn nút hủy đi sau khi đã hủy thành công
                button.classList.add('hidden');
                
            } catch (error) {
                showMessage(error?.message ?? 'Không thể hủy đặt bàn. Vui lòng thử lại.', true);
            } finally {
                pending = false;
                button.disabled = false;
            }
        });

        resultElement.append(button);
    }

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (pending) return;

        let code = form.elements.reservationCode.value.trim();
        const phone = form.elements.phone.value.trim();

        if (!code) {
            showMessage('Vui lòng nhập mã đặt bàn.', true);
            return;
        }

        // Đã bổ sung: Tự động loại bỏ dấu # nếu khách hàng lỡ copy vào
        if (code.startsWith('#')) {
            code = code.substring(1);
        }

        if (!phone) {
            showMessage('Vui lòng nhập số điện thoại.', true);
            return;
        }

        const digits = phone.replace(/\D/g, '');
        if (digits.length < 9 || digits.length > 11) {
            showMessage('Số điện thoại không hợp lệ.', true);
            return;
        }

        if (/[a-zA-Z]/.test(phone)) {
            showMessage('Số điện thoại không được chứa chữ cái.', true);
            return;
        }

        pending = true;
        submitButton.disabled = true;
        resultElement.replaceChildren();
        resultElement.classList.add('hidden');
        showMessage('Đang tra cứu đặt bàn...');

        try {
            const reservation = await bookingApi.lookupBooking(code, phone);
            if (!reservation || typeof reservation !== 'object') throw new TypeError('Reservation response is invalid.');
            renderLookupReservation(reservation, code, phone);
            showMessage('Đã tải thông tin đặt bàn.');
        } catch (error) {
            currentReservation = null;
            showMessage(error?.message ?? 'Không tìm thấy đặt bàn phù hợp với mã và số điện thoại.', true);
        } finally {
            pending = false;
            submitButton.disabled = false;
        }
    });

    async function reloadCurrentReservationSilently() {
        if (!currentReservation) return;
        const code = form.elements.reservationCode.value.trim().replace(/^#/, '');
        const phone = form.elements.phone.value.trim();
        if (!code || !phone) return;
        try {
            const reservation = await bookingApi.lookupBooking(code, phone);
            if (reservation && typeof reservation === 'object') {
                renderLookupReservation(reservation, code, phone);
            }
        } catch (e) {
            console.debug('Silent lookup reload failed:', e);
        }
    }

    realtimeClient.on('ReservationUpdated', reloadCurrentReservationSilently);
    realtimeClient.on('sync', reloadCurrentReservationSilently);
    realtimeClient.start().catch(() => {});
}