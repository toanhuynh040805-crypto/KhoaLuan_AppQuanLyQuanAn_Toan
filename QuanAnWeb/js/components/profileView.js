import { resolveAssetUrl } from '../api/config.js';

export function formatDate(value) {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('vi-VN', {
        hour: '2-digit',
        minute: '2-digit',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    }).format(date);
}

export function formatAmount(value) {
    if (value === null || value === undefined || value === '') return '0đ';
    const amount = Number(value);
    return Number.isFinite(amount)
        ? `${new Intl.NumberFormat('vi-VN').format(amount)}đ`
        : '0đ';
}

export function formatPoints(value) {
    if (value === null || value === undefined || value === '') return '0';
    const pts = Number(value);
    return Number.isFinite(pts) ? new Intl.NumberFormat('vi-VN').format(pts) : '0';
}

export function getMemberLevelMeta(level, currentPoints = 0) {
    const pts = Number(currentPoints) || 0;
    switch (level) {
        case 'Bac':
        case 1:
            return {
                label: 'Hạng Bạc',
                badgeClass: 'bg-slate-100 text-slate-800 border border-slate-300',
                cardTier: 'Bạc',
                nextTierName: 'Hạng Vàng',
                nextTierPoints: 400,
                neededPoints: Math.max(0, 400 - pts),
                progressPercent: Math.min(100, Math.max(0, Math.round(((pts - 100) / (400 - 100)) * 100))),
                benefits: [
                    'Tích lũy 1 điểm cho mỗi 10.000đ chi tiêu',
                    'Đổi điểm giảm giá: 1 điểm = 1.000đ khi thanh toán',
                    'Ưu tiên xác nhận đặt bàn trong khung giờ cao điểm'
                ]
            };
        case 'Vang':
        case 2:
            return {
                label: 'Hạng Vàng',
                badgeClass: 'bg-amber-50 text-amber-800 border border-amber-300',
                cardTier: 'Vàng',
                nextTierName: 'Hạng Kim cương',
                nextTierPoints: 1000,
                neededPoints: Math.max(0, 1000 - pts),
                progressPercent: Math.min(100, Math.max(0, Math.round(((pts - 400) / (1000 - 400)) * 100))),
                benefits: [
                    'Tích lũy 1 điểm cho mỗi 10.000đ chi tiêu',
                    'Đổi điểm giảm giá: 1 điểm = 1.000đ khi thanh toán',
                    'Ưu tiên chọn bàn view đẹp và phòng riêng',
                    'Nhận các ưu đãi đặc quyền theo mùa từ GustoPOS'
                ]
            };
        case 'KimCuong':
        case 3:
            return {
                label: 'Hạng Kim cương',
                badgeClass: 'bg-cyan-50 text-cyan-800 border border-cyan-300',
                cardTier: 'Kim cương',
                nextTierName: null,
                nextTierPoints: 1000,
                neededPoints: 0,
                progressPercent: 100,
                benefits: [
                    'Hạng thành viên cao cấp nhất tại GustoPOS',
                    'Tích lũy 1 điểm cho mỗi 10.000đ chi tiêu',
                    'Đổi điểm giảm giá: 1 điểm = 1.000đ khi thanh toán',
                    'Đặc quyền đặt bàn VIP ưu tiên tối đa',
                    'Hỗ trợ quản lý và dịch vụ khách hàng 24/7'
                ]
            };
        case 'ThanhVien':
        case 0:
        default:
            return {
                label: 'Thành viên',
                badgeClass: 'bg-primary/10 text-primary border border-primary/20',
                cardTier: 'Thành viên',
                nextTierName: 'Hạng Bạc',
                nextTierPoints: 100,
                neededPoints: Math.max(0, 100 - pts),
                progressPercent: Math.min(100, Math.max(0, Math.round((pts / 100) * 100))),
                benefits: [
                    'Tích lũy 1 điểm cho mỗi 10.000đ chi tiêu',
                    'Đổi điểm giảm giá trực tiếp vào hóa đơn (1 điểm = 1.000đ)',
                    'Lưu lịch sử đơn hàng và tham gia đánh giá món ăn'
                ]
            };
    }
}

export function getOrderStatusMeta(status) {
    switch (status) {
        case 'DangMo':
        case 0:
            return {
                label: 'Đang mở',
                badgeClass: 'bg-amber-50 text-amber-800 border border-amber-200'
            };
        case 'DaThanhToan':
        case 1:
            return {
                label: 'Đã thanh toán',
                badgeClass: 'bg-emerald-50 text-emerald-800 border border-emerald-200'
            };
        case 'DaHuy':
        case 2:
            return {
                label: 'Đã hủy',
                badgeClass: 'bg-rose-50 text-rose-800 border border-rose-200'
            };
        default:
            return {
                label: String(status || '—'),
                badgeClass: 'bg-surface-variant text-on-surface-variant border border-outline-variant'
            };
    }
}

export function getPaymentStatusMeta(status) {
    switch (status) {
        case 'ThanhCong':
            return {
                label: 'Đã thanh toán',
                badgeClass: 'bg-emerald-50 text-emerald-800 border border-emerald-200'
            };
        case 'ThanhToanMotPhan':
            return {
                label: 'Thanh toán một phần',
                badgeClass: 'bg-amber-50 text-amber-800 border border-amber-200'
            };
        case 'ThatBai':
            return {
                label: 'Thanh toán thất bại',
                badgeClass: 'bg-rose-100 text-rose-900 border border-rose-300'
            };
        case 'HoanTien':
            return {
                label: 'Đã hoàn tiền',
                badgeClass: 'bg-purple-50 text-purple-800 border border-purple-200'
            };
        case 'DaHuy':
            return {
                label: 'Đã hủy',
                badgeClass: 'bg-neutral-100 text-neutral-700 border border-neutral-300'
            };
        case 'ChuaThanhToan':
        default:
            return {
                label: 'Chưa thanh toán',
                badgeClass: 'bg-amber-50 text-amber-800 border border-amber-200'
            };
    }
}

export function getBookingStatusMeta(status) {
    switch (status) {
        case 'ChoXacNhan':
        case 0:
            return {
                label: 'Chờ xác nhận',
                badgeClass: 'bg-amber-50 text-amber-800 border border-amber-200',
                icon: 'schedule'
            };
        case 'DaDat':
        case 'DaXacNhan':
        case 1:
            return {
                label: 'Đã xác nhận',
                badgeClass: 'bg-emerald-50 text-emerald-800 border border-emerald-200',
                icon: 'check_circle'
            };
        case 'DaNhanBan':
        case 2:
            return {
                label: 'Đã nhận bàn',
                badgeClass: 'bg-blue-50 text-blue-800 border border-blue-200',
                icon: 'table_restaurant'
            };
        case 'DaHuy':
        case 3:
            return {
                label: 'Đã hủy',
                badgeClass: 'bg-rose-50 text-rose-800 border border-rose-200',
                icon: 'cancel'
            };
        case 'KhongDen':
        case 4:
            return {
                label: 'Không đến',
                badgeClass: 'bg-slate-100 text-slate-700 border border-slate-300',
                icon: 'event_busy'
            };
        case 'HoanTat':
        case 5:
            return {
                label: 'Hoàn tất',
                badgeClass: 'bg-teal-50 text-teal-800 border border-teal-200',
                icon: 'task_alt'
            };
        default:
            return {
                label: String(status || '—'),
                badgeClass: 'bg-surface-variant text-on-surface-variant border border-outline-variant',
                icon: 'info'
            };
    }
}

export function getKitchenStatusMeta(status) {
    switch (status) {
        case 'MoiGoi':
        case 0:
            return { label: 'Mới gọi', badgeClass: 'bg-slate-100 text-slate-700' };
        case 'DangCheBien':
        case 1:
            return { label: 'Đang nấu', badgeClass: 'bg-amber-100 text-amber-800' };
        case 'DaCheBien':
        case 2:
            return { label: 'Đã nấu xong', badgeClass: 'bg-blue-100 text-blue-800' };
        case 'DaPhucVu':
        case 3:
            return { label: 'Đã phục vụ', badgeClass: 'bg-emerald-100 text-emerald-800' };
        case 'DaHuy':
        case 4:
            return { label: 'Đã hủy', badgeClass: 'bg-rose-100 text-rose-800' };
        default:
            return { label: String(status || '—'), badgeClass: 'bg-surface-variant text-on-surface-variant' };
    }
}

export function getModerationStatusMeta(status) {
    switch (status) {
        case 'Public':
            return { label: 'Đã duyệt & công khai', badgeClass: 'bg-emerald-50 text-emerald-800 border border-emerald-200', icon: 'verified' };
        case 'Pending':
            return { label: 'Đang chờ duyệt', badgeClass: 'bg-amber-50 text-amber-800 border border-amber-200', icon: 'schedule' };
        case 'Rejected':
            return { label: 'Cần sửa từ ngữ', badgeClass: 'bg-rose-50 text-rose-800 border border-rose-200', icon: 'cancel' };
        case 'Hidden':
            return { label: 'Đã ẩn', badgeClass: 'bg-neutral-100 text-neutral-700 border border-neutral-300', icon: 'visibility_off' };
        default:
            return { label: String(status || '—'), badgeClass: 'bg-surface-variant text-on-surface-variant', icon: 'help' };
    }
}

export function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center gap-3 px-4 py-3 rounded-2xl shadow-xl border text-sm font-medium transition-all duration-300 transform translate-y-2 opacity-0 ${
        type === 'success' ? 'bg-emerald-50 text-emerald-900 border-emerald-200' :
        type === 'error' ? 'bg-rose-50 text-rose-900 border-rose-200' :
        type === 'warning' ? 'bg-amber-50 text-amber-900 border-amber-200' :
        'bg-surface-container-lowest text-on-surface border-outline-variant'
    }`;

    const iconName = type === 'success' ? 'check_circle' :
        type === 'error' ? 'error' :
        type === 'warning' ? 'warning' : 'info';

    toast.innerHTML = `
        <span class="material-symbols-outlined text-[20px] shrink-0 ${
            type === 'success' ? 'text-emerald-600' :
            type === 'error' ? 'text-rose-600' :
            type === 'warning' ? 'text-amber-600' : 'text-primary'
        }" style="font-variation-settings: 'FILL' 1;">${iconName}</span>
        <span class="flex-1">${message}</span>
    `;

    container.appendChild(toast);
    requestAnimationFrame(() => {
        toast.classList.remove('translate-y-2', 'opacity-0');
    });

    setTimeout(() => {
        toast.classList.add('opacity-0', 'translate-y-2');
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

export function renderLoading(container, message = 'Đang tải dữ liệu...') {
    if (!container) return;
    container.innerHTML = `
        <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center animate-pulse space-y-3">
            <div class="w-8 h-8 rounded-full border-2 border-primary border-t-transparent animate-spin mx-auto"></div>
            <p class="text-xs text-on-surface-variant font-medium">${message}</p>
        </div>
    `;
}

export function clearContent(container) {
    if (container) container.innerHTML = '';
}

export function renderStatus(container, message, { onRetry, loginHref } = {}) {
    if (!container) return;
    const box = document.createElement('div');
    box.className = 'rounded-2xl border border-outline-variant/60 bg-surface-container-low p-6 text-center space-y-3 text-xs md:text-sm text-on-surface-variant';
    box.innerHTML = `<p class="font-medium text-on-surface">${message}</p>`;

    if (onRetry) {
        const retryBtn = document.createElement('button');
        retryBtn.type = 'button';
        retryBtn.className = 'inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold bg-primary text-on-primary hover:opacity-95 shadow-sm transition';
        retryBtn.innerHTML = '<span class="material-symbols-outlined text-[16px]">refresh</span> Thử lại';
        retryBtn.onclick = onRetry;
        box.appendChild(retryBtn);
    }

    if (loginHref) {
        const loginLink = document.createElement('a');
        loginLink.href = loginHref;
        loginLink.className = 'inline-flex items-center gap-1.5 ml-2 px-4 py-2 rounded-xl text-xs font-semibold bg-surface-container text-on-surface hover:bg-surface-container-high transition';
        loginLink.textContent = 'Đăng nhập lại';
        box.appendChild(loginLink);
    }

    container.replaceChildren(box);
}

// Cập nhật thông tin Header & Hero Banner
export function renderHeroBanner(customer) {
    if (!customer) return;

    const fullName = customer.fullName ?? customer.FullName ?? 'Khách hàng';
    const phone = customer.phone ?? customer.Phone ?? '—';
    const email = customer.email ?? customer.Email ?? '—';
    const points = customer.loyaltyPoint ?? customer.LoyaltyPoint ?? 0;
    const level = customer.memberLevel ?? customer.MemberLevel ?? 'ThanhVien';
    const customerId = customer.customerId ?? customer.CustomerId ?? '—';
    const avatarUrl = customer.avatarUrl ?? customer.AvatarUrl;

    const resolvedAvatar = avatarUrl ? resolveAssetUrl(avatarUrl) : null;

    // Header updates
    const headerName = document.getElementById('header-user-name');
    if (headerName) headerName.textContent = fullName;

    const headerAvatar = document.getElementById('header-user-avatar');
    const headerAvatarFallback = document.getElementById('header-user-avatar-fallback');
    if (headerAvatar && headerAvatarFallback) {
        if (resolvedAvatar) {
            headerAvatar.src = resolvedAvatar;
            headerAvatar.classList.remove('hidden');
            headerAvatarFallback.classList.add('hidden');
            headerAvatar.onerror = () => {
                headerAvatar.classList.add('hidden');
                headerAvatarFallback.classList.remove('hidden');
            };
        } else {
            headerAvatar.classList.add('hidden');
            headerAvatarFallback.classList.remove('hidden');
        }
    }

    // Hero banner updates
    const heroName = document.getElementById('hero-user-name');
    if (heroName) heroName.textContent = fullName;

    const heroPhone = document.getElementById('hero-user-phone');
    if (heroPhone) heroPhone.querySelector('span:last-child').textContent = phone;

    const heroEmail = document.getElementById('hero-user-email');
    if (heroEmail) heroEmail.querySelector('span:last-child').textContent = email;

    const heroPoints = document.getElementById('hero-user-points');
    if (heroPoints) heroPoints.textContent = formatPoints(points);

    const heroId = document.getElementById('hero-user-id');
    if (heroId) heroId.textContent = `#${customerId}`;

    const tierMeta = getMemberLevelMeta(level, points);
    const heroBadge = document.getElementById('hero-member-badge');
    if (heroBadge) {
        heroBadge.className = `inline-flex items-center gap-1 px-3 py-0.5 rounded-full text-xs font-bold ${tierMeta.badgeClass}`;
        heroBadge.querySelector('span:last-child').textContent = tierMeta.label;
    }

    const heroAvatar = document.getElementById('hero-user-avatar');
    const heroAvatarFallback = document.getElementById('hero-user-avatar-fallback');
    if (heroAvatar && heroAvatarFallback) {
        if (resolvedAvatar) {
            heroAvatar.src = resolvedAvatar;
            heroAvatar.classList.remove('hidden');
            heroAvatarFallback.classList.add('hidden');
            heroAvatar.onerror = () => {
                heroAvatar.classList.add('hidden');
                heroAvatarFallback.classList.remove('hidden');
            };
        } else {
            const initials = fullName
                .split(' ')
                .filter(Boolean)
                .slice(-2)
                .map(w => w[0].toUpperCase())
                .join('') || 'GP';
            heroAvatarFallback.textContent = initials;
            heroAvatar.classList.add('hidden');
            heroAvatarFallback.classList.remove('hidden');
        }
    }

    // Update Digital Card in Overview
    const cardHolder = document.getElementById('card-holder-name');
    if (cardHolder) cardHolder.textContent = fullName;

    const cardCode = document.getElementById('card-member-code');
    if (cardCode) cardCode.textContent = `GP-${String(customerId).padStart(5, '0')}`;

    const cardTier = document.getElementById('card-tier-label');
    if (cardTier) cardTier.textContent = tierMeta.cardTier;

    const cardPoints = document.getElementById('card-points-display');
    if (cardPoints) cardPoints.textContent = `${formatPoints(points)} pts`;

    const benefitsList = document.getElementById('membership-tier-benefits');
    if (benefitsList) {
        let progressHtml = '';
        if (tierMeta.nextTierName) {
            progressHtml = `
                <div class="mb-4 pb-3 border-b border-outline-variant/40">
                    <div class="flex justify-between items-center text-xs mb-1.5 font-medium">
                        <span class="text-on-surface">Tiến độ lên <strong class="text-primary">${tierMeta.nextTierName}</strong>:</span>
                        <span class="text-on-surface-variant font-semibold">${formatPoints(points)} / ${formatPoints(tierMeta.nextTierPoints)} pts (${tierMeta.progressPercent}%)</span>
                    </div>
                    <div class="w-full bg-surface-container rounded-full h-2 overflow-hidden">
                        <div class="bg-primary h-2 rounded-full transition-all duration-500" style="width: ${tierMeta.progressPercent}%"></div>
                    </div>
                    <p class="text-[11px] text-on-surface-variant mt-1.5">Tích lũy thêm <strong>${formatPoints(tierMeta.neededPoints)} điểm</strong> để thăng hạng.</p>
                </div>
            `;
        } else {
            progressHtml = `
                <div class="mb-4 pb-3 border-b border-outline-variant/40">
                    <span class="inline-flex items-center gap-1 text-xs font-bold text-cyan-800 bg-cyan-50 border border-cyan-200 px-3 py-1 rounded-full">
                        <span class="material-symbols-outlined text-[16px]">military_tech</span> Bạn đang ở hạng thành viên cao nhất
                    </span>
                </div>
            `;
        }

        benefitsList.innerHTML = progressHtml + tierMeta.benefits.map(b => `
            <div class="flex items-start gap-2">
                <span class="material-symbols-outlined text-emerald-600 text-[18px] shrink-0 mt-0.5">check_circle</span>
                <span>${b}</span>
            </div>
        `).join('');
    }

    // Update Personal Info Tab View
    const inputFullName = document.getElementById('input-fullname');
    if (inputFullName) inputFullName.value = fullName;

    const inputPhone = document.getElementById('input-phone');
    if (inputPhone) inputPhone.value = phone !== '—' ? phone : '';

    const inputEmail = document.getElementById('input-email');
    if (inputEmail) inputEmail.value = email !== '—' ? email : '';

    const viewMemberLevel = document.getElementById('view-member-level');
    if (viewMemberLevel) viewMemberLevel.textContent = tierMeta.label;

    const viewLoyaltyPoints = document.getElementById('view-loyalty-points');
    if (viewLoyaltyPoints) viewLoyaltyPoints.textContent = `${formatPoints(points)} điểm`;
}

// Render Overview Activity
export function renderOverviewActivity(customer, orders = [], bookings = []) {
    // Stats
    const statPoints = document.getElementById('stat-points');
    if (statPoints) {
        statPoints.textContent = formatPoints(customer?.loyaltyPoint ?? customer?.LoyaltyPoint ?? 0);
    }

    const statOrders = document.getElementById('stat-orders-count');
    if (statOrders) statOrders.textContent = Array.isArray(orders) ? String(orders.length) : '0';

    const statBookings = document.getElementById('stat-bookings-count');
    if (statBookings) statBookings.textContent = Array.isArray(bookings) ? String(bookings.length) : '0';

    // Next / Recent Booking
    const recentBookingBox = document.getElementById('overview-recent-booking');
    if (recentBookingBox) {
        if (!Array.isArray(bookings) || bookings.length === 0) {
            recentBookingBox.innerHTML = `
                <div class="rounded-xl bg-surface-container-low p-5 text-center space-y-2">
                    <p class="text-xs text-on-surface-variant">Bạn chưa có lượt đặt bàn nào.</p>
                    <a href="../booking/index.html" class="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
                        Đặt bàn ngay →
                    </a>
                </div>
            `;
        } else {
            const latestBooking = bookings[0];
            const meta = getBookingStatusMeta(latestBooking.status ?? latestBooking.Status);
            recentBookingBox.innerHTML = `
                <div class="rounded-xl border border-outline-variant/60 bg-surface-container-low p-4 space-y-2">
                    <div class="flex items-center justify-between">
                        <span class="font-bold text-sm text-on-surface">Đặt bàn #${latestBooking.id ?? latestBooking.Id}</span>
                        <span class="px-2.5 py-0.5 rounded-full text-xs font-bold ${meta.badgeClass}">${meta.label}</span>
                    </div>
                    <div class="text-xs text-on-surface-variant space-y-1">
                        <div class="flex items-center gap-1.5">
                            <span class="material-symbols-outlined text-[16px]">calendar_today</span>
                            <span>${formatDate(latestBooking.reservationTime ?? latestBooking.ReservationTime)}</span>
                        </div>
                        <div class="flex items-center gap-1.5">
                            <span class="material-symbols-outlined text-[16px]">group</span>
                            <span>${latestBooking.numberOfGuests ?? latestBooking.NumberOfGuests} khách · 📍 ${latestBooking.tableName ?? latestBooking.TableName ?? 'Chưa xếp bàn'}</span>
                        </div>
                    </div>
                </div>
            `;
        }
    }

    // Next / Recent Order
    const recentOrderBox = document.getElementById('overview-recent-order');
    if (recentOrderBox) {
        if (!Array.isArray(orders) || orders.length === 0) {
            recentOrderBox.innerHTML = `
                <div class="rounded-xl bg-surface-container-low p-5 text-center space-y-2">
                    <p class="text-xs text-on-surface-variant">Bạn chưa có đơn hàng nào.</p>
                    <a href="../menu/index.html" class="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
                        Khám phá thực đơn →
                    </a>
                </div>
            `;
        } else {
            const latestOrder = orders[0];
            const orderMeta = getOrderStatusMeta(latestOrder.status ?? latestOrder.Status);
            const payMeta = getPaymentStatusMeta(latestOrder.paymentStatus ?? latestOrder.PaymentStatus);
            recentOrderBox.innerHTML = `
                <div class="rounded-xl border border-outline-variant/60 bg-surface-container-low p-4 space-y-2">
                    <div class="flex items-center justify-between">
                        <span class="font-bold text-sm text-on-surface">Đơn hàng #${latestOrder.orderId ?? latestOrder.OrderId}</span>
                        <span class="font-bold text-sm text-primary">${formatAmount(latestOrder.totalAmount ?? latestOrder.TotalAmount)}</span>
                    </div>
                    <div class="flex flex-wrap items-center gap-2">
                        <span class="px-2.5 py-0.5 rounded-full text-xs font-bold ${orderMeta.badgeClass}">${orderMeta.label}</span>
                        <span class="px-2.5 py-0.5 rounded-full text-xs font-bold ${payMeta.badgeClass}">${payMeta.label}</span>
                    </div>
                    <p class="text-[11px] text-on-surface-variant flex items-center gap-1 pt-1">
                        <span class="material-symbols-outlined text-[14px]">schedule</span>
                        <span>${formatDate(latestOrder.orderTime ?? latestOrder.OrderTime)}</span>
                    </p>
                </div>
            `;
        }
    }
}

// Render Bookings List
export function renderBookingHistory(container, bookings, filter = 'all', { onCancelBooking } = {}) {
    if (!container) return;

    if (!Array.isArray(bookings) || bookings.length === 0) {
        container.innerHTML = `
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center space-y-4">
                <div class="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">table_restaurant</span>
                </div>
                <div>
                    <h4 class="font-bold text-base text-on-surface">Chưa có lượt đặt bàn nào</h4>
                    <p class="text-xs text-on-surface-variant mt-1">Hãy đặt bàn trước để GustoPOS chuẩn bị chỗ ngồi chu đáo nhất cho bạn.</p>
                </div>
                <a href="../booking/index.html" class="inline-flex items-center gap-2 bg-primary text-on-primary text-xs font-semibold px-5 py-2.5 rounded-xl shadow-sm hover:opacity-95 transition">
                    <span class="material-symbols-outlined text-sm">add</span> Đặt bàn ngay
                </a>
            </div>
        `;
        return;
    }

    const now = new Date();
    const filteredBookings = bookings.filter(b => {
        const rawStatus = b.status ?? b.Status;
        const bTime = new Date(b.reservationTime ?? b.ReservationTime);

        if (filter === 'all') return true;
        if (filter === 'upcoming') {
            return (rawStatus === 'DaXacNhan' || rawStatus === 1 || rawStatus === 'ChoXacNhan' || rawStatus === 0) && bTime > now;
        }
        if (filter === 'seated') {
            return rawStatus === 'DaNhanBan' || rawStatus === 2;
        }
        if (filter === 'completed') {
            return rawStatus === 'HoanTat' || rawStatus === 5;
        }
        if (filter === 'cancelled') {
            return rawStatus === 'DaHuy' || rawStatus === 3 || rawStatus === 'KhongDen' || rawStatus === 4;
        }
        return true;
    });

    if (filteredBookings.length === 0) {
        container.innerHTML = `
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center text-xs text-on-surface-variant">
                Không tìm thấy lượt đặt bàn nào phù hợp với bộ lọc đã chọn.
            </div>
        `;
        return;
    }

    const listElement = document.createElement('div');
    listElement.className = 'space-y-4';

    filteredBookings.forEach(booking => {
        const id = booking.id ?? booking.Id ?? booking.reservationId ?? booking.ReservationId;
        const rawTime = booking.reservationTime ?? booking.ReservationTime;
        const guests = booking.numberOfGuests ?? booking.NumberOfGuests ?? 1;
        const status = booking.status ?? booking.Status;
        const tableName = booking.tableName ?? booking.TableName ?? 'Chưa xếp bàn';
        const reservationDate = new Date(rawTime);

        const meta = getBookingStatusMeta(status);
        // Backend cancel condition: reservation.ReservationTime > DateTime.Now && (Status == DaXacNhan || Status == DaNhanBan)
        const canCancel = reservationDate > now && (status === 'ChoXacNhan' || status === 5 || status === 'DaXacNhan' || status === 0 || status === 1 || status === 'DaNhanBan' || status === 2);

        const card = document.createElement('div');
        card.className = 'rounded-2xl border border-outline-variant/60 bg-surface-container-low p-5 flex flex-col md:flex-row md:items-center justify-between gap-4 hover:border-primary/40 transition shadow-sm';
        card.innerHTML = `
            <div class="flex items-start gap-4">
                <div class="w-12 h-12 rounded-xl bg-surface-container flex items-center justify-center shrink-0 text-primary">
                    <span class="material-symbols-outlined text-2xl">${meta.icon}</span>
                </div>
                <div class="space-y-1">
                    <div class="flex flex-wrap items-center gap-2">
                        <h4 class="font-bold text-base text-on-surface">Đặt bàn #${id}</h4>
                        <span class="px-3 py-0.5 rounded-full text-xs font-bold ${meta.badgeClass}">${meta.label}</span>
                    </div>
                    <div class="flex flex-wrap items-center gap-y-1 gap-x-3 text-xs text-on-surface-variant">
                        <span class="flex items-center gap-1">
                            <span class="material-symbols-outlined text-[16px]">calendar_today</span>
                            <span>${formatDate(rawTime)}</span>
                        </span>
                        <span>·</span>
                        <span class="flex items-center gap-1">
                            <span class="material-symbols-outlined text-[16px]">group</span>
                            <span>${guests} khách</span>
                        </span>
                        <span>·</span>
                        <span class="flex items-center gap-1 font-semibold text-on-surface">
                            <span class="material-symbols-outlined text-[16px] text-primary">table_bar</span>
                            <span>${tableName}</span>
                        </span>
                    </div>
                </div>
            </div>
            <div class="flex items-center gap-2 self-end md:self-center shrink-0">
                ${canCancel ? `
                    <button type="button" class="btn-cancel-booking inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold text-error bg-rose-50 hover:bg-rose-100 border border-rose-200 transition">
                        <span class="material-symbols-outlined text-[16px]">cancel</span>
                        <span>Hủy đặt bàn</span>
                    </button>
                ` : ''}
            </div>
        `;

        if (canCancel) {
            const cancelBtn = card.querySelector('.btn-cancel-booking');
            cancelBtn?.addEventListener('click', () => {
                if (typeof onCancelBooking === 'function') onCancelBooking(booking);
            });
        }

        listElement.appendChild(card);
    });

    container.replaceChildren(listElement);
}

// Render Orders List
export function renderOrderHistory(container, orders, filter = 'all', { onShowDetail } = {}) {
    if (!container) return;

    if (!Array.isArray(orders) || orders.length === 0) {
        container.innerHTML = `
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center space-y-4">
                <div class="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">receipt_long</span>
                </div>
                <div>
                    <h4 class="font-bold text-base text-on-surface">Chưa có đơn hàng nào</h4>
                    <p class="text-xs text-on-surface-variant mt-1">Bạn chưa gọi món nào trên website hoặc tại quán.</p>
                </div>
                <a href="../menu/index.html" class="inline-flex items-center gap-2 bg-primary text-on-primary text-xs font-semibold px-5 py-2.5 rounded-xl shadow-sm hover:opacity-95 transition">
                    <span class="material-symbols-outlined text-sm">restaurant_menu</span> Xem thực đơn
                </a>
            </div>
        `;
        return;
    }

    const filteredOrders = orders.filter(o => {
        const rawStatus = o.status ?? o.Status;
        if (filter === 'all') return true;
        if (filter === 'DangMo') return rawStatus === 'DangMo' || rawStatus === 0;
        if (filter === 'DaThanhToan') return rawStatus === 'DaThanhToan' || rawStatus === 1;
        if (filter === 'DaHuy') return rawStatus === 'DaHuy' || rawStatus === 2;
        return true;
    });

    if (filteredOrders.length === 0) {
        container.innerHTML = `
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center text-xs text-on-surface-variant">
                Không tìm thấy đơn hàng nào phù hợp với bộ lọc đã chọn.
            </div>
        `;
        return;
    }

    const listElement = document.createElement('div');
    listElement.className = 'space-y-4';

    filteredOrders.forEach(order => {
        const id = order.orderId ?? order.OrderId ?? order.id ?? order.Id;
        const time = order.orderTime ?? order.OrderTime;
        const amount = order.totalAmount ?? order.TotalAmount ?? 0;
        const status = order.status ?? order.Status;
        const paymentStatus = order.paymentStatus ?? order.PaymentStatus;

        const orderMeta = getOrderStatusMeta(status);
        const payMeta = getPaymentStatusMeta(paymentStatus);

        const canReviewExperience = Boolean(order.canReviewExperience ?? order.CanReviewExperience);
        const reviewableItemCount = Number(order.reviewableItemCount ?? order.ReviewableItemCount ?? 0);
        const canReview = canReviewExperience || reviewableItemCount > 0;

        // Dish summary string
        const orderItems = order.orderItems ?? order.OrderItems ?? order.items ?? order.Items ?? [];
        let itemsSummary = '—';
        if (Array.isArray(orderItems) && orderItems.length > 0) {
            itemsSummary = orderItems.map(item => {
                const menuItem = item.menuItem ?? item.MenuItem;
                const name = item.itemName ?? item.ItemName ?? menuItem?.itemName ?? menuItem?.ItemName ?? 'Món ăn';
                const qty = item.quantity ?? item.Quantity ?? 1;
                return `${name} × ${qty}`;
            }).join(', ');
        }

        const card = document.createElement('div');
        card.className = 'rounded-2xl border border-outline-variant/60 bg-surface-container-low p-5 space-y-4 hover:border-primary/40 transition shadow-sm';
        card.innerHTML = `
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-outline-variant/40">
                <div class="flex items-center gap-3">
                    <span class="material-symbols-outlined text-primary text-2xl" style="font-variation-settings: 'FILL' 1;">receipt</span>
                    <div>
                        <h4 class="font-bold text-base text-on-surface">Đơn hàng #${id}</h4>
                        <p class="text-xs text-on-surface-variant flex items-center gap-1">
                            <span class="material-symbols-outlined text-[14px]">schedule</span>
                            <span>${formatDate(time)}</span>
                        </p>
                    </div>
                </div>
                <div class="text-left sm:text-right">
                    <span class="text-xs text-on-surface-variant font-medium">Tổng tiền:</span>
                    <p class="text-lg font-extrabold text-primary leading-tight">${formatAmount(amount)}</p>
                </div>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-12 gap-4 items-center">
                <!-- Status Badges -->
                <div class="md:col-span-4 flex flex-wrap items-center gap-2">
                    <span class="px-3 py-1 rounded-full text-xs font-bold ${orderMeta.badgeClass}">Đơn: ${orderMeta.label}</span>
                    <span class="px-3 py-1 rounded-full text-xs font-bold ${payMeta.badgeClass}">${payMeta.label}</span>
                </div>

                <!-- Items Preview -->
                <div class="md:col-span-5 text-xs text-on-surface-variant line-clamp-2" title="${itemsSummary}">
                    <span class="font-semibold text-on-surface">Món đã gọi:</span> ${itemsSummary}
                </div>

                <!-- Action Buttons -->
                <div class="md:col-span-3 flex items-center justify-end gap-2">
                    <button type="button" class="btn-view-detail px-3 py-2 rounded-xl text-xs font-semibold bg-surface-container text-on-surface hover:bg-surface-container-high transition">
                        Xem chi tiết
                    </button>
                    ${canReview ? `
                        <a href="./order-review.html?id=${encodeURIComponent(String(id))}" class="inline-flex items-center gap-1 px-3.5 py-2 rounded-xl text-xs font-semibold bg-primary text-on-primary hover:opacity-95 shadow-sm transition">
                            <span class="material-symbols-outlined text-[14px]">rate_review</span>
                            <span>Đánh giá</span>
                        </a>
                    ` : ''}
                </div>
            </div>
        `;

        card.querySelector('.btn-view-detail')?.addEventListener('click', () => {
            if (typeof onShowDetail === 'function') onShowDetail(id);
        });

        listElement.appendChild(card);
    });

    container.replaceChildren(listElement);
}

// Render Order Detail Modal
export function renderOrderDetailModal(order) {
    const modal = document.getElementById('modal-order-detail');
    const title = document.getElementById('modal-order-title');
    const subtitle = document.getElementById('modal-order-subtitle');
    const body = document.getElementById('modal-order-body');
    const actions = document.getElementById('modal-order-actions');
    if (!modal || !body) return;

    const orderId = order.orderId ?? order.OrderId;
    const time = order.orderTime ?? order.OrderTime;
    const totalAmount = order.totalAmount ?? order.TotalAmount ?? 0;
    const paidAmount = order.paidAmount ?? order.PaidAmount ?? 0;
    const remainingAmount = order.remainingAmount ?? order.RemainingAmount ?? 0;
    const subTotal = order.subTotal ?? order.SubTotal ?? 0;
    const discountAmount = order.discountAmount ?? order.DiscountAmount ?? 0;
    const taxAmount = order.taxAmount ?? order.TaxAmount ?? 0;
    const serviceFee = order.serviceFee ?? order.ServiceFee ?? 0;
    const note = order.note ?? order.Note;
    const tableName = order.table ? `${order.table.tableName || 'Bàn'} - ${order.table.areaName || 'Khu vực'}` : 'Mang về / Chưa xếp bàn';

    const orderMeta = getOrderStatusMeta(order.status ?? order.Status);
    const payMeta = getPaymentStatusMeta(order.paymentStatus ?? order.PaymentStatus);

    title.textContent = `Chi tiết đơn hàng #${orderId}`;
    subtitle.textContent = `Đặt lúc: ${formatDate(time)} · 📍 ${tableName}`;

    const items = order.orderItems ?? order.OrderItems ?? [];
    const invoices = order.invoices ?? order.Invoices ?? [];

    let itemsHtml = '';
    if (items.length === 0) {
        itemsHtml = '<p class="text-xs text-on-surface-variant italic py-2">Không có món ăn trong đơn hàng này.</p>';
    } else {
        itemsHtml = `
            <div class="divide-y divide-outline-variant/40">
                ${items.map(item => {
                    const itemName = item.itemName ?? item.ItemName ?? 'Món ăn';
                    const qty = item.quantity ?? item.Quantity ?? 1;
                    const price = item.unitPrice ?? item.UnitPrice ?? 0;
                    const lineTotal = item.lineTotal ?? item.LineTotal ?? (qty * price);
                    const kStatus = getKitchenStatusMeta(item.status ?? item.Status);
                    const itemNote = item.note ?? item.Note;
                    const cancelReason = item.cancelReason ?? item.CancelReason;
                    const resolvedImage = item.imageUrl ? resolveAssetUrl(item.imageUrl) : '../../assets/menu-placeholder.svg';

                    return `
                        <div class="py-3 flex items-start justify-between gap-3">
                            <div class="flex items-start gap-3">
                                <img src="${resolvedImage}" alt="${itemName}" class="w-12 h-12 rounded-xl object-cover bg-surface-container shrink-0" onerror="this.src='../../assets/menu-placeholder.svg'">
                                <div class="space-y-0.5">
                                    <h5 class="text-xs font-bold text-on-surface">${itemName}</h5>
                                    <p class="text-[11px] text-on-surface-variant">${formatAmount(price)} × ${qty}</p>
                                    ${itemNote ? `<p class="text-[11px] text-amber-700 italic">Ghi chú: ${itemNote}</p>` : ''}
                                    ${cancelReason ? `<p class="text-[11px] text-rose-700 italic">Lý do hủy: ${cancelReason}</p>` : ''}
                                </div>
                            </div>
                            <div class="text-right shrink-0">
                                <p class="text-xs font-bold text-on-surface">${formatAmount(lineTotal)}</p>
                                <span class="inline-block mt-1 px-2 py-0.5 rounded text-[10px] font-semibold ${kStatus.badgeClass}">${kStatus.label}</span>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;
    }

    body.innerHTML = `
        <!-- Order Status Summary -->
        <div class="rounded-2xl bg-surface-container-low p-4 flex flex-wrap items-center justify-between gap-3 border border-outline-variant/40">
            <div class="flex flex-wrap items-center gap-2">
                <span class="px-3 py-1 rounded-full text-xs font-bold ${orderMeta.badgeClass}">Đơn: ${orderMeta.label}</span>
                <span class="px-3 py-1 rounded-full text-xs font-bold ${payMeta.badgeClass}">${payMeta.label}</span>
            </div>
            ${note ? `<div class="text-xs text-on-surface-variant"><span class="font-semibold text-on-surface">Ghi chú:</span> ${note}</div>` : ''}
        </div>

        <!-- Ordered Items Section -->
        <div>
            <h4 class="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2">Danh sách món ăn</h4>
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-lowest p-4">
                ${itemsHtml}
            </div>
        </div>

        <!-- Financial Breakdown -->
        <div>
            <h4 class="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2">Chi tiết thanh toán</h4>
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-lowest p-4 space-y-2.5 text-xs text-on-surface-variant">
                <div class="flex justify-between">
                    <span>Tiền món (Tạm tính):</span>
                    <span class="font-semibold text-on-surface">${formatAmount(subTotal)}</span>
                </div>
                ${discountAmount > 0 ? `
                    <div class="flex justify-between text-emerald-700 font-medium">
                        <span>Giảm giá khuyến mãi:</span>
                        <span>-${formatAmount(discountAmount)}</span>
                    </div>
                ` : ''}
                ${taxAmount > 0 ? `
                    <div class="flex justify-between">
                        <span>Thuế VAT:</span>
                        <span class="font-semibold text-on-surface">${formatAmount(taxAmount)}</span>
                    </div>
                ` : ''}
                ${serviceFee > 0 ? `
                    <div class="flex justify-between">
                        <span>Phí phòng VIP / Dịch vụ:</span>
                        <span class="font-semibold text-on-surface">${formatAmount(serviceFee)}</span>
                    </div>
                ` : ''}
                <div class="pt-2 border-t border-outline-variant/40 flex justify-between text-sm font-extrabold text-on-surface">
                    <span>Tổng tiền thanh toán:</span>
                    <span class="text-primary">${formatAmount(totalAmount)}</span>
                </div>
                <div class="flex justify-between text-xs pt-1">
                    <span>Đã thanh toán:</span>
                    <span class="font-bold text-emerald-700">${formatAmount(paidAmount)}</span>
                </div>
                ${remainingAmount > 0 ? `
                    <div class="flex justify-between text-xs font-bold text-rose-700">
                        <span>Còn lại cần thanh toán:</span>
                        <span>${formatAmount(remainingAmount)}</span>
                    </div>
                ` : ''}
            </div>
        </div>

        ${invoices.length > 0 ? `
            <div>
                <h4 class="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2">Lịch sử hóa đơn & Giao dịch</h4>
                <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-lowest p-4 space-y-3">
                    ${invoices.map(inv => `
                        <div class="text-xs space-y-1.5 pb-2 border-b border-outline-variant/30 last:border-0 last:pb-0">
                            <div class="flex justify-between font-semibold text-on-surface">
                                <span>Hóa đơn #${inv.invoiceId ?? inv.InvoiceId}</span>
                                <span class="text-primary">${formatAmount(inv.totalAmount ?? inv.TotalAmount)}</span>
                            </div>
                            <div class="text-[11px] text-on-surface-variant">
                                Ngày xuất: ${formatDate(inv.invoiceDate ?? inv.InvoiceDate)} · Trạng thái: ${inv.status ?? inv.Status}
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        ` : ''}
    `;

    // Action buttons in modal footer
    const canReviewExperience = Boolean(order.canReviewExperience ?? order.CanReviewExperience);
    const reviewableItemCount = Number(order.reviewableItemCount ?? order.ReviewableItemCount ?? 0);
    if (canReviewExperience || reviewableItemCount > 0) {
        actions.innerHTML = `
            <a href="./order-review.html?id=${encodeURIComponent(String(orderId))}" class="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-xl text-xs font-semibold bg-primary text-on-primary hover:opacity-95 shadow-sm transition">
                <span class="material-symbols-outlined text-[16px]">rate_review</span>
                <span>Đánh giá đơn hàng</span>
            </a>
        `;
    } else {
        actions.innerHTML = '';
    }

    modal.classList.remove('hidden');
}

// Render My Reviews Tab
export function renderMyReviewsTab(container, orders = []) {
    if (!container) return;

    // Filter orders that have reviews
    const reviewedOrders = orders.filter(o => {
        const reviewedCount = Number(o.reviewedItemCount ?? o.ReviewedItemCount ?? 0);
        const hasExp = Boolean(o.hasExperienceReview ?? o.HasExperienceReview);
        return reviewedCount > 0 || hasExp;
    });

    if (reviewedOrders.length === 0) {
        container.innerHTML = `
            <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-8 text-center space-y-4">
                <div class="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">rate_review</span>
                </div>
                <div>
                    <h4 class="font-bold text-base text-on-surface">Chưa có đánh giá nào</h4>
                    <p class="text-xs text-on-surface-variant mt-1">Sau khi thưởng thức bữa ăn tại GustoPOS, hãy để lại đánh giá để chia sẻ cảm nhận của bạn nhé.</p>
                </div>
                <a href="../menu/index.html" class="inline-flex items-center gap-2 bg-primary text-on-primary text-xs font-semibold px-5 py-2.5 rounded-xl shadow-sm hover:opacity-95 transition">
                    <span class="material-symbols-outlined text-sm">restaurant_menu</span> Xem thực đơn
                </a>
            </div>
        `;
        return;
    }

    container.innerHTML = `
        <div class="space-y-4">
            <p class="text-xs text-on-surface-variant">Bạn đã đánh giá các đơn hàng dưới đây. Bấm "Xem & Chỉnh sửa" để xem chi tiết hoặc cập nhật phản hồi.</p>
            <div class="space-y-4">
                ${reviewedOrders.map(o => {
                    const id = o.orderId ?? o.OrderId;
                    const time = o.orderTime ?? o.OrderTime;
                    const amount = o.totalAmount ?? o.TotalAmount ?? 0;
                    const reviewedCount = Number(o.reviewedItemCount ?? o.ReviewedItemCount ?? 0);
                    const hasExp = Boolean(o.hasExperienceReview ?? o.HasExperienceReview);

                    return `
                        <div class="rounded-2xl border border-outline-variant/60 bg-surface-container-low p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                            <div class="space-y-1">
                                <div class="flex items-center gap-2">
                                    <h4 class="font-bold text-sm text-on-surface">Đơn hàng #${id}</h4>
                                    <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200">Đã đánh giá</span>
                                </div>
                                <p class="text-xs text-on-surface-variant flex items-center gap-1">
                                    <span class="material-symbols-outlined text-[14px]">calendar_today</span>
                                    <span>${formatDate(time)}</span>
                                    <span>·</span>
                                    <span>${formatAmount(amount)}</span>
                                </p>
                                <p class="text-[11px] text-on-surface-variant">
                                    ${reviewedCount > 0 ? `Đã đánh giá ${reviewedCount} món ăn` : ''}
                                    ${reviewedCount > 0 && hasExp ? ' · ' : ''}
                                    ${hasExp ? 'Đã đánh giá trải nghiệm quán' : ''}
                                </p>
                            </div>
                            <div class="shrink-0">
                                <a href="./order-review.html?id=${encodeURIComponent(String(id))}" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold bg-primary text-on-primary hover:opacity-95 shadow-sm transition">
                                    <span class="material-symbols-outlined text-[16px]">edit</span>
                                    <span>Xem & Chỉnh sửa</span>
                                </a>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}
