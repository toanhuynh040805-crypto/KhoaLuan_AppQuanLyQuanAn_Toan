import { hasLoadedCustomer, ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { bookingApi } from '../api/bookingApi.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

/**
 * =========================================================================
 * MODULE: TRA CỨU & QUẢN LÝ ĐẶT BÀN (GUSTOPOS WEB)
 * ĐỒNG BỘ 100% VỚI ASP.NET CORE BACKEND & C# WINFORMS
 * 
 * - Backend Model: Models.Reservation (Enum ReservationStatus 0..5)
 * - WinForm: TableManagementControl.cs (Cập nhật Status 0 khi nhân viên Duyệt)
 * - Guest API: GET /api/reservations/lookup?code=..&phone=.. (Status: int)
 * - Guest Cancel: POST /api/reservations/cancel?code=..&phone=..
 * - Member API: GET /api/customers/me/bookings (Status: string)
 * - Member Cancel: POST /api/reservations/me/{id}/cancel
 * - Realtime: SignalR Hub 'OrderHub' -> Event 'ReservationUpdated'
 * =========================================================================
 */

// 1. CÁC PHẦN TỬ GIAO DIỆN
const pageRoleBadge = document.getElementById('page-role-badge');
const pageTitle = document.getElementById('page-title');
const pageSubtitle = document.getElementById('page-subtitle');
const headerActions = document.getElementById('header-actions');
const btnRefreshBookings = document.getElementById('btn-refresh-bookings');
const globalAlert = document.getElementById('lookup-global-alert');

// Khối Khách vãng lai (Guest)
const guestLookupSection = document.getElementById('guest-lookup-section');
const guestForm = document.querySelector('[data-booking-lookup-form]');
const guestResultSection = document.getElementById('guest-result-section');
const guestCardContainer = document.getElementById('guest-card-container');
const btnGuestSearchAgain = document.getElementById('btn-guest-search-again');

// Khối Thành viên (Member)
const memberBookingsSection = document.getElementById('member-bookings-section');
const memberBookingsList = document.getElementById('member-bookings-list');
const memberBookingsEmpty = document.getElementById('member-bookings-empty');

// Popup Modal Xác nhận Hủy bàn (Confirm Modal - Thay thế OTP vì Backend không có OTP)
const cancelConfirmModal = document.getElementById('cancel-confirm-modal');
const modalBookingCode = document.getElementById('modal-booking-code');
const modalTimeDisplay = document.getElementById('modal-time-display');
const modalTableDisplay = document.getElementById('modal-table-display');
const cancelErrorMsg = document.getElementById('cancel-error-msg');
const cancelErrorText = document.getElementById('cancel-error-text');
const btnCloseConfirmModal = document.getElementById('btn-close-confirm-modal');
const btnCancelModalAction = document.getElementById('btn-cancel-modal-action');
const btnConfirmCancelBooking = document.getElementById('btn-confirm-cancel-booking');
const btnConfirmText = document.getElementById('btn-confirm-text');

// Biến trạng thái
let activePendingBooking = null;
let pendingCancelCallback = null;
let isSubmittingCancel = false;
let currentGuestReservation = null;

// =========================================================================
// 2. HELPER FUNCTIONS & BUSINESS LOGIC MAPPING
// =========================================================================

/**
 * Hiển thị thông báo Alert toàn trang
 */
function showAlert(message, type = 'info', autoHideMs = 6000) {
    if (!globalAlert) return;
    if (!message) {
        globalAlert.className = 'hidden';
        globalAlert.innerHTML = '';
        return;
    }

    const configs = {
        success: {
            bg: 'bg-emerald-50 border-emerald-200 text-emerald-900',
            icon: 'check_circle',
            iconColor: 'text-emerald-600'
        },
        error: {
            bg: 'bg-red-50 border-red-200 text-red-900',
            icon: 'error',
            iconColor: 'text-error'
        },
        warning: {
            bg: 'bg-amber-50 border-amber-200 text-amber-900',
            icon: 'warning',
            iconColor: 'text-amber-600'
        },
        info: {
            bg: 'bg-blue-50 border-blue-200 text-blue-900',
            icon: 'info',
            iconColor: 'text-blue-600'
        }
    };
    const c = configs[type] || configs.info;
    globalAlert.className = `rounded-xl border p-4 text-xs sm:text-sm font-medium transition-all shadow-xs flex items-center gap-2.5 ${c.bg}`;
    globalAlert.innerHTML = `
        <span class="material-symbols-outlined text-[20px] shrink-0 ${c.iconColor}">${c.icon}</span>
        <div class="flex-1 leading-relaxed">${message}</div>
    `;

    if (autoHideMs > 0) {
        setTimeout(() => {
            if (globalAlert.textContent.includes(message)) {
                globalAlert.className = 'hidden';
                globalAlert.innerHTML = '';
            }
        }, autoHideMs);
    }
}

/**
 * Định dạng ngày giờ hiển thị (HH:mm - DD/MM/YYYY)
 */
function formatBookingDateTime(rawTime) {
    if (!rawTime) return '—';
    try {
        const d = new Date(rawTime);
        if (Number.isNaN(d.getTime())) return String(rawTime);
        const pad = (n) => String(n).padStart(2, '0');
        return `${pad(d.getHours())}:${pad(d.getMinutes())} - ${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
    } catch {
        return String(rawTime);
    }
}

/**
 * ÁNH XẠ TRẠNG THÁI (STATUS MAPPING) ĐỒNG BỘ 100% VỚI WINFORM & BACKEND:
 * Enum Backend:
 *   0: DaXacNhan  - Đã xác nhận (Xanh lá)
 *   1: DaNhanBan  - Đã nhận bàn (Xanh dương)
 *   2: DaHuy      - Đã hủy (Xám)
 *   3: KhongDen   - Không đến (Đỏ/Xám)
 *   4: HoanTat    - Hoàn tất (Tím)
 *   5: ChoXacNhan - Chờ xác nhận (Vàng)
 */
function getBookingStatusMeta(status) {
    const s = String(status ?? '').trim();

    // 5 hoặc "ChoXacNhan" -> Chờ xác nhận (Vàng)
    if (s === '5' || s === 'ChoXacNhan') {
        return {
            statusCode: 5,
            label: 'Chờ xác nhận',
            badgeClass: 'bg-amber-100 text-amber-800 border-amber-200',
            dotClass: 'bg-amber-500',
            canCancelStatus: true
        };
    }

    // 0 hoặc "DaXacNhan" / "DaDat" -> Đã xác nhận (Xanh lá)
    if (s === '0' || s === 'DaXacNhan' || s === 'DaDat') {
        return {
            statusCode: 0,
            label: 'Đã xác nhận',
            badgeClass: 'bg-emerald-100 text-emerald-800 border-emerald-200',
            dotClass: 'bg-emerald-500',
            canCancelStatus: true
        };
    }

    // 2 hoặc "DaHuy" -> Đã hủy (Xám)
    if (s === '2' || s === 'DaHuy') {
        return {
            statusCode: 2,
            label: 'Đã hủy',
            badgeClass: 'bg-gray-100 text-gray-700 border-gray-200',
            dotClass: 'bg-gray-400',
            canCancelStatus: false
        };
    }

    // 1 hoặc "DaNhanBan" -> Đã nhận bàn (Xanh dương)
    if (s === '1' || s === 'DaNhanBan') {
        return {
            statusCode: 1,
            label: 'Đã nhận bàn',
            badgeClass: 'bg-blue-100 text-blue-800 border-blue-200',
            dotClass: 'bg-blue-500',
            canCancelStatus: false
        };
    }

    // 3 hoặc "KhongDen" -> Không đến (Đỏ)
    if (s === '3' || s === 'KhongDen') {
        return {
            statusCode: 3,
            label: 'Không đến',
            badgeClass: 'bg-red-100 text-red-800 border-red-200',
            dotClass: 'bg-red-500',
            canCancelStatus: false
        };
    }

    // 4 hoặc "HoanTat" -> Hoàn tất (Tím)
    if (s === '4' || s === 'HoanTat') {
        return {
            statusCode: 4,
            label: 'Hoàn tất',
            badgeClass: 'bg-purple-100 text-purple-800 border-purple-200',
            dotClass: 'bg-purple-500',
            canCancelStatus: false
        };
    }

    // Fallback mặc định
    return {
        statusCode: 5,
        label: s || 'Chờ xác nhận',
        badgeClass: 'bg-amber-100 text-amber-800 border-amber-200',
        dotClass: 'bg-amber-500',
        canCancelStatus: true
    };
}

/**
 * CHÍNH SÁCH HỦY BÀN (CANCELLATION POLICY):
 * Chặn hủy sát giờ nếu thời gian từ hiện tại đến giờ nhận bàn < 2 tiếng (120 phút).
 */
function checkCancellationPolicy(reservationTime) {
    if (!reservationTime) {
        return { canCancel: false, isTooLate: true, minutesLeft: 0, reason: 'Không có thông tin thời gian.' };
    }
    const target = new Date(reservationTime);
    if (Number.isNaN(target.getTime())) {
        return { canCancel: false, isTooLate: true, minutesLeft: 0, reason: 'Thời gian đặt bàn không hợp lệ.' };
    }

    const now = new Date();
    const diffMs = target.getTime() - now.getTime();
    const diffMinutes = Math.floor(diffMs / 60000);

    // Dưới 2 tiếng (120 phút) hoặc giờ hẹn đã qua
    if (diffMinutes < 120) {
        return {
            canCancel: false,
            isTooLate: true,
            diffMinutes,
            reason: diffMinutes <= 0
                ? 'Lịch hẹn đã đến hoặc đã qua giờ nhận bàn.'
                : `Chỉ còn ${diffMinutes} phút nữa là đến giờ hẹn (dưới 2 tiếng). Vui lòng liên hệ Hotline nếu cần hủy khẩn cấp!`
        };
    }

    return {
        canCancel: true,
        isTooLate: false,
        diffMinutes,
        reason: ''
    };
}

// =========================================================================
// 3. RENDER THẺ ĐẶT BÀN (BOOKING CARD)
// =========================================================================

/**
 * Tạo phần tử DOM Booking Card hoàn chỉnh
 */
function createBookingCardElement(booking, onCancelRequest) {
    const id = booking.id ?? booking.Id ?? booking.reservationId ?? booking.ReservationId ?? '0000';
    const rawTime = booking.reservationTime ?? booking.ReservationTime;
    const guests = booking.numberOfGuests ?? booking.NumberOfGuests ?? 1;
    const status = booking.status ?? booking.Status;
    const tableName = booking.tableName ?? booking.TableName ?? (booking.tableId ? `Bàn ${booking.tableId}` : 'Chưa xếp bàn');
    const customerName = booking.customerName ?? booking.CustomerName ?? '';
    const customerPhone = booking.customerPhone ?? booking.CustomerPhone ?? booking.phone ?? booking.Phone ?? '';
    const note = booking.note ?? booking.Note ?? '';

    const statusMeta = getBookingStatusMeta(status);

    const card = document.createElement('div');
    card.id = `booking-card-${id}`;
    card.className = 'rounded-2xl border border-outline-variant/60 bg-surface-container-lowest p-5 sm:p-6 shadow-sm hover:border-primary/40 transition-all flex flex-col md:flex-row md:items-center justify-between gap-5 relative overflow-hidden';

    // Xác định nút thao tác Hủy bàn
    let actionButtonHtml = '';
    if (statusMeta.canCancelStatus) {
        const policy = checkCancellationPolicy(rawTime);
        if (policy.canCancel) {
            actionButtonHtml = `
                <button type="button" 
                        data-action="cancel" 
                        data-id="${id}" 
                        class="w-full md:w-auto px-4 py-2.5 rounded-xl text-xs font-semibold bg-error/10 hover:bg-error hover:text-on-primary text-error border border-error/20 transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-xs">
                    <span class="material-symbols-outlined text-[16px]">cancel</span>
                    <span>Hủy bàn</span>
                </button>
            `;
        } else {
            actionButtonHtml = `
                <button type="button" 
                        disabled 
                        class="w-full md:w-auto px-4 py-2.5 rounded-xl text-xs font-semibold bg-surface-container-high text-on-surface-variant/60 border border-outline-variant/60 flex items-center justify-center gap-1.5 cursor-not-allowed opacity-60" 
                        title="${policy.reason}">
                    <span class="material-symbols-outlined text-[16px]">phone_in_talk</span>
                    <span>Liên hệ Hotline để hủy (Sát giờ)</span>
                </button>
            `;
        }
    }

    card.innerHTML = `
        <div class="space-y-3 flex-1">
            <div class="flex flex-wrap items-center gap-3">
                <span class="font-extrabold text-base sm:text-lg text-on-surface tracking-tight">
                    Đặt bàn #${id}
                </span>
                <span id="badge-status-${id}" class="px-3 py-1 rounded-full text-xs font-bold border inline-flex items-center gap-1.5 ${statusMeta.badgeClass}">
                    <span class="w-2 h-2 rounded-full ${statusMeta.dotClass}"></span>
                    <span>${statusMeta.label}</span>
                </span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs sm:text-sm text-on-surface-variant pt-1">
                <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-[20px] text-primary shrink-0">calendar_today</span>
                    <div>
                        <p class="text-[11px] text-on-surface-variant/70 font-medium">Thời gian đến</p>
                        <p class="font-semibold text-on-surface">${formatBookingDateTime(rawTime)}</p>
                    </div>
                </div>
                <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-[20px] text-primary shrink-0">group</span>
                    <div>
                        <p class="text-[11px] text-on-surface-variant/70 font-medium">Số lượng khách</p>
                        <p class="font-semibold text-on-surface">${guests} người</p>
                    </div>
                </div>
                <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-[20px] text-primary shrink-0">table_restaurant</span>
                    <div>
                        <p class="text-[11px] text-on-surface-variant/70 font-medium">Khu vực / Bàn</p>
                        <p class="font-semibold text-on-surface">${tableName}</p>
                    </div>
                </div>
            </div>

            ${customerName || customerPhone ? `
                <div class="text-xs text-on-surface-variant/80 flex flex-wrap items-center gap-x-4 gap-y-1 pt-1 border-t border-outline-variant/30">
                    <span>Khách: <strong class="text-on-surface font-semibold">${customerName || 'Quý khách'}</strong> ${customerPhone ? `(${customerPhone})` : ''}</span>
                    ${note ? `<span class="italic text-on-surface-variant/70 truncate max-w-md">Ghi chú: "${note}"</span>` : ''}
                </div>
            ` : ''}
        </div>

        <div id="action-wrapper-${id}" class="flex md:flex-col items-center justify-end md:justify-center gap-2 shrink-0 border-t md:border-t-0 md:border-l border-outline-variant/40 pt-4 md:pt-0 md:pl-6">
            ${actionButtonHtml}
        </div>
    `;

    // Gắn sự kiện Hủy bàn
    const cancelBtn = card.querySelector('[data-action="cancel"]');
    if (cancelBtn) {
        cancelBtn.addEventListener('click', () => {
            if (typeof onCancelRequest === 'function') {
                onCancelRequest(booking);
            }
        });
    }

    return card;
}

/**
 * Cập nhật giao diện card sau khi hủy thành công
 */
function updateCardToCancelledState(bookingId) {
    const card = document.getElementById(`booking-card-${bookingId}`);
    if (!card) return;

    // Đổi badge sang Đã hủy (Xám)
    const badge = document.getElementById(`badge-status-${bookingId}`);
    if (badge) {
        badge.className = 'px-3 py-1 rounded-full text-xs font-bold border inline-flex items-center gap-1.5 bg-gray-100 text-gray-700 border-gray-200';
        badge.innerHTML = `
            <span class="w-2 h-2 rounded-full bg-gray-400"></span>
            <span>Đã hủy</span>
        `;
    }

    // Xóa nút hủy
    const actionWrapper = document.getElementById(`action-wrapper-${bookingId}`);
    if (actionWrapper) {
        actionWrapper.innerHTML = `
            <span class="text-xs font-medium text-on-surface-variant/70 italic flex items-center gap-1">
                <span class="material-symbols-outlined text-[16px]">info</span>
                Đã hủy trực tuyến
            </span>
        `;
    }
}

// =========================================================================
// 4. POPUP CONFIRMATION MODAL HỦY BÀN (THAY THẾ OTP)
// =========================================================================

/**
 * Mở modal xác nhận hủy bàn
 */
function openCancelConfirmModal(booking, onConfirmed) {
    activePendingBooking = booking;
    pendingCancelCallback = onConfirmed;

    const id = booking.id ?? booking.Id ?? booking.reservationId ?? booking.ReservationId;
    const rawTime = booking.reservationTime ?? booking.ReservationTime;
    const tableName = booking.tableName ?? booking.TableName ?? (booking.tableId ? `Bàn ${booking.tableId}` : 'Chưa xếp bàn');

    modalBookingCode.textContent = `Đơn đặt bàn #${id}`;
    modalTimeDisplay.textContent = formatBookingDateTime(rawTime);
    modalTableDisplay.textContent = tableName;

    cancelErrorMsg.classList.add('hidden');
    btnConfirmCancelBooking.disabled = false;
    btnConfirmText.textContent = 'Xác nhận hủy bàn';

    cancelConfirmModal.classList.remove('hidden');
}

function closeCancelConfirmModal() {
    cancelConfirmModal.classList.add('hidden');
    activePendingBooking = null;
    pendingCancelCallback = null;
    isSubmittingCancel = false;
}

// Đóng modal
if (btnCloseConfirmModal) btnCloseConfirmModal.addEventListener('click', closeCancelConfirmModal);
if (btnCancelModalAction) btnCancelModalAction.addEventListener('click', closeCancelConfirmModal);

cancelConfirmModal?.addEventListener('click', (e) => {
    if (e.target === cancelConfirmModal) closeCancelConfirmModal();
});

document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && cancelConfirmModal && !cancelConfirmModal.classList.contains('hidden')) {
        closeCancelConfirmModal();
    }
});

// Xác nhận hủy đặt bàn qua API Backend
btnConfirmCancelBooking?.addEventListener('click', async () => {
    if (isSubmittingCancel || !activePendingBooking) return;

    isSubmittingCancel = true;
    btnConfirmCancelBooking.disabled = true;
    btnConfirmText.textContent = 'Đang xử lý hủy...';
    cancelErrorMsg.classList.add('hidden');

    const id = activePendingBooking.id ?? activePendingBooking.Id ?? activePendingBooking.reservationId ?? activePendingBooking.ReservationId;
    const phone = activePendingBooking.customerPhone ?? activePendingBooking.CustomerPhone ?? activePendingBooking.phone ?? activePendingBooking.Phone ?? '';

    try {
        // GỌI CHÍNH XÁC ENDPOINT BACKEND:
        // - Nếu là Member đã đăng nhập: POST api/reservations/me/{id}/cancel
        // - Nếu là Guest vãng lai: POST api/reservations/cancel?code={code}&phone={phone}
        const isMember = hasLoadedCustomer();
        if (isMember) {
            await bookingApi.cancelMyBooking(id);
        } else {
            await bookingApi.cancelBooking(id, phone);
        }

        // Cập nhật trạng thái bộ nhớ
        activePendingBooking.status = 2;
        activePendingBooking.Status = 2;

        closeCancelConfirmModal();
        updateCardToCancelledState(id);
        showAlert(`Đã hủy đặt bàn #${id} thành công!`, 'success');

        if (typeof pendingCancelCallback === 'function') {
            pendingCancelCallback(id);
        }
    } catch (error) {
        cancelErrorText.textContent = error?.message ?? 'Không thể hủy đặt bàn. Vui lòng liên hệ nhà hàng để được hỗ trợ!';
        cancelErrorMsg.classList.remove('hidden');
        btnConfirmCancelBooking.disabled = false;
        btnConfirmText.textContent = 'Xác nhận hủy bàn';
    } finally {
        isSubmittingCancel = false;
    }
});

// =========================================================================
// 5. KỊCH BẢN 1: KHÁCH VÃNG LAI (TRA CỨU QUA MÃ & SĐT)
// =========================================================================

function setupGuestLookupScenario() {
    pageRoleBadge.textContent = 'Khách vãng lai';
    pageTitle.textContent = 'Tra cứu & Quản lý đặt bàn';
    pageSubtitle.textContent = 'Vui lòng nhập mã đặt bàn và số điện thoại để tra cứu trạng thái hoặc hủy lịch hẹn.';
    headerActions.classList.add('hidden');

    guestLookupSection.classList.remove('hidden');
    guestResultSection.classList.add('hidden');
    memberBookingsSection.classList.add('hidden');

    // Nút quay lại tra cứu đơn khác
    btnGuestSearchAgain?.addEventListener('click', () => {
        currentGuestReservation = null;
        guestResultSection.classList.add('hidden');
        guestLookupSection.classList.remove('hidden');
        guestForm.reset();
        document.getElementById('reservation_code')?.focus();
    });

    // Submit form tra cứu
    guestForm?.addEventListener('submit', async (e) => {
        e.preventDefault();

        let code = guestForm.elements.reservationCode.value.trim();
        const phone = guestForm.elements.phone.value.trim();

        if (!code) {
            showAlert('Vui lòng nhập mã đặt bàn.', 'error');
            guestForm.elements.reservationCode.focus();
            return;
        }

        // Tự động bỏ dấu # nếu người dùng copy cả ký tự #
        if (code.startsWith('#')) {
            code = code.substring(1);
        }

        if (!phone) {
            showAlert('Vui lòng nhập số điện thoại đặt bàn.', 'error');
            guestForm.elements.phone.focus();
            return;
        }

        const digits = phone.replace(/\D/g, '');
        if (digits.length < 9 || digits.length > 11) {
            showAlert('Số điện thoại không hợp lệ (cần từ 9 đến 11 chữ số).', 'error');
            guestForm.elements.phone.focus();
            return;
        }

        const submitBtn = guestForm.querySelector('[data-lookup-submit]');
        if (submitBtn) submitBtn.disabled = true;
        showAlert('Đang tra cứu thông tin đặt bàn từ hệ thống...', 'info');

        try {
            // GỌI CHÍNH XÁC BACKEND: GET api/reservations/lookup?code={code}&phone={phone}
            const reservation = await bookingApi.lookupBooking(code, phone);

            if (!reservation || typeof reservation !== 'object') {
                throw new Error('Không tìm thấy thông tin đặt bàn phù hợp với mã và số điện thoại đã nhập.');
            }

            // Ghi nhớ SĐT để hủy nếu backend không trả trường phone
            if (!reservation.phone && !reservation.customerPhone) {
                reservation.phone = phone;
                reservation.customerPhone = phone;
            }

            currentGuestReservation = reservation;

            // Ẩn form, hiển thị 1 Card kết quả
            guestLookupSection.classList.add('hidden');
            guestResultSection.classList.remove('hidden');
            guestCardContainer.innerHTML = '';

            const card = createBookingCardElement(reservation, (targetBooking) => {
                openCancelConfirmModal(targetBooking, () => {
                    showAlert('Bạn đã hủy lịch hẹn thành công.', 'success');
                });
            });
            guestCardContainer.appendChild(card);
            showAlert('Đã tìm thấy thông tin đặt bàn.', 'success', 3000);

        } catch (error) {
            currentGuestReservation = null;
            showAlert(error?.message ?? 'Không tìm thấy đặt bàn phù hợp. Vui lòng kiểm tra lại thông tin!', 'error');
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    });
}

// =========================================================================
// 6. KỊCH BẢN 2: THÀNH VIÊN (LỊCH SỬ ĐẶT BÀN TỰ ĐỘNG)
// =========================================================================

async function setupMemberScenario() {
    pageRoleBadge.textContent = 'Thành viên GustoPOS';
    pageTitle.textContent = 'Lịch sử đặt bàn của bạn';
    pageSubtitle.textContent = 'Xem và quản lý các lịch hẹn đặt bàn trực tuyến đã đăng ký tại nhà hàng.';
    headerActions.classList.remove('hidden');

    guestLookupSection.classList.add('hidden');
    guestResultSection.classList.add('hidden');
    memberBookingsSection.classList.remove('hidden');

    await loadMemberBookings();

    btnRefreshBookings?.addEventListener('click', async () => {
        btnRefreshBookings.disabled = true;
        await loadMemberBookings(true);
        btnRefreshBookings.disabled = false;
    });
}

/**
 * Tự động gọi API lấy danh sách đặt bàn của thành viên
 * GỌI CHÍNH XÁC BACKEND: GET api/customers/me/bookings (WebProfileController)
 */
async function loadMemberBookings(isManual = false) {
    memberBookingsList.innerHTML = `
        <div class="py-12 text-center space-y-3">
            <div class="inline-block w-8 h-8 border-3 border-primary border-t-transparent rounded-full animate-spin"></div>
            <p class="text-xs font-semibold text-on-surface-variant">Đang tải danh sách lịch hẹn đặt bàn...</p>
        </div>
    `;
    memberBookingsEmpty.classList.add('hidden');

    try {
        const bookings = await bookingApi.getMyBookings();

        renderMemberBookingsList(bookings);

        if (isManual) {
            showAlert('Đã làm mới danh sách đặt bàn.', 'success', 2500);
        }
    } catch (error) {
        memberBookingsList.innerHTML = `
            <div class="rounded-2xl border border-red-200 bg-red-50 p-6 text-center space-y-2 text-error text-xs">
                <span class="material-symbols-outlined text-3xl">error</span>
                <p class="font-bold">Không thể tải danh sách đặt bàn</p>
                <p class="text-on-surface-variant">${error?.message ?? 'Vui lòng kiểm tra lại kết nối mạng.'}</p>
            </div>
        `;
    }
}

/**
 * Render danh sách Booking Cards của Thành viên
 */
function renderMemberBookingsList(bookings) {
    memberBookingsList.innerHTML = '';

    if (!Array.isArray(bookings) || bookings.length === 0) {
        memberBookingsEmpty.classList.remove('hidden');
        return;
    }

    memberBookingsEmpty.classList.add('hidden');

    bookings.forEach((booking) => {
        const card = createBookingCardElement(booking, (targetBooking) => {
            openCancelConfirmModal(targetBooking, () => {
                showAlert('Bạn đã hủy lịch hẹn thành công!', 'success');
            });
        });
        memberBookingsList.appendChild(card);
    });
}

// =========================================================================
// 7. KHỞI TẠO VÀ ĐỒNG BỘ REALTIME VỚI WINFORM
// =========================================================================

async function initBookingLookupPage() {
    setupSessionNavigation();

    // Đồng bộ phiên khách hàng
    try {
        await ensureCustomerSession();
    } catch {
        // Bỏ qua lỗi session nếu chạy vãng lai
    }

    const isMember = hasLoadedCustomer();

    if (isMember) {
        await setupMemberScenario();
    } else {
        setupGuestLookupScenario();
    }

    // ĐỒNG BỘ REALTIME KHI NHÂN VIÊN DUYỆT BÀN HOẶC HỦY TRÊN WINFORM:
    // WinForm bấm 'Xác nhận' -> Backend emit SignalR 'ReservationUpdated'
    realtimeClient.on('ReservationUpdated', async (payload) => {
        console.debug('[SignalR] ReservationUpdated received:', payload);
        const updatedId = payload?.reservationId ?? payload?.ReservationId;

        // Nếu là Member: Tự động tải lại danh sách để đồng bộ trạng thái mới
        if (hasLoadedCustomer()) {
            loadMemberBookings(false);
        } 
        // Nếu là Guest đang xem kết quả của đúng đơn này: Tải lại thông tin để cập nhật tức thì
        else if (currentGuestReservation) {
            const guestId = currentGuestReservation.id ?? currentGuestReservation.Id ?? currentGuestReservation.reservationId ?? currentGuestReservation.ReservationId;
            if (String(guestId) === String(updatedId)) {
                const code = guestForm.elements.reservationCode.value.trim().replace(/^#/, '');
                const phone = guestForm.elements.phone.value.trim();
                if (code && phone) {
                    try {
                        const updated = await bookingApi.lookupBooking(code, phone);
                        if (updated) {
                            currentGuestReservation = updated;
                            guestCardContainer.innerHTML = '';
                            const card = createBookingCardElement(updated, (targetBooking) => {
                                openCancelConfirmModal(targetBooking);
                            });
                            guestCardContainer.appendChild(card);
                            showAlert(`Lịch hẹn #${updatedId} vừa được cập nhật trạng thái mới từ nhà hàng!`, 'info', 4000);
                        }
                    } catch {
                        // Silent catch
                    }
                }
            }
        }
    });

    realtimeClient.start().catch(() => {});
}

// Khởi chạy khi DOM sẵn sàng
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initBookingLookupPage, { once: true });
} else {
    initBookingLookupPage();
}
