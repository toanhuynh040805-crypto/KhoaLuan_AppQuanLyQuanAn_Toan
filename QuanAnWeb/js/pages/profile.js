import { hasCustomerSession, setCurrentCustomer } from '../api/session.js?auth-session-2';
import { ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { profileApi } from '../api/profileApi.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import {
    clearContent,
    renderLoading,
    renderOrderHistory,
    renderStatus,
    renderUserInfo
} from '../components/profileView.js';

const statusElement = document.querySelector('[data-profile-state]');
const infoElement = document.querySelector('[data-profile-info]');
const ordersElement = document.querySelector('[data-profile-orders]');
const bookingHistoryElement = document.querySelector('[data-profile-booking-history]');

// Đã đồng bộ: Hàm render danh sách lịch sử đặt bàn từ mảng dữ liệu API
function renderBookingHistory(container, bookings) {
    if (!container) return;

    if (!Array.isArray(bookings) || bookings.length === 0) {
        renderStatus(container, 'Chưa có đặt bàn nào được ghi nhận trong lịch sử.');
        return;
    }

    const list = document.createElement('div');
    list.className = 'mt-4 space-y-4';

    const statusLabels = {
        0: 'Đã xác nhận',
        1: 'Đã nhận bàn',
        2: 'Đã hủy',
        3: 'Không đến',
        4: 'Hoàn tất'
    };

    const statusColors = {
        0: 'text-amber-600 bg-amber-50 border border-amber-200',
        1: 'text-green-600 bg-green-50 border border-green-200',
        2: 'text-red-600 bg-red-50 border border-red-200',
        3: 'text-red-600 bg-red-50 border border-red-200',
        4: 'text-tertiary bg-secondary-container border border-outline-variant'
    };

    bookings.forEach(booking => {
        const id = booking.id ?? booking.Id ?? booking.reservationId ?? booking.ReservationId;
        const rawTime = booking.reservationTime ?? booking.ReservationTime;
        const guests = booking.numberOfGuests ?? booking.NumberOfGuests;
        const status = booking.status ?? booking.Status;
        const tableName = booking.tableName ?? booking.TableName ?? 'Chưa xếp bàn';

        let formattedDateTime = '—';
        if (rawTime) {
            const dt = new Date(rawTime);
            if (!isNaN(dt.getTime())) {
                formattedDateTime = `${dt.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} ngày ${dt.toLocaleDateString('vi-VN')}`;
            }
        }

        const statusLabel = statusLabels[status] ?? `Trạng thái ${status}`;
        const colorClass = statusColors[status] ?? 'text-on-surface-variant bg-surface-variant';

        const item = document.createElement('div');
        item.className = 'rounded-lg border border-outline-variant bg-surface-container-low p-4 flex flex-col md:flex-row md:items-center justify-between gap-4';
        
        item.innerHTML = `
            <div>
                <h3 class="font-label-lg font-bold text-on-surface">Mã đặt bàn: #${id}</h3>
                <p class="mt-1 text-body-sm text-on-surface-variant flex items-center gap-1">
                    <span class="material-symbols-outlined text-[16px]">calendar_today</span>${formattedDateTime}
                </p>
                <p class="mt-1 text-body-sm text-on-surface-variant flex items-center gap-1">
                    <span class="material-symbols-outlined text-[16px]">group</span>${guests} khách · 📍 ${tableName}
                </p>
            </div>
            <div class="shrink-0">
                <span class="px-3 py-1 rounded-full text-xs font-bold whitespace-nowrap ${colorClass}">${statusLabel}</span>
            </div>
        `;
        list.append(item);
    });

    container.replaceChildren(list);
}

const profileSessionReady = hasCustomerSession() || await ensureCustomerSession();
if (!profileSessionReady) {
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;
    const loginUrl = new URL('../auth/login_register.html', location.href);
    loginUrl.searchParams.set('returnUrl', returnUrl);
    window.location.replace(loginUrl);
} else if (statusElement && infoElement && ordersElement) {
    setupSessionNavigation();
    
    let sessionExpired = false;
    let historyRefreshPending = false;
    globalThis.addEventListener('gusto:unauthorized', () => {
        sessionExpired = true;
        clearContent(infoElement);
        clearContent(ordersElement);
        if (bookingHistoryElement) clearContent(bookingHistoryElement);
        renderStatus(statusElement, 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để xem dữ liệu cá nhân.', {
            loginHref: '../auth/login_register.html'
        });
    });

    async function loadProfile() {
        renderStatus(statusElement, 'Đang tải hồ sơ khách hàng.');
        renderLoading(infoElement, 'Đang tải thông tin cá nhân...');
        renderLoading(ordersElement, 'Đang tải lịch sử đơn hàng...');
        if (bookingHistoryElement) renderLoading(bookingHistoryElement, 'Đang tải lịch sử đặt bàn...');

        // Đã đồng bộ: Gọi API lấy cả lịch sử đặt bàn từ Backend
        const [profileResult, historyResult, bookingResult] = await Promise.allSettled([
            profileApi.getMyProfile(),
            profileApi.getMyOrderHistory(),
            profileApi.getMyBookingHistory ? profileApi.getMyBookingHistory() : Promise.resolve([])
        ]);
        if (sessionExpired) return;

        // Xử lý Profile
        if (profileResult.status === 'fulfilled' && profileResult.value && typeof profileResult.value === 'object') {
            setCurrentCustomer(profileResult.value);
            renderUserInfo(infoElement, profileResult.value);
            const headerName = document.querySelector('#header-user-name');
            if (headerName) headerName.textContent = profileResult.value.fullName ?? profileResult.value.FullName ?? 'Khách hàng';
        } else {
            renderStatus(infoElement, profileResult.reason?.message ?? 'Không thể tải hồ sơ khách hàng.', {
                onRetry: loadProfile
            });
        }

        // Xử lý Lịch sử đơn hàng
        if (historyResult.status === 'fulfilled') {
            const history = Array.isArray(historyResult.value)
                ? historyResult.value
                : historyResult.value?.Orders ?? historyResult.value?.orders ?? [];
            renderOrderHistory(ordersElement, history);
        } else {
            renderStatus(ordersElement, historyResult.reason?.message ?? 'Không thể tải lịch sử đơn hàng.', {
                onRetry: loadProfile
            });
        }

        // Xử lý Lịch sử đặt bàn
        if (bookingHistoryElement) {
            if (bookingResult.status === 'fulfilled') {
                const bookings = Array.isArray(bookingResult.value) ? bookingResult.value : [];
                renderBookingHistory(bookingHistoryElement, bookings);
            } else {
                renderStatus(bookingHistoryElement, bookingResult.reason?.message ?? 'Không thể tải lịch sử đặt bàn.', {
                    onRetry: loadProfile
                });
            }
        }

        clearContent(statusElement);
    }

    async function refreshOrderHistory() {
        if (sessionExpired || historyRefreshPending) return;
        historyRefreshPending = true;
        try {
            const history = await profileApi.getMyOrderHistory();
            if (sessionExpired) return;
            renderOrderHistory(
                ordersElement,
                Array.isArray(history) ? history : history?.Orders ?? history?.orders ?? []
            );
        } catch (error) {
            if (!sessionExpired) {
                renderStatus(ordersElement, error?.message ?? 'Không thể làm mới lịch sử đơn hàng.', {
                    onRetry: refreshOrderHistory
                });
            }
        } finally {
            historyRefreshPending = false;
        }
    }

    function refreshWhenVisible() {
        if (!document.hidden) void refreshOrderHistory();
    }

    globalThis.addEventListener('focus', refreshOrderHistory);
    document.addEventListener('visibilitychange', refreshWhenVisible);
    loadProfile();
} 