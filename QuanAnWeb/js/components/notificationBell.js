import { notificationApi } from '../api/notificationApi.js?auth-session-2';
import { getCurrentCustomer, hasCustomerSession, getCustomerToken } from '../api/session.js?auth-session-2';
import { API_ORIGIN } from '../api/config.js';
import { realtimeClient } from '../realtime/realtimeClient.js';

let currentNotifications = [];
let currentUnreadCount = 0;
let currentSkip = 0;
const TAKE = 20;
let hasMoreNotifications = false;
let isDropdownOpen = false;
let isInitialFetchDone = false;

// 1. Tiện ích xác định đường dẫn tương đối từ trang hiện tại
function getBasePath() {
    const pathname = window.location.pathname.replace(/\\/g, '/');
    if (pathname.includes('/pages/')) {
        return '../..';
    }
    return '.';
}

function resolveTargetUrl(item) {
    const base = getBasePath();
    const entityType = item.relatedEntityType || item.RelatedEntityType;
    const entityId = item.relatedEntityId || item.RelatedEntityId;

    if (entityType === 'Order') {
        return `${base}/pages/profile/index.html#orders`;
    }
    if (entityType === 'Reservation') {
        return `${base}/pages/profile/index.html#bookings`;
    }
    if (entityType === 'Review') {
        return `${base}/pages/reviews/index.html`;
    }
    if (entityType === 'KhuyenMai') {
        return `${base}/pages/promotions/index.html`;
    }
    return `${base}/pages/profile/index.html`;
}

// 2. Định dạng thời gian tương đối thân thiện
function formatTime(dateStr) {
    if (!dateStr) return '';
    try {
        const date = new Date(dateStr);
        if (isNaN(date.getTime())) return '';
        const now = new Date();
        const diffSec = Math.floor((now - date) / 1000);

        if (diffSec < 60) return 'Vừa xong';
        const diffMin = Math.floor(diffSec / 60);
        if (diffMin < 60) return `${diffMin} phút trước`;
        const diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return `${diffHour} giờ trước`;
        const diffDays = Math.floor(diffHour / 24);
        if (diffDays <= 7) return `${diffDays} ngày trước`;

        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const year = date.getFullYear();
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        return `${hours}:${minutes} ${day}/${month}/${year}`;
    } catch {
        return '';
    }
}

// 3. Icon và màu sắc tương ứng từng loại thông báo
function getNotificationMeta(type) {
    switch (type) {
        case 'DatBan':
            return {
                icon: 'table_restaurant',
                bg: 'bg-sky-50 dark:bg-sky-950/40 text-sky-600 dark:text-sky-400 border border-sky-200/60'
            };
        case 'DonHang':
            return {
                icon: 'restaurant_menu',
                bg: 'bg-amber-50 dark:bg-amber-950/40 text-amber-600 dark:text-amber-400 border border-amber-200/60'
            };
        case 'KhuyenMai':
            return {
                icon: 'local_offer',
                bg: 'bg-purple-50 dark:bg-purple-950/40 text-purple-600 dark:text-purple-400 border border-purple-200/60'
            };
        case 'HeThong':
            return {
                icon: 'verified',
                bg: 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 border border-emerald-200/60'
            };
        case 'TaiKhoan':
            return {
                icon: 'person',
                bg: 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border border-slate-200/60'
            };
        default:
            return {
                icon: 'notifications',
                bg: 'bg-primary/10 text-primary border border-primary/20'
            };
    }
}

// 4. Đảm bảo phần tử huy hiệu thông báo tồn tại trên nút chuông
function ensureNotificationBadge(bellBtn) {
    if (!bellBtn) return null;
    let badge = bellBtn.querySelector('[data-notification-badge], #notification-badge');
    if (!badge) {
        badge = document.createElement('span');
        badge.id = 'notification-badge';
        badge.setAttribute('data-notification-badge', '');
        badge.className = 'absolute -top-1 -right-1 min-w-[18px] h-[18px] px-1 bg-[#ba1a1a] text-white text-[10px] font-extrabold rounded-full flex items-center justify-center border-2 border-white pointer-events-none shadow-xs hidden';
        badge.setAttribute('aria-live', 'polite');
        bellBtn.appendChild(badge);
    } else {
        if (!badge.hasAttribute('data-notification-badge')) {
            badge.setAttribute('data-notification-badge', '');
        }
    }
    return badge;
}

// 5. Cập nhật huy hiệu số lượng chưa đọc trên tất cả nút chuông
function updateBadges(count) {
    currentUnreadCount = Math.max(0, Number(count) || 0);
    const text = currentUnreadCount > 99 ? '99+' : (currentUnreadCount > 0 ? String(currentUnreadCount) : '');
    
    document.querySelectorAll('[data-notification-badge], #notification-badge').forEach(badge => {
        if (currentUnreadCount > 0) {
            badge.textContent = text;
            badge.classList.remove('hidden');
        } else {
            badge.textContent = '';
            badge.classList.add('hidden');
        }
    });

    const unreadPill = document.getElementById('notification-unread-pill');
    if (unreadPill) {
        if (currentUnreadCount > 0) {
            unreadPill.textContent = `${currentUnreadCount} mới`;
            unreadPill.classList.remove('hidden');
        } else {
            unreadPill.classList.add('hidden');
        }
    }

    const markAllBtn = document.getElementById('btn-notification-mark-all');
    if (markAllBtn) {
        markAllBtn.disabled = currentUnreadCount === 0;
        markAllBtn.classList.toggle('opacity-50', currentUnreadCount === 0);
        markAllBtn.classList.toggle('cursor-not-allowed', currentUnreadCount === 0);
    }
}

// 6. Tải số lượng chưa đọc từ API
export async function refreshUnreadCount() {
    if (!hasCustomerSession()) {
        updateBadges(0);
        return;
    }
    try {
        const res = await notificationApi.getUnreadCount();
        const count = res?.count ?? res?.Count ?? 0;
        updateBadges(count);
    } catch {
        // im lặng bỏ qua nếu mạng chập chờn
    }
}

// 7. Xây dựng cấu trúc Popover DOM nếu chưa có
function ensureDropdownContainer(anchorBtn) {
    let dropdown = document.getElementById('notification-popover');
    if (dropdown) return dropdown;

    let parentWrapper = anchorBtn.closest('#notification-bell-wrapper') || anchorBtn.parentElement;
    if (!parentWrapper.classList.contains('relative')) {
        parentWrapper.classList.add('relative');
    }

    dropdown = document.createElement('div');
    dropdown.id = 'notification-popover';
    dropdown.className = 'hidden absolute right-0 top-full mt-2 w-80 sm:w-96 max-w-[calc(100vw-1.5rem)] bg-surface-container-lowest rounded-2xl shadow-xl border border-outline-variant/60 z-50 overflow-hidden flex flex-col transition-all duration-200 transform origin-top-right';
    dropdown.setAttribute('role', 'region');
    dropdown.setAttribute('aria-label', 'Danh sách thông báo');

    dropdown.innerHTML = `
        <!-- Header -->
        <div class="px-4 py-3.5 border-b border-outline-variant/40 flex items-center justify-between bg-surface-container-low/50">
            <div class="flex items-center gap-2">
                <span class="font-bold text-sm text-on-surface">Thông báo</span>
                <span id="notification-unread-pill" class="hidden px-2 py-0.5 rounded-full text-[11px] font-bold bg-primary text-on-primary"></span>
            </div>
            <button type="button" id="btn-notification-mark-all" class="text-xs font-semibold text-primary hover:underline flex items-center gap-1 disabled:opacity-50 disabled:no-underline transition" title="Đánh dấu tất cả đã đọc">
                <span class="material-symbols-outlined text-[16px]">done_all</span>
                <span>Đã đọc tất cả</span>
            </button>
        </div>

        <!-- Body / Danh sách thông báo -->
        <div id="notification-items-container" class="max-h-[380px] overflow-y-auto divide-y divide-outline-variant/20 custom-scrollbar">
            <!-- Loading state mặc định -->
            <div class="p-6 text-center text-xs text-on-surface-variant flex flex-col items-center gap-2">
                <span class="material-symbols-outlined animate-spin text-primary text-2xl">progress_activity</span>
                <span>Đang tải thông báo...</span>
            </div>
        </div>

        <!-- Footer / Phân trang tải thêm -->
        <div id="notification-footer" class="p-2 border-t border-outline-variant/30 text-center bg-surface-container-low/30 hidden">
            <button type="button" id="btn-notification-load-more" class="w-full py-2 text-xs font-semibold text-primary hover:bg-surface-container-high rounded-xl transition">
                Xem thêm thông báo cũ hơn
            </button>
        </div>
    `;

    parentWrapper.appendChild(dropdown);

    // Bắt sự kiện Đánh dấu tất cả đã đọc
    dropdown.querySelector('#btn-notification-mark-all')?.addEventListener('click', async (e) => {
        e.stopPropagation();
        if (currentUnreadCount === 0) return;
        try {
            await notificationApi.markAllRead();
            currentNotifications.forEach(n => { n.isRead = true; n.IsRead = true; });
            updateBadges(0);
            renderNotificationItems(currentNotifications, false);
        } catch (err) {
            console.error('[NotificationBell] Mark all read error:', err);
        }
    });

    // Bắt sự kiện Tải thêm
    dropdown.querySelector('#btn-notification-load-more')?.addEventListener('click', async (e) => {
        e.stopPropagation();
        await fetchNotifications(false);
    });

    return dropdown;
}

// 7. Render danh sách thông báo vào popup
function renderNotificationItems(items, append = false) {
    const container = document.getElementById('notification-items-container');
    const footer = document.getElementById('notification-footer');
    if (!container) return;

    if (!append) {
        container.innerHTML = '';
    }

    if (!hasCustomerSession()) {
        const loginUrl = `${getBasePath()}/pages/auth/login_register.html`;
        container.innerHTML = `
            <div class="p-8 text-center space-y-3">
                <div class="w-12 h-12 rounded-full bg-surface-variant flex items-center justify-center mx-auto text-on-surface-variant">
                    <span class="material-symbols-outlined text-2xl">lock</span>
                </div>
                <div>
                    <h5 class="text-sm font-bold text-on-surface">Đăng nhập tài khoản</h5>
                    <p class="text-xs text-on-surface-variant mt-1">Đăng nhập để theo dõi trạng thái đơn hàng và lịch đặt bàn của bạn.</p>
                </div>
                <a href="${loginUrl}" class="inline-flex items-center gap-1.5 bg-primary text-on-primary text-xs font-semibold px-4 py-2 rounded-xl hover:opacity-95 shadow-sm transition">
                    <span class="material-symbols-outlined text-[16px]">login</span> Đăng nhập ngay
                </a>
            </div>
        `;
        if (footer) footer.classList.add('hidden');
        return;
    }

    if (!items || items.length === 0) {
        container.innerHTML = `
            <div class="p-8 text-center space-y-2">
                <div class="w-12 h-12 rounded-full bg-surface-variant flex items-center justify-center mx-auto text-on-surface-variant">
                    <span class="material-symbols-outlined text-2xl">notifications_none</span>
                </div>
                <h5 class="text-sm font-bold text-on-surface">Chưa có thông báo nào</h5>
                <p class="text-xs text-on-surface-variant max-w-xs mx-auto">Các cập nhật về đơn hàng, đặt bàn và ưu đãi của bạn sẽ xuất hiện tại đây.</p>
            </div>
        `;
        if (footer) footer.classList.add('hidden');
        return;
    }

    const fragment = document.createDocumentFragment();

    items.forEach(item => {
        const id = item.id || item.Id || item.notificationId || item.NotificationId;
        const title = item.title || item.Title || 'Thông báo';
        const message = item.message || item.Message || item.content || item.Content || '';
        const type = item.notificationType || item.NotificationType || '';
        const isRead = Boolean(item.isRead ?? item.IsRead);
        const createdAt = item.createdAt || item.CreatedAt;
        const meta = getNotificationMeta(type);

        const el = document.createElement('div');
        el.className = `p-3.5 flex items-start gap-3 transition-colors cursor-pointer select-none ${
            isRead ? 'hover:bg-surface-container-high' : 'bg-primary/[0.04] hover:bg-primary/[0.08]'
        }`;
        el.setAttribute('data-notification-id', String(id));
        el.setAttribute('tabindex', '0');
        el.setAttribute('role', 'button');

        el.innerHTML = `
            <div class="w-9 h-9 rounded-xl ${meta.bg} flex items-center justify-center shrink-0 mt-0.5">
                <span class="material-symbols-outlined text-[20px]">${meta.icon}</span>
            </div>
            <div class="flex-1 min-w-0 space-y-1">
                <div class="flex items-center justify-between gap-2">
                    <h6 class="text-xs font-bold text-on-surface truncate">${title}</h6>
                    ${!isRead ? '<span class="w-2 h-2 rounded-full bg-primary shrink-0 ring-2 ring-surface" title="Chưa đọc"></span>' : ''}
                </div>
                <p class="text-[12px] text-on-surface-variant line-clamp-2 leading-relaxed">${message}</p>
                <span class="text-[10px] text-on-surface-variant/70 block pt-0.5">${formatTime(createdAt)}</span>
            </div>
        `;

        // Sự kiện nhấn vào thông báo: Đánh dấu đã đọc + điều hướng
        const handleOpenNotification = async () => {
            if (!isRead) {
                item.isRead = true;
                item.IsRead = true;
                el.classList.remove('bg-primary/[0.04]', 'hover:bg-primary/[0.08]');
                el.classList.add('hover:bg-surface-container-high');
                el.querySelector('.bg-primary.w-2')?.remove();
                updateBadges(Math.max(0, currentUnreadCount - 1));
                try {
                    await notificationApi.markRead(id);
                } catch {
                    // im lặng bỏ qua nếu lỗi mạng
                }
            }
            toggleDropdown(false);
            const targetUrl = resolveTargetUrl(item);
            if (targetUrl) {
                window.location.assign(targetUrl);
            }
        };

        el.addEventListener('click', handleOpenNotification);
        el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                handleOpenNotification();
            }
        });

        fragment.appendChild(el);
    });

    container.appendChild(fragment);

    if (footer) {
        footer.classList.toggle('hidden', !hasMoreNotifications);
    }
}

// 8. Tải danh sách thông báo từ Backend API
async function fetchNotifications(reset = true) {
    if (!hasCustomerSession()) {
        renderNotificationItems([]);
        return;
    }

    if (reset) {
        currentSkip = 0;
        currentNotifications = [];
        const container = document.getElementById('notification-items-container');
        if (container) {
            container.innerHTML = `
                <div class="p-6 text-center text-xs text-on-surface-variant flex flex-col items-center gap-2">
                    <span class="material-symbols-outlined animate-spin text-primary text-2xl">progress_activity</span>
                    <span>Đang tải thông báo...</span>
                </div>
            `;
        }
    }

    try {
        const res = await notificationApi.getMyNotifications(currentSkip, TAKE);
        const items = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
        const total = res?.total ?? res?.Total ?? items.length;
        const unread = res?.unreadCount ?? res?.UnreadCount ?? currentUnreadCount;
        
        updateBadges(unread);

        if (reset) {
            currentNotifications = items;
        } else {
            currentNotifications = [...currentNotifications, ...items];
        }

        currentSkip = currentNotifications.length;
        hasMoreNotifications = currentSkip < total;

        renderNotificationItems(reset ? currentNotifications : items, !reset);
        isInitialFetchDone = true;
    } catch (err) {
        const container = document.getElementById('notification-items-container');
        if (container) {
            container.innerHTML = `
                <div class="p-6 text-center space-y-3 text-xs">
                    <span class="material-symbols-outlined text-3xl text-error">error_outline</span>
                    <p class="text-on-surface-variant">${err.message || 'Không thể tải thông báo lúc này.'}</p>
                    <button type="button" id="btn-retry-notifications" class="inline-flex items-center gap-1.5 bg-surface-container text-primary font-semibold px-3 py-1.5 rounded-lg hover:bg-surface-variant transition">
                        <span class="material-symbols-outlined text-sm">refresh</span> Thử lại
                    </button>
                </div>
            `;
            container.querySelector('#btn-retry-notifications')?.addEventListener('click', () => fetchNotifications(true));
        }
    }
}

// 9. Bật / Tắt Dropdown Popover
export function toggleDropdown(forceState) {
    const dropdown = document.getElementById('notification-popover');
    const bellBtn = document.getElementById('btn-notification-bell') || document.querySelector('[data-notification-bell]');
    if (!dropdown || !bellBtn) return;

    isDropdownOpen = forceState !== undefined ? forceState : !isDropdownOpen;

    if (isDropdownOpen) {
        dropdown.classList.remove('hidden');
        bellBtn.setAttribute('aria-expanded', 'true');
        bellBtn.classList.add('text-primary', 'bg-surface-container-high');
        fetchNotifications(true);
    } else {
        dropdown.classList.add('hidden');
        bellBtn.setAttribute('aria-expanded', 'false');
        bellBtn.classList.remove('text-primary', 'bg-surface-container-high');
    }
}

// 11. Đăng ký nhận thông báo thời gian thực qua RealtimeClient
let isRealtimeSubscribed = false;

function setupRealtimeNotificationListener() {
    if (isRealtimeSubscribed) return;
    isRealtimeSubscribed = true;

    realtimeClient.on('NewCustomerNotification', (newNoti) => {
        // Tránh cộng trùng nếu thông báo này đã được ghi nhận trước đó
        const notiId = newNoti?.id ?? newNoti?.Id ?? newNoti?.notificationId ?? newNoti?.NotificationId;
        if (notiId && currentNotifications.some(n => (n.id ?? n.Id ?? n.notificationId ?? n.NotificationId) === notiId)) {
            return;
        }

        // Tăng số lượng chưa đọc ngay lập tức (optimistic UI)
        updateBadges(currentUnreadCount + 1);

        // Thêm vào đầu mảng thông báo nếu danh sách đã tải
        if (isInitialFetchDone) {
            currentNotifications.unshift(newNoti);
            renderNotificationItems(currentNotifications, false);
        }

        // Hiệu ứng rung nhẹ nút chuông
        const bellBtn = document.getElementById('btn-notification-bell') || document.querySelector('[data-notification-bell]');
        if (bellBtn) {
            bellBtn.classList.add('animate-bounce');
            setTimeout(() => bellBtn.classList.remove('animate-bounce'), 1000);
        }
    });

    realtimeClient.on('sync', () => {
        if (hasCustomerSession()) {
            refreshUnreadCount();
        }
    });
}

// 12. Khởi tạo toàn bộ Module Thông báo Chuông
export function setupNotificationBell() {
    const bellBtn = document.getElementById('btn-notification-bell') || document.querySelector('[data-notification-bell]');
    if (!bellBtn) return;

    // Đảm bảo phần tử huy hiệu thông báo tồn tại và đồng bộ selector
    ensureNotificationBadge(bellBtn);

    // Đảm bảo có wrapper và dropdown popover
    ensureDropdownContainer(bellBtn);

    // Gắn sự kiện click mở/đóng chuông
    if (bellBtn.dataset.bellReady !== 'true') {
        bellBtn.dataset.bellReady = 'true';
        bellBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleDropdown();
        });
    }

    // Đóng khi click bên ngoài popover hoặc nhấn Escape (chỉ gắn 1 lần trên document)
    if (document.documentElement.dataset.notificationBellDocListenersReady !== 'true') {
        document.documentElement.dataset.notificationBellDocListenersReady = 'true';

        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('notification-popover');
            const btn = document.getElementById('btn-notification-bell') || document.querySelector('[data-notification-bell]');
            if (isDropdownOpen && dropdown && !dropdown.contains(e.target) && btn && !btn.contains(e.target)) {
                toggleDropdown(false);
            }
        });

        document.addEventListener('keydown', (e) => {
            const btn = document.getElementById('btn-notification-bell') || document.querySelector('[data-notification-bell]');
            if (e.key === 'Escape' && isDropdownOpen) {
                toggleDropdown(false);
                btn?.focus();
            }
        });
    }

    // Tải số lượng chưa đọc ban đầu từ Backend API
    refreshUnreadCount();

    // Khởi tạo kết nối realtime chung
    setupRealtimeNotificationListener();

    // Lắng nghe sự kiện đăng xuất / hết phiên để dọn dẹp
    if (document.documentElement.dataset.notificationListenerReady !== 'true') {
        document.documentElement.dataset.notificationListenerReady = 'true';
        
        globalThis.addEventListener('gusto:unauthorized', () => {
            updateBadges(0);
            currentNotifications = [];
            toggleDropdown(false);
        });

        globalThis.addEventListener('gusto:session-updated', () => {
            if (hasCustomerSession()) {
                refreshUnreadCount();
            } else {
                updateBadges(0);
                currentNotifications = [];
                toggleDropdown(false);
            }
        });
    }
}

