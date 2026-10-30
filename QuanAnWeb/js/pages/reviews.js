import { hasCustomerSession, getCustomerToken, getCurrentCustomer } from '../api/session.js?auth-session-2';
import { ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { reviewApi } from '../api/reviewApi.js?auth-session-2';
import { resolveAssetUrl } from '../api/config.js';
import { realtimeClient } from '../realtime/realtimeClient.js';

// ==============================================================
// 1. TIỆN ÍCH CHUNG
// ==============================================================
function formatAmount(amount) {
    if (typeof amount !== 'number') return '0 đ';
    return amount.toLocaleString('vi-VN') + ' đ';
}

function formatDate(dateStr) {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

const toastContainer = document.getElementById('toast-container');
function showToast(message, type = 'info') {
    if (!toastContainer) return;
    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center gap-3 px-4 py-3 rounded-2xl shadow-xl border text-sm font-medium transition-all duration-300 transform translate-y-2 opacity-0 ${
        type === 'success' ? 'bg-emerald-50 text-emerald-950 border-emerald-300' :
        type === 'error' ? 'bg-rose-50 text-rose-950 border-rose-300' :
        type === 'warning' ? 'bg-amber-50 text-amber-950 border-amber-300' :
        'bg-surface-container-lowest text-on-surface border-outline-variant shadow-md'
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

    toastContainer.appendChild(toast);
    requestAnimationFrame(() => {
        toast.classList.remove('translate-y-2', 'opacity-0');
    });

    setTimeout(() => {
        toast.classList.add('opacity-0', 'translate-y-2');
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

function renderStarsHtml(rating, sizeClass = 'text-[18px]') {
    let html = '';
    const rounded = Math.round(rating);
    for (let i = 1; i <= 5; i++) {
        const isFilled = i <= rounded;
        html += `<span class="material-symbols-outlined text-amber-400 ${sizeClass} ${isFilled ? 'fill-active' : ''}">star</span>`;
    }
    return html;
}

const ratingLabels = {
    1: 'Rất tệ (1 sao)',
    2: 'Tệ (2 sao)',
    3: 'Bình thường (3 sao)',
    4: 'Hài lòng (4 sao)',
    5: 'Tuyệt vời (5 sao)'
};

function getModerationBadge(moderationStatus) {
    if (!moderationStatus) return '';
    if (moderationStatus === 'Public') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-50 text-emerald-800 border border-emerald-200"><span class="material-symbols-outlined text-[13px]" style="font-variation-settings: \'FILL\' 1;">verified</span> Đã công khai</span>';
    }
    if (moderationStatus === 'Pending') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-amber-50 text-amber-800 border border-amber-200"><span class="material-symbols-outlined text-[13px]">schedule</span> Đang chờ duyệt</span>';
    }
    if (moderationStatus === 'Rejected') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-rose-50 text-rose-800 border border-rose-200"><span class="material-symbols-outlined text-[13px]">cancel</span> Cần sửa từ ngữ</span>';
    }
    if (moderationStatus === 'Hidden') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-neutral-100 text-neutral-700 border border-neutral-300">Đã ẩn</span>';
    }
    return '';
}

// Kiểm tra quyền Quản lý từ JWT Token (an toàn giải mã client để hiển thị tab quản lý)
function checkIsManager() {
    try {
        const token = getCustomerToken();
        if (!token) return false;
        const parts = token.split('.');
        if (parts.length !== 3) return false;
        const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
        const payload = JSON.parse(payloadJson);
        const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role;
        if (Array.isArray(role)) return role.includes('QuanLy');
        return role === 'QuanLy';
    } catch {
        return false;
    }
}

// ==============================================================
// 2. KHỞI TẠO VÀ CHUYỂN TAB
// ==============================================================
let activeTab = 'top-dishes';
const tabBtnTop = document.getElementById('tab-btn-top-dishes');
const tabBtnMy = document.getElementById('tab-btn-my-reviewable');
const tabBtnMod = document.getElementById('tab-btn-moderation');

const paneTop = document.getElementById('pane-top-dishes');
const paneMy = document.getElementById('pane-my-reviewable');
const paneMod = document.getElementById('pane-moderation');

function switchTab(tab) {
    activeTab = tab;
    tabBtnTop.classList.toggle('border-primary', tab === 'top-dishes');
    tabBtnTop.classList.toggle('text-primary', tab === 'top-dishes');
    tabBtnTop.classList.toggle('border-transparent', tab !== 'top-dishes');
    tabBtnTop.classList.toggle('text-on-surface-variant', tab !== 'top-dishes');

    tabBtnMy.classList.toggle('border-primary', tab === 'my-reviewable');
    tabBtnMy.classList.toggle('text-primary', tab === 'my-reviewable');
    tabBtnMy.classList.toggle('border-transparent', tab !== 'my-reviewable');
    tabBtnMy.classList.toggle('text-on-surface-variant', tab !== 'my-reviewable');

    tabBtnMod.classList.toggle('border-primary', tab === 'moderation');
    tabBtnMod.classList.toggle('text-primary', tab === 'moderation');
    tabBtnMod.classList.toggle('border-transparent', tab !== 'moderation');
    tabBtnMod.classList.toggle('text-on-surface-variant', tab !== 'moderation');

    paneTop.classList.toggle('hidden', tab !== 'top-dishes');
    paneMy.classList.toggle('hidden', tab !== 'my-reviewable');
    paneMod.classList.toggle('hidden', tab !== 'moderation');

    if (tab === 'my-reviewable' && !myReviewableLoaded) {
        loadMyReviewableItems();
    } else if (tab === 'moderation' && !moderationLoaded) {
        loadModerationReviews();
    }
}

tabBtnTop.addEventListener('click', () => switchTab('top-dishes'));
tabBtnMy.addEventListener('click', () => switchTab('my-reviewable'));
tabBtnMod.addEventListener('click', () => switchTab('moderation'));

// ==============================================================
// 3. TAB 1: MÓN ĂN ĐƯỢC ĐÁNH GIÁ NGON (TOP RATED DISHES)
// ==============================================================
const topDishesContainer = document.getElementById('top-dishes-container');
const inputSearchDish = document.getElementById('input-search-dish');
const selectCategory = document.getElementById('select-category');
const selectSort = document.getElementById('select-sort');

let cachedSummaries = [];
let searchDebounceTimer = null;

async function loadCategories() {
    try {
        const categories = await reviewApi.getCategories();
        if (Array.isArray(categories)) {
            categories.forEach(cat => {
                const opt = document.createElement('option');
                opt.value = cat.id || cat.Id || cat;
                opt.textContent = cat.name || cat.Name || cat;
                selectCategory.appendChild(opt);
            });
        }
    } catch {
        // Fallback silently if category endpoint is unconfigured
    }
}

async function loadTopDishes() {
    topDishesContainer.innerHTML = `
        <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center animate-pulse space-y-4">
            <div class="h-8 bg-surface-variant rounded-full w-1/4 mx-auto"></div>
            <div class="h-4 bg-surface-variant rounded-full w-1/2 mx-auto"></div>
            <p class="text-xs text-on-surface-variant">Đang tải danh sách món ăn có đánh giá từ khách hàng...</p>
        </div>
    `;

    try {
        const params = {
            skip: 0,
            take: 48,
            search: inputSearchDish.value.trim() || undefined,
            categoryId: selectCategory.value ? Number(selectCategory.value) : undefined,
            sortBy: selectSort.value || 'highest_rating'
        };

        const res = await reviewApi.getMenuItemReviewSummaries(params);
        cachedSummaries = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
        renderTopDishes(cachedSummaries);
    } catch (error) {
        topDishesContainer.innerHTML = `
            <div class="rounded-3xl border border-error/30 bg-rose-50 p-10 text-center space-y-4 text-rose-950">
                <span class="material-symbols-outlined text-4xl text-error">sentiment_dissatisfied</span>
                <p class="text-base font-bold">${error.message || 'Không thể tải danh sách món ăn đánh giá lúc này.'}</p>
                <button type="button" id="btn-retry-top" class="inline-flex items-center gap-2 bg-primary text-on-primary text-sm font-semibold px-5 py-2.5 rounded-xl hover:opacity-95 shadow-sm">
                    <span class="material-symbols-outlined text-sm">refresh</span> Thử lại
                </button>
            </div>
        `;
        document.getElementById('btn-retry-top')?.addEventListener('click', loadTopDishes);
    }
}

function renderTopDishes(items) {
    if (!Array.isArray(items) || items.length === 0) {
        topDishesContainer.innerHTML = `
            <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center space-y-4">
                <div class="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">restaurant_menu</span>
                </div>
                <div>
                    <h4 class="font-bold text-lg text-on-surface">Chưa có đủ đánh giá để xếp hạng</h4>
                    <p class="text-xs text-on-surface-variant mt-1 max-w-md mx-auto">
                        Các món ăn sẽ tự động xuất hiện tại đây ngay khi có thực khách thưởng thức và để lại nhận xét công khai.
                    </p>
                </div>
                <a href="../menu/index.html" class="inline-flex items-center gap-2 bg-primary text-on-primary text-xs font-semibold px-5 py-2.5 rounded-xl shadow-sm hover:opacity-95 transition">
                    <span class="material-symbols-outlined text-sm">restaurant_menu</span> Khám phá thực đơn
                </a>
            </div>
        `;
        return;
    }

    const grid = document.createElement('div');
    grid.className = 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6';

    items.forEach(item => {
        const menuItemId = item.menuItemId ?? item.MenuItemId;
        const itemName = item.itemName ?? item.ItemName ?? 'Món ngon';
        const imageUrl = resolveAssetUrl(item.imageUrl ?? item.ImageUrl) || '../../images/dish-placeholder.jpg';
        const price = item.price ?? item.Price ?? 0;
        const categoryName = item.categoryName ?? item.CategoryName ?? 'Món ăn';
        const avgRating = item.averageRating ?? item.AverageRating ?? 0;
        const reviewCount = item.reviewCount ?? item.ReviewCount ?? 0;
        const featuredReviews = item.featuredReviews ?? item.FeaturedReviews ?? [];

        const card = document.createElement('div');
        card.className = 'bg-surface-container-lowest rounded-3xl border border-outline-variant/60 overflow-hidden shadow-sm hover:shadow-md transition-all flex flex-col justify-between';

        card.innerHTML = `
            <div>
                <!-- Dish Image -->
                <div class="relative w-full h-48 bg-surface-variant overflow-hidden">
                    <img src="${imageUrl}" alt="${itemName}" class="w-full h-full object-cover transition-transform duration-500 hover:scale-105" onerror="this.onerror=null; this.src='https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=500';">
                    <span class="absolute top-3 left-3 bg-black/60 backdrop-blur-md text-white text-[11px] font-bold px-3 py-1 rounded-full shadow-sm">
                        ${categoryName}
                    </span>
                    <span class="absolute bottom-3 right-3 bg-white/95 backdrop-blur-md text-on-surface text-xs font-bold px-2.5 py-1 rounded-xl shadow-sm flex items-center gap-1">
                        <span class="material-symbols-outlined text-amber-400 text-[16px]" style="font-variation-settings: 'FILL' 1;">star</span>
                        <span>${avgRating > 0 ? avgRating.toFixed(1) : '5.0'}</span>
                    </span>
                </div>

                <!-- Dish Info -->
                <div class="p-5 space-y-3">
                    <div class="flex items-start justify-between gap-2">
                        <h4 class="font-extrabold text-base text-on-surface line-clamp-1 flex-1" title="${itemName}">${itemName}</h4>
                        <span class="font-bold text-sm text-primary shrink-0">${formatAmount(price)}</span>
                    </div>

                    <!-- Rating Stars & Count -->
                    <div class="flex items-center gap-2 text-xs text-on-surface-variant">
                        <div class="flex items-center text-amber-400">
                            ${renderStarsHtml(avgRating, 'text-[16px]')}
                        </div>
                        <span class="font-bold text-on-surface">${avgRating > 0 ? avgRating.toFixed(1) : '5.0'}/5</span>
                        <span>·</span>
                        <span>${reviewCount} đánh giá hợp lệ</span>
                    </div>

                    <!-- Featured Positive Review Quote -->
                    ${featuredReviews.length > 0 && featuredReviews[0].comment ? `
                        <div class="rounded-2xl bg-surface-container-low p-3.5 border border-outline-variant/40 space-y-1.5">
                            <div class="flex items-center gap-1 text-[11px] font-bold text-primary">
                                <span class="material-symbols-outlined text-[14px]">format_quote</span>
                                <span>Nhận xét tiêu biểu</span>
                            </div>
                            <p class="text-xs text-on-surface-variant italic line-clamp-2">
                                "${featuredReviews[0].comment}"
                            </p>
                            <p class="text-[10px] text-on-surface-variant/70 text-right">
                                — ${featuredReviews[0].customerName || 'Khách hàng GustoPOS'}
                            </p>
                        </div>
                    ` : `
                        <div class="rounded-2xl bg-surface-container-low/50 p-3 text-[11px] text-on-surface-variant/70 italic text-center">
                            Được nhiều thực khách đánh giá cao về chất lượng
                        </div>
                    `}
                </div>
            </div>

            <!-- Card Actions -->
            <div class="p-5 pt-0 border-t border-outline-variant/30 flex items-center gap-2 mt-2">
                <button type="button" data-view-reviews="${menuItemId}" class="flex-1 inline-flex items-center justify-center gap-1.5 py-2.5 rounded-xl text-xs font-bold bg-primary/10 text-primary hover:bg-primary hover:text-on-primary transition-colors">
                    <span class="material-symbols-outlined text-[16px]">visibility</span>
                    <span>Xem ${reviewCount} đánh giá</span>
                </button>
                <a href="../menu/index.html" class="p-2.5 rounded-xl text-on-surface-variant bg-surface-container hover:bg-surface-variant transition-colors" title="Xem trong thực đơn">
                    <span class="material-symbols-outlined text-[18px]">restaurant_menu</span>
                </a>
            </div>
        `;

        grid.appendChild(card);
    });

    topDishesContainer.replaceChildren(grid);

    // Bắt sự kiện xem chi tiết đánh giá món
    grid.querySelectorAll('[data-view-reviews]').forEach(btn => {
        btn.addEventListener('click', () => {
            const menuItemId = Number(btn.dataset.viewReviews);
            openDishReviewsModal(menuItemId);
        });
    });
}

// Search debounce
inputSearchDish.addEventListener('input', () => {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
        loadTopDishes();
    }, 350);
});

selectCategory.addEventListener('change', loadTopDishes);
selectSort.addEventListener('change', loadTopDishes);

// ==============================================================
// 4. TAB 2: MÓN ĂN BẠN CÓ THỂ ĐÁNH GIÁ (MY REVIEWABLE ITEMS)
// ==============================================================
const myReviewableContainer = document.getElementById('my-reviewable-container');
const badgeMyCount = document.getElementById('badge-my-reviewable-count');
let myReviewableLoaded = false;
let cachedMyItems = [];
let currentMyFilter = 'all';

async function loadMyReviewableItems() {
    myReviewableLoaded = true;

    // Kiểm tra đăng nhập
    const isLoggedIn = hasCustomerSession() || await ensureCustomerSession();
    if (!isLoggedIn) {
        const returnUrl = encodeURIComponent(location.pathname + location.search);
        myReviewableContainer.innerHTML = `
            <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-10 text-center space-y-4 max-w-xl mx-auto my-6">
                <div class="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">lock</span>
                </div>
                <div>
                    <h4 class="font-bold text-lg text-on-surface">Đăng nhập để đánh giá món ăn</h4>
                    <p class="text-xs text-on-surface-variant mt-1.5 leading-relaxed">
                        Bạn có thể đánh giá các món ăn mà bạn đã thưởng thức sau khi nhân viên phục vụ và đơn hàng được thanh toán. Hãy đăng nhập tài khoản của bạn để xem danh sách món ăn.
                    </p>
                </div>
                <div class="pt-2">
                    <a href="../auth/login_register.html?returnUrl=${returnUrl}" class="inline-flex items-center gap-2 bg-primary text-on-primary text-sm font-semibold px-6 py-3 rounded-xl shadow-sm hover:opacity-95 transition">
                        <span class="material-symbols-outlined text-[18px]">login</span>
                        <span>Đăng nhập ngay</span>
                    </a>
                </div>
            </div>
        `;
        return;
    }

    myReviewableContainer.innerHTML = `
        <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center animate-pulse space-y-4">
            <div class="h-6 bg-surface-variant rounded-full w-1/3 mx-auto"></div>
            <div class="h-4 bg-surface-variant rounded-full w-1/2 mx-auto"></div>
            <p class="text-xs text-on-surface-variant">Đang tải danh sách món ăn từ đơn hàng của bạn...</p>
        </div>
    `;

    try {
        const res = await reviewApi.getMyReviewableItems(0, 100);
        cachedMyItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
        
        // Cập nhật số lượng món chưa đánh giá
        const unreviewedCount = cachedMyItems.filter(item => item.canReview && !item.review).length;
        if (unreviewedCount > 0) {
            badgeMyCount.textContent = String(unreviewedCount);
            badgeMyCount.classList.remove('hidden');
        } else {
            badgeMyCount.classList.add('hidden');
        }

        renderMyItems();
    } catch (error) {
        myReviewableContainer.innerHTML = `
            <div class="rounded-3xl border border-error/30 bg-rose-50 p-10 text-center space-y-4 text-rose-950">
                <span class="material-symbols-outlined text-4xl text-error">sentiment_dissatisfied</span>
                <p class="text-base font-bold">${error.message || 'Không thể tải món ăn từ đơn hàng.'}</p>
                <button type="button" id="btn-retry-my" class="inline-flex items-center gap-2 bg-primary text-on-primary text-sm font-semibold px-5 py-2.5 rounded-xl hover:opacity-95 shadow-sm">
                    <span class="material-symbols-outlined text-sm">refresh</span> Thử lại
                </button>
            </div>
        `;
        document.getElementById('btn-retry-my')?.addEventListener('click', loadMyReviewableItems);
    }
}

function renderMyItems() {
    let items = cachedMyItems;
    if (currentMyFilter === 'unreviewed') {
        items = items.filter(i => !i.review && i.canReview);
    } else if (currentMyFilter === 'reviewed') {
        items = items.filter(i => Boolean(i.review));
    }

    if (items.length === 0) {
        myReviewableContainer.innerHTML = `
            <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center space-y-4">
                <div class="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto text-primary">
                    <span class="material-symbols-outlined text-3xl">rate_review</span>
                </div>
                <div>
                    <h4 class="font-bold text-lg text-on-surface">Không có món ăn nào trong mục này</h4>
                    <p class="text-xs text-on-surface-variant mt-1 max-w-md mx-auto">
                        Món ăn chỉ có thể đánh giá sau khi nhân viên bếp/phục vụ xác nhận hoàn tất và đơn hàng được thanh toán đầy đủ.
                    </p>
                </div>
                <a href="../menu/index.html" class="inline-flex items-center gap-2 bg-primary text-on-primary text-xs font-semibold px-5 py-2.5 rounded-xl shadow-sm hover:opacity-95 transition">
                    <span class="material-symbols-outlined text-sm">restaurant_menu</span> Đặt món ngay
                </a>
            </div>
        `;
        return;
    }

    const list = document.createElement('div');
    list.className = 'space-y-4';

    items.forEach(item => {
        const orderItemId = item.orderItemId ?? item.OrderItemId;
        const orderId = item.orderId ?? item.OrderId;
        const itemName = item.itemName ?? item.ItemName ?? 'Món ăn';
        const imageUrl = resolveAssetUrl(item.imageUrl ?? item.ImageUrl) || '../../images/dish-placeholder.jpg';
        const unitPrice = item.unitPrice ?? item.UnitPrice ?? 0;
        const quantity = item.quantity ?? item.Quantity ?? 1;
        const orderTime = item.orderTime ?? item.OrderTime;
        const canReview = Boolean(item.canReview ?? item.CanReview);
        const review = item.review ?? item.Review;

        const row = document.createElement('div');
        row.className = 'bg-surface-container-lowest rounded-3xl border border-outline-variant/60 p-5 md:p-6 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-5';

        row.innerHTML = `
            <div class="flex items-start gap-4 flex-1">
                <img src="${imageUrl}" alt="${itemName}" class="w-20 h-20 rounded-2xl object-cover border border-outline-variant shrink-0" onerror="this.onerror=null; this.src='https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=200';">
                <div class="space-y-1.5 flex-1">
                    <div class="flex flex-wrap items-center gap-2">
                        <h4 class="font-bold text-base text-on-surface">${itemName}</h4>
                        <span class="text-xs text-on-surface-variant">x${quantity}</span>
                        ${review ? getModerationBadge(review.moderationStatus ?? review.ModerationStatus) : (
                            canReview 
                                ? '<span class="text-[11px] font-bold text-primary bg-primary/10 px-2.5 py-0.5 rounded-full">Đủ điều kiện đánh giá</span>'
                                : '<span class="text-[11px] font-bold text-secondary bg-surface-variant px-2.5 py-0.5 rounded-full">Chưa phục vụ</span>'
                        )}
                    </div>

                    <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-on-surface-variant">
                        <span>Đơn hàng #${orderId}</span>
                        <span>·</span>
                        <span>${formatDate(orderTime)}</span>
                        <span>·</span>
                        <span class="font-semibold text-primary">${formatAmount(unitPrice)}</span>
                        <span>·</span>
                        <span class="inline-flex items-center gap-1 text-emerald-700 font-semibold">
                            <span class="material-symbols-outlined text-[14px]">check_circle</span> Đã thanh toán
                        </span>
                    </div>

                    <!-- Nếu đã có review -> hiển thị nội dung đã đánh giá -->
                    ${review ? `
                        <div class="mt-2 p-3 bg-surface-container-low rounded-xl text-xs space-y-1">
                            <div class="flex items-center gap-1.5 text-amber-400">
                                ${renderStarsHtml(review.rating ?? review.Rating ?? 5, 'text-[15px]')}
                                <span class="font-bold text-on-surface ml-1">${review.rating ?? review.Rating}/5 sao</span>
                            </div>
                            ${(review.comment ?? review.Comment) ? `
                                <p class="text-on-surface-variant italic">"${review.comment ?? review.Comment}"</p>
                            ` : `
                                <p class="text-on-surface-variant/60 italic">Không có nhận xét chi tiết</p>
                            `}
                        </div>
                    ` : ''}
                </div>
            </div>

            <div class="shrink-0 flex items-center justify-end">
                ${review ? `
                    <button type="button" data-edit-review="${orderItemId}" data-order-id="${orderId}" class="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-xl text-xs font-bold bg-surface-container hover:bg-surface-variant text-on-surface transition shadow-sm">
                        <span class="material-symbols-outlined text-[16px]">edit</span>
                        <span>Chỉnh sửa đánh giá</span>
                    </button>
                ` : canReview ? `
                    <button type="button" data-add-review="${orderItemId}" data-order-id="${orderId}" class="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-xl text-xs font-bold bg-primary text-on-primary hover:opacity-95 transition shadow-sm">
                        <span class="material-symbols-outlined text-[16px]">rate_review</span>
                        <span>Đánh giá món ăn</span>
                    </button>
                ` : `
                    <span class="text-xs text-on-surface-variant italic">Chờ nhân viên phục vụ món</span>
                `}
            </div>
        `;

        list.appendChild(row);
    });

    myReviewableContainer.replaceChildren(list);

    // Bind click đánh giá / sửa đánh giá
    list.querySelectorAll('[data-add-review], [data-edit-review]').forEach(btn => {
        btn.addEventListener('click', () => {
            const orderItemId = Number(btn.dataset.addReview || btn.dataset.editReview);
            const orderId = Number(btn.dataset.orderId);
            const item = cachedMyItems.find(i => (i.orderItemId ?? i.OrderItemId) === orderItemId);
            if (item) {
                openReviewFormModal(orderId, item);
            }
        });
    });
}

// Bắt sự kiện lọc sub-filter Tab 2
document.querySelectorAll('[data-my-filter]').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('[data-my-filter]').forEach(b => {
            b.classList.remove('bg-primary', 'text-on-primary', 'font-bold');
            b.classList.add('bg-surface-container', 'text-on-surface-variant', 'font-semibold');
        });
        btn.classList.add('bg-primary', 'text-on-primary', 'font-bold');
        btn.classList.remove('bg-surface-container', 'text-on-surface-variant', 'font-semibold');
        currentMyFilter = btn.dataset.myFilter;
        renderMyItems();
    });
});

// ==============================================================
// 5. TAB 3: KIỂM DUYỆT ĐÁNH GIÁ (QUẢN LÝ / MODERATION)
// ==============================================================
const moderationContainer = document.getElementById('moderation-container');
const badgePendingCount = document.getElementById('badge-pending-count');
let moderationLoaded = false;
let currentModStatus = '';

async function loadModerationReviews() {
    moderationLoaded = true;
    moderationContainer.innerHTML = `
        <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center animate-pulse space-y-4">
            <div class="h-6 bg-surface-variant rounded-full w-1/3 mx-auto"></div>
            <p class="text-xs text-on-surface-variant">Đang tải danh sách đánh giá cần kiểm duyệt...</p>
        </div>
    `;

    try {
        const res = await reviewApi.getModerationReviews({
            skip: 0,
            take: 100,
            status: currentModStatus || undefined
        });

        const items = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
        
        // Cập nhật badge số lượng pending nếu có
        const pendingCount = items.filter(r => (r.moderationStatus ?? r.ModerationStatus) === 'Pending').length;
        if (pendingCount > 0) {
            badgePendingCount.textContent = String(pendingCount);
            badgePendingCount.classList.remove('hidden');
        }

        renderModerationReviews(items);
    } catch (error) {
        moderationContainer.innerHTML = `
            <div class="rounded-3xl border border-error/30 bg-rose-50 p-10 text-center space-y-4 text-rose-950">
                <span class="material-symbols-outlined text-4xl text-error">gpp_maybe</span>
                <p class="text-base font-bold">${error.message || 'Chỉ tài khoản Quản lý mới có quyền truy cập khu vực này.'}</p>
            </div>
        `;
    }
}

function renderModerationReviews(items) {
    if (!Array.isArray(items) || items.length === 0) {
        moderationContainer.innerHTML = `
            <div class="rounded-3xl border border-outline-variant/60 bg-surface-container-low p-12 text-center space-y-3">
                <span class="material-symbols-outlined text-4xl text-primary">verified</span>
                <h4 class="font-bold text-base text-on-surface">Không có đánh giá nào cần xử lý</h4>
                <p class="text-xs text-on-surface-variant">Tất cả đánh giá đều đang ở trạng thái phù hợp.</p>
            </div>
        `;
        return;
    }

    const tableWrapper = document.createElement('div');
    tableWrapper.className = 'bg-surface-container-lowest rounded-3xl border border-outline-variant/60 overflow-hidden shadow-sm';

    let rowsHtml = '';
    items.forEach(r => {
        const reviewId = r.reviewId ?? r.ReviewId;
        const itemName = r.itemName ?? r.ItemName ?? 'Món ăn';
        const customerName = r.customerName ?? r.CustomerName ?? 'Khách hàng';
        const rating = r.rating ?? r.Rating;
        const comment = r.comment ?? r.Comment ?? '(Không có nhận xét)';
        const date = r.createdAt ?? r.CreatedAt;
        const status = r.moderationStatus ?? r.ModerationStatus;
        const isVisible = r.isVisible ?? r.IsVisible;

        rowsHtml += `
            <tr class="border-b border-outline-variant/30 hover:bg-surface-container-low/50 transition">
                <td class="p-4 text-xs font-mono font-bold text-on-surface-variant">#${reviewId}</td>
                <td class="p-4">
                    <p class="font-bold text-xs text-on-surface">${itemName}</p>
                    <p class="text-[11px] text-on-surface-variant">${customerName}</p>
                </td>
                <td class="p-4">
                    <div class="flex items-center text-amber-400">
                        ${renderStarsHtml(rating, 'text-[14px]')}
                    </div>
                </td>
                <td class="p-4 text-xs text-on-surface max-w-xs break-words">
                    ${comment}
                </td>
                <td class="p-4 text-[11px] text-on-surface-variant whitespace-nowrap">
                    ${formatDate(date)}
                </td>
                <td class="p-4 whitespace-nowrap">
                    ${getModerationBadge(status)}
                </td>
                <td class="p-4 whitespace-nowrap text-right">
                    <div class="flex items-center justify-end gap-1.5">
                        ${status === 'Pending' ? `
                            <button type="button" data-action="approve" data-id="${reviewId}" class="p-2 rounded-lg bg-emerald-50 text-emerald-700 hover:bg-emerald-100 font-semibold text-xs transition" title="Duyệt công khai">
                                <span class="material-symbols-outlined text-[18px]">check</span>
                            </button>
                            <button type="button" data-action="reject" data-id="${reviewId}" class="p-2 rounded-lg bg-rose-50 text-rose-700 hover:bg-rose-100 font-semibold text-xs transition" title="Từ chối">
                                <span class="material-symbols-outlined text-[18px]">close</span>
                            </button>
                        ` : ''}
                        ${(status === 'Public' || status === 'Hidden') ? `
                            <button type="button" data-action="toggle-vis" data-id="${reviewId}" data-vis="${isVisible}" class="p-2 rounded-lg bg-surface-container text-on-surface hover:bg-surface-variant text-xs transition" title="${isVisible ? 'Ẩn đánh giá' : 'Hiện lại'}">
                                <span class="material-symbols-outlined text-[18px]">${isVisible ? 'visibility_off' : 'visibility'}</span>
                            </button>
                        ` : ''}
                    </div>
                </td>
            </tr>
        `;
    });

    tableWrapper.innerHTML = `
        <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
                <thead class="bg-surface-container-low text-on-surface-variant font-bold border-b border-outline-variant/60">
                    <tr>
                        <th class="p-4">ID</th>
                        <th class="p-4">Món / Khách</th>
                        <th class="p-4">Số sao</th>
                        <th class="p-4">Nhận xét</th>
                        <th class="p-4">Thời gian</th>
                        <th class="p-4">Trạng thái</th>
                        <th class="p-4 text-right">Thao tác</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml}
                </tbody>
            </table>
        </div>
    `;

    moderationContainer.replaceChildren(tableWrapper);

    // Bắt sự kiện duyệt / từ chối / ẩn
    tableWrapper.querySelectorAll('[data-action]').forEach(btn => {
        btn.addEventListener('click', async () => {
            const action = btn.dataset.action;
            const reviewId = Number(btn.dataset.id);
            btn.disabled = true;

            try {
                if (action === 'approve') {
                    await reviewApi.setModerationDecision(reviewId, 'Public');
                    showToast(`Đã duyệt đánh giá #${reviewId} công khai.`, 'success');
                } else if (action === 'reject') {
                    await reviewApi.setModerationDecision(reviewId, 'Rejected');
                    showToast(`Đã từ chối đánh giá #${reviewId}.`, 'warning');
                } else if (action === 'toggle-vis') {
                    const currentVis = btn.dataset.vis === 'true';
                    await reviewApi.setModerationVisibility(reviewId, !currentVis);
                    showToast(`Đã ${!currentVis ? 'hiện' : 'ẩn'} đánh giá #${reviewId}.`, 'info');
                }
                loadModerationReviews();
                loadTopDishes();
            } catch (err) {
                showToast(err.message || 'Thao tác không thành công.', 'error');
                btn.disabled = false;
            }
        });
    });
}

document.querySelectorAll('[data-mod-filter]').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('[data-mod-filter]').forEach(b => {
            b.classList.remove('bg-primary', 'text-on-primary', 'font-bold');
            b.classList.add('bg-surface-container', 'font-semibold');
        });
        btn.classList.add('bg-primary', 'text-on-primary', 'font-bold');
        btn.classList.remove('bg-surface-container', 'font-semibold');
        currentModStatus = btn.dataset.modFilter;
        loadModerationReviews();
    });
});

// ==============================================================
// 6. MODAL 1: XEM TẤT CẢ ĐÁNH GIÁ CỦA MÓN ĂN
// ==============================================================
const modalDish = document.getElementById('modal-dish-reviews');
const btnCloseDishModal = document.getElementById('btn-close-dish-modal');
const modalDishImg = document.getElementById('modal-dish-img');
const modalDishTitle = document.getElementById('modal-dish-title');
const modalDishCategory = document.getElementById('modal-dish-category');
const modalDishAvg = document.getElementById('modal-dish-avg');
const modalDishStars = document.getElementById('modal-dish-stars');
const modalDishTotal = document.getElementById('modal-dish-total');
const modalRatingBreakdown = document.getElementById('modal-rating-breakdown');
const modalReviewsList = document.getElementById('modal-reviews-list');
const modalDishSort = document.getElementById('modal-dish-sort');
const btnModalLoadMore = document.getElementById('btn-modal-load-more');
const modalLoadMoreContainer = document.getElementById('modal-load-more-container');

let currentModalMenuItemId = null;
let currentModalStarFilter = null;
let currentModalSkip = 0;
const MODAL_TAKE = 10;

btnCloseDishModal.addEventListener('click', () => {
    modalDish.classList.add('hidden');
});

modalDish.addEventListener('click', (e) => {
    if (e.target === modalDish) modalDish.classList.add('hidden');
});

async function openDishReviewsModal(menuItemId) {
    currentModalMenuItemId = menuItemId;
    currentModalStarFilter = null;
    currentModalSkip = 0;

    const dish = cachedSummaries.find(d => (d.menuItemId ?? d.MenuItemId) === menuItemId);
    if (dish) {
        modalDishTitle.textContent = dish.itemName ?? dish.ItemName;
        modalDishCategory.textContent = dish.categoryName ?? dish.CategoryName ?? 'Món ngon';
        modalDishImg.src = resolveAssetUrl(dish.imageUrl ?? dish.ImageUrl) || '../../images/dish-placeholder.jpg';
    }

    // Reset filter button styles
    document.querySelectorAll('.star-filter-btn').forEach(b => {
        b.classList.remove('bg-primary', 'text-on-primary', 'font-bold');
        b.classList.add('bg-surface-container', 'text-on-surface', 'font-semibold');
    });
    document.querySelector('.star-filter-btn[data-star=""]')?.classList.add('bg-primary', 'text-on-primary', 'font-bold');

    modalDish.classList.remove('hidden');
    await fetchModalDishReviews(true);
}

async function fetchModalDishReviews(reset = false) {
    if (reset) {
        currentModalSkip = 0;
        modalReviewsList.innerHTML = '<div class="py-8 text-center text-xs text-on-surface-variant animate-pulse">Đang tải đánh giá...</div>';
    }

    try {
        const res = await reviewApi.getMenuItemReviews(currentModalMenuItemId, {
            skip: currentModalSkip,
            take: MODAL_TAKE,
            rating: currentModalStarFilter || undefined,
            sort: modalDishSort.value || 'newest'
        });

        const avgRating = res?.averageRating ?? res?.AverageRating ?? 0;
        const reviewCount = res?.reviewCount ?? res?.ReviewCount ?? 0;
        const reviews = res?.reviews ?? res?.Reviews ?? [];
        const hasMore = Boolean(res?.hasMore ?? res?.HasMore);
        const breakdown = res?.ratingBreakdown ?? res?.RatingBreakdown ?? {};

        modalDishAvg.textContent = avgRating > 0 ? avgRating.toFixed(1) : '5.0';
        modalDishStars.innerHTML = renderStarsHtml(avgRating, 'text-[22px]');
        modalDishTotal.textContent = `${reviewCount} đánh giá hợp lệ`;

        // Render Breakdown Bars
        let breakdownHtml = '';
        for (let star = 5; star >= 1; star--) {
            const count = breakdown[star] || 0;
            const percent = reviewCount > 0 ? Math.round((count / reviewCount) * 100) : 0;
            breakdownHtml += `
                <div class="flex items-center gap-2 text-xs">
                    <span class="w-10 text-on-surface font-semibold flex items-center gap-0.5">
                        ${star} <span class="material-symbols-outlined text-[13px] text-amber-400">star</span>
                    </span>
                    <div class="flex-1 h-2 bg-surface-variant rounded-full overflow-hidden">
                        <div class="h-full bg-amber-400 rounded-full transition-all duration-500" style="width: ${percent}%"></div>
                    </div>
                    <span class="w-8 text-right text-on-surface-variant text-[11px]">${count}</span>
                </div>
            `;
        }
        modalRatingBreakdown.innerHTML = breakdownHtml;

        // Render Reviews
        if (reset) modalReviewsList.innerHTML = '';

        if (reviews.length === 0 && reset) {
            modalReviewsList.innerHTML = `
                <div class="py-8 text-center text-xs text-on-surface-variant italic">
                    Chưa có đánh giá nào phù hợp với bộ lọc này.
                </div>
            `;
            modalLoadMoreContainer.classList.add('hidden');
            return;
        }

        reviews.forEach(r => {
            const customerName = r.customerName ?? r.CustomerName ?? 'Khách hàng GustoPOS';
            const rating = r.rating ?? r.Rating;
            const comment = r.comment ?? r.Comment;
            const date = r.createdAt ?? r.CreatedAt;

            const itemDiv = document.createElement('div');
            itemDiv.className = 'p-4 rounded-2xl bg-surface-container-low border border-outline-variant/40 space-y-2';
            itemDiv.innerHTML = `
                <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2.5">
                        <div class="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center font-bold text-primary text-xs">
                            ${customerName.charAt(0).toUpperCase()}
                        </div>
                        <div>
                            <div class="flex items-center gap-1.5">
                                <h5 class="font-bold text-xs text-on-surface">${customerName}</h5>
                                <span class="inline-flex items-center gap-0.5 text-[10px] font-semibold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
                                    <span class="material-symbols-outlined text-[12px]">verified</span> Đã dùng món
                                </span>
                            </div>
                            <p class="text-[10px] text-on-surface-variant">${formatDate(date)}</p>
                        </div>
                    </div>
                    <div class="flex items-center text-amber-400">
                        ${renderStarsHtml(rating, 'text-[15px]')}
                    </div>
                </div>
                ${comment ? `
                    <p class="text-xs text-on-surface leading-relaxed pl-10">${comment}</p>
                ` : ''}
            `;
            modalReviewsList.appendChild(itemDiv);
        });

        if (hasMore) {
            modalLoadMoreContainer.classList.remove('hidden');
            currentModalSkip += reviews.length;
        } else {
            modalLoadMoreContainer.classList.add('hidden');
        }

    } catch (err) {
        modalReviewsList.innerHTML = `<div class="py-6 text-center text-xs text-error font-medium">${err.message || 'Lỗi tải đánh giá.'}</div>`;
    }
}

// Bắt sự kiện lọc theo số sao trong modal
document.querySelectorAll('.star-filter-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.star-filter-btn').forEach(b => {
            b.classList.remove('bg-primary', 'text-on-primary', 'font-bold');
            b.classList.add('bg-surface-container', 'text-on-surface', 'font-semibold');
        });
        btn.classList.add('bg-primary', 'text-on-primary', 'font-bold');
        btn.classList.remove('bg-surface-container', 'text-on-surface', 'font-semibold');

        currentModalStarFilter = btn.dataset.star ? Number(btn.dataset.star) : null;
        fetchModalDishReviews(true);
    });
});

modalDishSort.addEventListener('change', () => fetchModalDishReviews(true));
btnModalLoadMore.addEventListener('click', () => fetchModalDishReviews(false));

// ==============================================================
// 7. MODAL 2: FORM ĐÁNH GIÁ MÓN ĂN (REVIEW FORM MODAL)
// ==============================================================
const modalForm = document.getElementById('modal-review-form');
const btnCloseFormModal = document.getElementById('btn-close-form-modal');
const btnCancelForm = document.getElementById('btn-cancel-form');
const formModalTitle = document.getElementById('form-modal-title');
const formModalSubtitle = document.getElementById('form-modal-subtitle');
const formStarContainer = document.getElementById('form-star-picker-container');
const formStarLabel = document.getElementById('form-star-label');
const formReviewComment = document.getElementById('form-review-comment');
const formReviewCharCount = document.getElementById('form-review-char-count');
const formReviewError = document.getElementById('form-review-error');
const dishReviewForm = document.getElementById('dish-review-form');
const btnSubmitReview = document.getElementById('btn-submit-review');
const btnSubmitText = document.getElementById('btn-submit-text');

let currentFormOrderId = null;
let currentFormOrderItemId = null;
let currentFormIsEdit = false;
let currentFormRating = 5;

btnCloseFormModal.addEventListener('click', () => modalForm.classList.add('hidden'));
btnCancelForm.addEventListener('click', () => modalForm.classList.add('hidden'));
modalForm.addEventListener('click', (e) => {
    if (e.target === modalForm) modalForm.classList.add('hidden');
});

function createFormStarPicker(initialRating = 5) {
    currentFormRating = initialRating;
    formStarContainer.innerHTML = '';
    const stars = [];

    for (let i = 1; i <= 5; i++) {
        const star = document.createElement('button');
        star.type = 'button';
        star.className = 'text-amber-400 p-1 hover:scale-125 transition-transform focus:outline-none';
        star.innerHTML = `<span class="material-symbols-outlined text-3xl ${i <= currentFormRating ? 'fill-active' : ''}">star</span>`;

        star.addEventListener('mouseenter', () => updateVisual(i));
        star.addEventListener('mouseleave', () => updateVisual(currentFormRating));
        star.addEventListener('click', () => {
            currentFormRating = i;
            updateVisual(currentFormRating);
            formStarLabel.textContent = ratingLabels[currentFormRating] || `${currentFormRating} sao`;
        });

        stars.push(star);
        formStarContainer.appendChild(star);
    }

    function updateVisual(rating) {
        stars.forEach((s, idx) => {
            const icon = s.querySelector('.material-symbols-outlined');
            if (idx + 1 <= rating) {
                icon.classList.add('fill-active');
            } else {
                icon.classList.remove('fill-active');
            }
        });
    }

    formStarLabel.textContent = ratingLabels[currentFormRating] || `${currentFormRating} sao`;
}

function openReviewFormModal(orderId, item) {
    currentFormOrderId = orderId;
    currentFormOrderItemId = item.orderItemId ?? item.OrderItemId;
    const existing = item.review ?? item.Review;
    currentFormIsEdit = Boolean(existing);

    formModalTitle.textContent = currentFormIsEdit ? `Chỉnh sửa đánh giá: ${item.itemName ?? item.ItemName}` : `Đánh giá món: ${item.itemName ?? item.ItemName}`;
    formModalSubtitle.textContent = `Đơn hàng #${orderId}`;

    formReviewError.textContent = '';
    const initialRating = existing ? (existing.rating ?? existing.Rating ?? 5) : 5;
    createFormStarPicker(initialRating);

    formReviewComment.value = existing ? (existing.comment ?? existing.Comment ?? '') : '';
    formReviewCharCount.textContent = formReviewComment.value.length;
    btnSubmitText.textContent = currentFormIsEdit ? 'Cập nhật đánh giá' : 'Gửi đánh giá';

    modalForm.classList.remove('hidden');
}

formReviewComment.addEventListener('input', () => {
    formReviewCharCount.textContent = formReviewComment.value.length;
    formReviewError.textContent = '';
});

dishReviewForm.onsubmit = async (e) => {
    e.preventDefault();
    formReviewError.textContent = '';
    btnSubmitReview.disabled = true;
    const originalText = btnSubmitText.textContent;
    btnSubmitText.textContent = 'Đang gửi...';

    const payload = {
        rating: currentFormRating,
        comment: formReviewComment.value.trim() || null
    };

    try {
        let res;
        if (currentFormIsEdit) {
            res = await reviewApi.updateReview(currentFormOrderId, currentFormOrderItemId, payload);
        } else {
            res = await reviewApi.createReview(currentFormOrderId, currentFormOrderItemId, payload);
        }

        modalForm.classList.add('hidden');

        if (res?.code === 'REVIEW_PENDING') {
            showToast('Đánh giá của bạn đã được lưu và đang chờ kiểm duyệt!', 'info');
        } else {
            showToast('Cảm ơn bạn! Đánh giá món ăn đã được ghi nhận thành công.', 'success');
        }

        // Tải lại dữ liệu Tab của tôi và Tab món ngon
        await loadMyReviewableItems();
        await loadTopDishes();

    } catch (err) {
        // Lỗi từ ngữ hoặc lỗi nghiệp vụ
        formReviewError.textContent = err.message || 'Không thể gửi đánh giá lúc này.';
        showToast(err.message || 'Gửi đánh giá thất bại.', 'error');
    } finally {
        btnSubmitReview.disabled = false;
        btnSubmitText.textContent = originalText;
    }
};

// ==============================================================
// 8. KHỞI TẠO TRANG KHI NẠP
// ==============================================================
document.addEventListener('DOMContentLoaded', async () => {
    setupSessionNavigation();

    // Kiểm tra nếu là Quản lý thì hiện tab kiểm duyệt
    if (checkIsManager()) {
        tabBtnMod.classList.remove('hidden');
        tabBtnMod.classList.add('flex');
    }

    await loadCategories();
    await loadTopDishes();

    async function reloadReviewsSilently() {
        try {
            if (activeTab === 'top-dishes') {
                const params = {
                    skip: 0,
                    take: 48,
                    search: inputSearchDish.value.trim() || undefined,
                    categoryId: selectCategory.value ? Number(selectCategory.value) : undefined,
                    sortBy: selectSort.value || 'highest_rating'
                };
                const res = await reviewApi.getMenuItemReviewSummaries(params);
                cachedSummaries = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                renderTopDishes(cachedSummaries);
            } else if (activeTab === 'my-reviewable' && hasCustomerSession()) {
                const res = await reviewApi.getMyReviewableItems(0, 100);
                cachedMyItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                const unreviewedCount = cachedMyItems.filter(item => item.canReview && !item.review).length;
                if (unreviewedCount > 0) {
                    badgeMyCount.textContent = String(unreviewedCount);
                    badgeMyCount.classList.remove('hidden');
                } else {
                    badgeMyCount.classList.add('hidden');
                }
                renderMyItems();
            } else if (activeTab === 'moderation' && checkIsManager()) {
                loadModerationReviews();
            }
        } catch (e) {
            console.debug('Silent reviews reload failed:', e);
        }
    }

    realtimeClient.on('ReviewsChanged', reloadReviewsSilently);
    realtimeClient.on('PaymentCompleted', reloadReviewsSilently);
    realtimeClient.on('sync', reloadReviewsSilently);
    realtimeClient.start().catch(() => {});
});

