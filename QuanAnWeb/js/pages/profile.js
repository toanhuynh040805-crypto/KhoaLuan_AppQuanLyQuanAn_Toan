import { hasCustomerSession, setCurrentCustomer, getCurrentCustomer } from '../api/session.js?auth-session-2';
import { ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { profileApi } from '../api/profileApi.js?auth-session-2';
import { bookingApi } from '../api/bookingApi.js?auth-session-2';
import { resolveAssetUrl } from '../api/config.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';
import {
    renderHeroBanner,
    renderOverviewActivity,
    renderBookingHistory,
    renderOrderHistory,
    renderMyReviewsTab,
    renderOrderDetailModal,
    renderLoading,
    renderStatus,
    showToast
} from '../components/profileView.js';

// DOM Elements
const globalAlert = document.getElementById('profile-global-alert');
const tabButtons = document.querySelectorAll('.profile-tab-btn');
const tabPanes = document.querySelectorAll('.tab-pane');

const bookingListContainer = document.getElementById('booking-history-list');
const orderListContainer = document.getElementById('order-history-list');
const myReviewsContainer = document.getElementById('my-reviews-container');

// Modals
const orderModal = document.getElementById('modal-order-detail');
const closeOrderModalBtn = document.getElementById('btn-close-order-modal');
const closeOrderFooterBtn = document.getElementById('btn-modal-close-footer');

const cancelModal = document.getElementById('modal-cancel-booking');
const cancelDetailsBox = document.getElementById('cancel-booking-details');
const btnConfirmCancel = document.getElementById('btn-confirm-cancel-booking');
const btnDismissCancel = document.getElementById('btn-dismiss-cancel-booking');

const avatarModal = document.getElementById('modal-avatar-upload');
const btnOpenAvatarModal = document.getElementById('btn-open-avatar-modal');
const btnCloseAvatarModal = document.getElementById('btn-close-avatar-modal');
const btnCancelAvatarUpload = document.getElementById('btn-cancel-avatar-upload');
const btnSubmitAvatarUpload = document.getElementById('btn-submit-avatar-upload');
const avatarFileInput = document.getElementById('avatar-file-input');
const avatarPreviewImg = document.getElementById('avatar-preview-img');
const avatarPreviewFallback = document.getElementById('avatar-preview-fallback');
const avatarFeedback = document.getElementById('avatar-upload-feedback');

// Personal Info Form
const infoForm = document.getElementById('profile-info-form');
const btnToggleEditMode = document.getElementById('btn-toggle-edit-mode');
const btnEditModeText = document.getElementById('btn-edit-mode-text');
const btnCancelEdit = document.getElementById('btn-cancel-edit');
const formEditActions = document.getElementById('form-edit-actions');
const btnSaveProfile = document.getElementById('btn-save-profile');

const inputFullName = document.getElementById('input-fullname');
const inputPhone = document.getElementById('input-phone');
const inputEmail = document.getElementById('input-email');
const errorFullName = document.getElementById('error-fullname');
const errorPhone = document.getElementById('error-phone');
const errorEmail = document.getElementById('error-email');

// In-memory data cache
let cachedCustomer = null;
let cachedOrders = [];
let cachedBookings = [];
let activeBookingFilter = 'all';
let activeOrderFilter = 'all';
let bookingToCancel = null;
let selectedAvatarFile = null;
let isEditMode = false;
let isSessionExpired = false;

// Check authentication
const sessionReady = hasCustomerSession() || await ensureCustomerSession();
if (!sessionReady) {
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;
    const loginUrl = new URL('../auth/login_register.html', location.href);
    loginUrl.searchParams.set('returnUrl', returnUrl);
    window.location.replace(loginUrl);
} else {
    setupSessionNavigation();
    initProfilePage();
}

function initProfilePage() {
    // Listen for unauthorized events
    globalThis.addEventListener('gusto:unauthorized', () => {
        isSessionExpired = true;
        if (globalAlert) {
            globalAlert.className = 'rounded-2xl border border-error/40 bg-rose-50 p-4 text-xs md:text-sm text-rose-900 flex items-center justify-between gap-3 shadow-sm';
            globalAlert.innerHTML = `
                <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-error">error</span>
                    <span>Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục thao tác.</span>
                </div>
                <a href="../auth/login_register.html" class="font-bold underline text-primary shrink-0">Đăng nhập</a>
            `;
            globalAlert.classList.remove('hidden');
        }
    });

    // Tab Navigation setup
    setupTabs();

    // Personal info edit handlers
    setupPersonalInfoHandlers();

    // Booking actions and cancel modal handlers
    setupBookingHandlers();

    // Order detail modal handlers
    setupOrderDetailHandlers();

    // Avatar upload handlers
    setupAvatarUploadHandlers();

    // Filters handlers
    setupFilterHandlers();

    // Real-time setup
    setupRealtime();

    // Initial load
    loadAllData();
}

// =========================================================================
// 1. TABS MANAGEMENT
// =========================================================================
function setupTabs() {
    function switchTab(targetTabId, updateHash = true) {
        tabButtons.forEach(btn => {
            const target = btn.dataset.tabTarget;
            const icon = btn.querySelector('.material-symbols-outlined');
            if (target === targetTabId) {
                btn.className = 'profile-tab-btn flex items-center gap-2 px-5 py-3 text-sm font-semibold border-b-2 border-primary text-primary transition whitespace-nowrap';
                if (icon) icon.classList.add('fill-active');
            } else {
                btn.className = 'profile-tab-btn flex items-center gap-2 px-5 py-3 text-sm font-medium border-b-2 border-transparent text-on-surface-variant hover:text-on-surface transition whitespace-nowrap';
                if (icon) icon.classList.remove('fill-active');
            }
        });

        tabPanes.forEach(pane => {
            if (pane.id === `pane-${targetTabId}`) {
                pane.classList.remove('hidden');
            } else {
                pane.classList.add('hidden');
            }
        });

        if (updateHash && location.hash !== `#${targetTabId}`) {
            history.replaceState(null, '', `#${targetTabId}`);
        }
    }

    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            switchTab(btn.dataset.tabTarget);
        });
    });

    // Shortcut switch links (e.g. data-switch-tab="bookings")
    document.querySelectorAll('[data-switch-tab]').forEach(el => {
        el.addEventListener('click', (e) => {
            e.preventDefault();
            switchTab(el.dataset.switchTab);
        });
    });

    document.getElementById('btn-quick-edit-profile')?.addEventListener('click', () => {
        switchTab('personal-info');
        setEditMode(true);
    });

    // Hash change handler
    function syncTabFromHash() {
        const hash = location.hash.replace('#', '').trim();
        const validTabs = ['overview', 'personal-info', 'bookings', 'orders', 'reviews'];
        if (validTabs.includes(hash)) {
            switchTab(hash, false);
        } else {
            switchTab('overview', false);
        }
    }

    window.addEventListener('hashchange', syncTabFromHash);
    syncTabFromHash();
}

// =========================================================================
// 2. DATA LOADING & REAL-TIME UPDATES
// =========================================================================
async function reloadCustomer(silent = true) {
    if (isSessionExpired) return;
    try {
        const profile = await profileApi.getMyProfile();
        if (profile) {
            cachedCustomer = profile;
            setCurrentCustomer(profile);
            if (!isEditMode) {
                renderHeroBanner(profile);
            }
            if (cachedOrders && cachedBookings) {
                renderOverviewActivity(cachedCustomer, cachedOrders, cachedBookings);
            }
        }
    } catch (err) {
        if (!silent) {
            showToast(err.message || 'Không thể tải hồ sơ khách hàng.', 'error');
        }
    }
}

async function reloadOrders(silent = true) {
    if (isSessionExpired) return;
    if (!silent) {
        renderLoading(orderListContainer, 'Đang tải lịch sử đơn hàng...');
    }
    try {
        const raw = await profileApi.getMyOrderHistory();
        cachedOrders = Array.isArray(raw) ? raw : raw?.orders ?? raw?.Orders ?? [];
        renderOrderHistory(orderListContainer, cachedOrders, activeOrderFilter, {
            onShowDetail: openOrderDetailModal
        });
        renderMyReviewsTab(myReviewsContainer, cachedOrders);
        const orderBadge = document.getElementById('tab-badge-orders');
        if (orderBadge) {
            if (cachedOrders.length > 0) {
                orderBadge.textContent = String(cachedOrders.length);
                orderBadge.classList.remove('hidden');
            } else {
                orderBadge.classList.add('hidden');
            }
        }
        if (cachedCustomer) {
            renderOverviewActivity(cachedCustomer, cachedOrders, cachedBookings);
        }
    } catch (err) {
        if (!silent) {
            renderStatus(orderListContainer, err.message || 'Không thể tải lịch sử đơn hàng.', {
                onRetry: () => reloadOrders(false)
            });
        }
    }
}

async function reloadBookings(silent = true) {
    if (isSessionExpired) return;
    if (!silent) {
        renderLoading(bookingListContainer, 'Đang tải lịch sử đặt bàn...');
    }
    try {
        const raw = await profileApi.getMyBookingHistory();
        cachedBookings = Array.isArray(raw) ? raw : raw?.bookings ?? raw?.Bookings ?? [];
        renderBookingHistory(bookingListContainer, cachedBookings, activeBookingFilter, {
            onCancelBooking: openCancelBookingModal
        });
        const bookingBadge = document.getElementById('tab-badge-bookings');
        if (bookingBadge) {
            if (cachedBookings.length > 0) {
                bookingBadge.textContent = String(cachedBookings.length);
                bookingBadge.classList.remove('hidden');
            } else {
                bookingBadge.classList.add('hidden');
            }
        }
        const countAll = document.getElementById('filter-count-all');
        if (countAll) countAll.textContent = String(cachedBookings.length);
        if (cachedCustomer) {
            renderOverviewActivity(cachedCustomer, cachedOrders, cachedBookings);
        }
    } catch (err) {
        if (!silent) {
            renderStatus(bookingListContainer, err.message || 'Không thể tải lịch sử đặt bàn.', {
                onRetry: () => reloadBookings(false)
            });
        }
    }
}

async function loadAllData() {
    renderLoading(bookingListContainer, 'Đang tải lịch sử đặt bàn...');
    renderLoading(orderListContainer, 'Đang tải lịch sử đơn hàng...');
    renderLoading(myReviewsContainer, 'Đang tải danh sách đánh giá...');

    await Promise.allSettled([
        reloadCustomer(false),
        reloadOrders(false),
        reloadBookings(false)
    ]);
}

function setupRealtime() {
    realtimeClient.on('OrderUpdated', () => {
        reloadOrders(true);
        reloadCustomer(true);
    });
    realtimeClient.on('PaymentCompleted', () => {
        reloadOrders(true);
        reloadCustomer(true);
    });
    realtimeClient.on('OrderCreated', () => {
        reloadOrders(true);
        reloadCustomer(true);
    });
    realtimeClient.on('ReservationUpdated', () => {
        reloadBookings(true);
    });
    realtimeClient.on('CustomerProfileUpdated', () => {
        reloadCustomer(true);
    });
    realtimeClient.on('sync', () => {
        reloadOrders(true);
        reloadBookings(true);
        reloadCustomer(true);
    });
    realtimeClient.start().catch(() => {});
}

// =========================================================================
// 3. PERSONAL INFO EDIT HANDLERS
// =========================================================================
function setEditMode(active) {
    isEditMode = active;
    inputFullName.disabled = !active;
    inputPhone.disabled = !active;
    inputEmail.disabled = !active;

    if (active) {
        formEditActions.classList.remove('hidden');
        btnToggleEditMode.classList.add('hidden');
        inputFullName.focus();
    } else {
        formEditActions.classList.add('hidden');
        btnToggleEditMode.classList.remove('hidden');
        clearFormErrors();
        // Restore original cached values
        if (cachedCustomer) renderHeroBanner(cachedCustomer);
    }
}

function clearFormErrors() {
    errorFullName.classList.add('hidden');
    errorPhone.classList.add('hidden');
    errorEmail.classList.add('hidden');
}

function setupPersonalInfoHandlers() {
    btnToggleEditMode?.addEventListener('click', () => setEditMode(true));
    btnCancelEdit?.addEventListener('click', () => setEditMode(false));

    infoForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        clearFormErrors();

        const fullName = inputFullName.value.trim();
        const phone = inputPhone.value.trim();
        const email = inputEmail.value.trim() || null;

        let hasError = false;

        if (!fullName || fullName.length < 2) {
            errorFullName.textContent = 'Họ và tên phải có ít nhất 2 ký tự.';
            errorFullName.classList.remove('hidden');
            hasError = true;
        }

        if (!phone || !/^\d{9,11}$/.test(phone)) {
            errorPhone.textContent = 'Số điện thoại phải gồm 9 đến 11 chữ số.';
            errorPhone.classList.remove('hidden');
            hasError = true;
        }

        if (email && (email.length > 100 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))) {
            errorEmail.textContent = 'Địa chỉ Email không đúng định dạng hoặc vượt quá 100 ký tự.';
            errorEmail.classList.remove('hidden');
            hasError = true;
        }

        if (hasError) return;

        btnSaveProfile.disabled = true;
        const originalBtnText = btnSaveProfile.innerHTML;
        btnSaveProfile.innerHTML = '<span class="material-symbols-outlined text-[18px] animate-spin">progress_activity</span> Đang lưu...';

        try {
            const updated = await profileApi.updateMyProfile({ fullName, phone, email });
            cachedCustomer = updated;
            setCurrentCustomer(updated);
            renderHeroBanner(updated);
            setEditMode(false);
            showToast('Cập nhật thông tin cá nhân thành công!', 'success');
        } catch (error) {
            const msg = error.message || 'Không thể lưu thay đổi. Vui lòng thử lại.';
            showToast(msg, 'error');
            if (msg.includes('Số điện thoại')) {
                errorPhone.textContent = msg;
                errorPhone.classList.remove('hidden');
            } else if (msg.includes('Email')) {
                errorEmail.textContent = msg;
                errorEmail.classList.remove('hidden');
            }
        } finally {
            btnSaveProfile.disabled = false;
            btnSaveProfile.innerHTML = originalBtnText;
        }
    });
}

// =========================================================================
// 4. BOOKING HANDLERS & CANCEL CONFIRMATION
// =========================================================================
function setupBookingHandlers() {
    document.getElementById('btn-refresh-bookings')?.addEventListener('click', async () => {
        await reloadBookings(false);
        showToast('Đã làm mới lịch sử đặt bàn.', 'info');
    });

    btnDismissCancel?.addEventListener('click', closeCancelModal);
    cancelModal?.addEventListener('click', (e) => {
        if (e.target === cancelModal) closeCancelModal();
    });

    btnConfirmCancel?.addEventListener('click', async () => {
        if (!bookingToCancel) return;
        const id = bookingToCancel.id ?? bookingToCancel.Id;

        btnConfirmCancel.disabled = true;
        const originalText = btnConfirmCancel.textContent;
        btnConfirmCancel.textContent = 'Đang hủy...';

        try {
            await bookingApi.cancelMyBooking(id);
            showToast(`Đã hủy đặt bàn #${id} thành công!`, 'success');

            // Update status locally
            const target = cachedBookings.find(b => (b.id ?? b.Id) === id);
            if (target) {
                target.status = 'DaHuy';
                target.Status = 'DaHuy';
            }

            renderBookingHistory(bookingListContainer, cachedBookings, activeBookingFilter, {
                onCancelBooking: openCancelBookingModal
            });
            if (cachedCustomer) {
                renderOverviewActivity(cachedCustomer, cachedOrders, cachedBookings);
            }
            closeCancelModal();
        } catch (error) {
            showToast(error.message || 'Không thể hủy đặt bàn lúc này.', 'error');
        } finally {
            btnConfirmCancel.disabled = false;
            btnConfirmCancel.textContent = originalText;
        }
    });
}

function openCancelBookingModal(booking) {
    bookingToCancel = booking;
    const id = booking.id ?? booking.Id;
    const rawTime = booking.reservationTime ?? booking.ReservationTime;
    const guests = booking.numberOfGuests ?? booking.NumberOfGuests ?? 1;
    const tableName = booking.tableName ?? booking.TableName ?? 'Chưa xếp bàn';

    let formattedDate = '—';
    if (rawTime) {
        const dt = new Date(rawTime);
        formattedDate = `${dt.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} ngày ${dt.toLocaleDateString('vi-VN')}`;
    }

    cancelDetailsBox.innerHTML = `
        <div class="flex justify-between">
            <span class="text-on-surface-variant">Mã đặt bàn:</span>
            <span class="font-bold text-on-surface">#${id}</span>
        </div>
        <div class="flex justify-between">
            <span class="text-on-surface-variant">Thời gian:</span>
            <span class="font-semibold text-on-surface">${formattedDate}</span>
        </div>
        <div class="flex justify-between">
            <span class="text-on-surface-variant">Bàn & Khách:</span>
            <span class="font-semibold text-on-surface">${guests} khách · ${tableName}</span>
        </div>
    `;

    cancelModal.classList.remove('hidden');
}

function closeCancelModal() {
    bookingToCancel = null;
    cancelModal.classList.add('hidden');
}

// =========================================================================
// 5. ORDER DETAIL MODAL HANDLERS
// =========================================================================
function setupOrderDetailHandlers() {
    document.getElementById('btn-refresh-orders')?.addEventListener('click', async () => {
        await reloadOrders(false);
        showToast('Đã làm mới lịch sử đơn hàng.', 'info');
    });

    closeOrderModalBtn?.addEventListener('click', closeOrderDetailModal);
    closeOrderFooterBtn?.addEventListener('click', closeOrderDetailModal);
    orderModal?.addEventListener('click', (e) => {
        if (e.target === orderModal) closeOrderDetailModal();
    });
}

async function openOrderDetailModal(orderId) {
    const modalBody = document.getElementById('modal-order-body');
    const modalTitle = document.getElementById('modal-order-title');
    const modalSubtitle = document.getElementById('modal-order-subtitle');
    const modalActions = document.getElementById('modal-order-actions');

    modalTitle.textContent = `Đang tải đơn hàng #${orderId}...`;
    modalSubtitle.textContent = 'Vui lòng chờ trong giây lát...';
    modalActions.innerHTML = '';
    modalBody.innerHTML = `
        <div class="py-12 text-center space-y-3">
            <div class="w-8 h-8 rounded-full border-2 border-primary border-t-transparent animate-spin mx-auto"></div>
            <p class="text-xs text-on-surface-variant">Đang tải thông tin chi tiết từ hệ thống...</p>
        </div>
    `;

    orderModal.classList.remove('hidden');

    try {
        const orderDetail = await profileApi.getMyOrderDetail(orderId);
        renderOrderDetailModal(orderDetail);
    } catch (error) {
        modalBody.innerHTML = `
            <div class="py-8 text-center space-y-3 text-error">
                <span class="material-symbols-outlined text-4xl">error</span>
                <p class="text-sm font-semibold">${error.message || 'Không thể tải chi tiết đơn hàng.'}</p>
            </div>
        `;
    }
}

function closeOrderDetailModal() {
    orderModal.classList.add('hidden');
}

// =========================================================================
// 6. AVATAR UPLOAD MODAL HANDLERS
// =========================================================================
function setupAvatarUploadHandlers() {
    btnOpenAvatarModal?.addEventListener('click', () => {
        selectedAvatarFile = null;
        avatarFileInput.value = '';
        avatarFeedback.classList.add('hidden');
        btnSubmitAvatarUpload.disabled = true;

        if (cachedCustomer?.avatarUrl) {
            avatarPreviewImg.src = resolveAssetUrl(cachedCustomer.avatarUrl);
            avatarPreviewImg.classList.remove('hidden');
            avatarPreviewFallback.classList.add('hidden');
        } else {
            avatarPreviewImg.classList.add('hidden');
            avatarPreviewFallback.classList.remove('hidden');
        }

        avatarModal.classList.remove('hidden');
    });

    const closeAvatarModal = () => avatarModal.classList.add('hidden');
    btnCloseAvatarModal?.addEventListener('click', closeAvatarModal);
    btnCancelAvatarUpload?.addEventListener('click', closeAvatarModal);
    avatarModal?.addEventListener('click', (e) => {
        if (e.target === avatarModal) closeAvatarModal();
    });

    avatarFileInput?.addEventListener('change', () => {
        avatarFeedback.classList.add('hidden');
        const file = avatarFileInput.files?.[0];
        if (!file) return;

        // Size check (max 5MB)
        if (file.size > 5 * 1024 * 1024) {
            avatarFeedback.textContent = 'Dung lượng ảnh không được vượt quá 5MB.';
            avatarFeedback.classList.remove('hidden');
            btnSubmitAvatarUpload.disabled = true;
            return;
        }

        // Format check
        const validTypes = ['image/png', 'image/jpeg', 'image/webp'];
        if (!validTypes.includes(file.type)) {
            avatarFeedback.textContent = 'Chỉ chấp nhận file ảnh định dạng PNG, JPG hoặc WEBP.';
            avatarFeedback.classList.remove('hidden');
            btnSubmitAvatarUpload.disabled = true;
            return;
        }

        selectedAvatarFile = file;
        const reader = new FileReader();
        reader.onload = (e) => {
            avatarPreviewImg.src = e.target.result;
            avatarPreviewImg.classList.remove('hidden');
            avatarPreviewFallback.classList.add('hidden');
            btnSubmitAvatarUpload.disabled = false;
        };
        reader.readAsDataURL(file);
    });

    btnSubmitAvatarUpload?.addEventListener('click', async () => {
        if (!selectedAvatarFile) return;

        btnSubmitAvatarUpload.disabled = true;
        const originalText = btnSubmitAvatarUpload.textContent;
        btnSubmitAvatarUpload.textContent = 'Đang tải lên...';

        try {
            const updated = await profileApi.uploadMyAvatar(selectedAvatarFile);
            cachedCustomer = updated;
            setCurrentCustomer(updated);
            renderHeroBanner(updated);
            showToast('Cập nhật ảnh đại diện thành công!', 'success');
            closeAvatarModal();
        } catch (error) {
            avatarFeedback.textContent = error.message || 'Tải ảnh lên thất bại. Vui lòng thử lại.';
            avatarFeedback.classList.remove('hidden');
            btnSubmitAvatarUpload.disabled = false;
            btnSubmitAvatarUpload.textContent = originalText;
        }
    });
}

// =========================================================================
// 7. FILTER PILLS HANDLERS
// =========================================================================
function setupFilterHandlers() {
    // Booking filters
    const bookingFilterButtons = document.querySelectorAll('.booking-filter-btn');
    bookingFilterButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            bookingFilterButtons.forEach(b => {
                b.className = 'booking-filter-btn px-3.5 py-1.5 rounded-full text-xs font-medium bg-surface-container text-on-surface hover:bg-surface-container-high transition whitespace-nowrap';
            });
            btn.className = 'booking-filter-btn px-3.5 py-1.5 rounded-full text-xs font-bold bg-primary text-on-primary shadow-sm transition whitespace-nowrap';
            activeBookingFilter = btn.dataset.filter;
            renderBookingHistory(bookingListContainer, cachedBookings, activeBookingFilter, {
                onCancelBooking: openCancelBookingModal
            });
        });
    });

    // Order filters
    const orderFilterButtons = document.querySelectorAll('.order-filter-btn');
    orderFilterButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            orderFilterButtons.forEach(b => {
                b.className = 'order-filter-btn px-3.5 py-1.5 rounded-full text-xs font-medium bg-surface-container text-on-surface hover:bg-surface-container-high transition whitespace-nowrap';
            });
            btn.className = 'order-filter-btn px-3.5 py-1.5 rounded-full text-xs font-bold bg-primary text-on-primary shadow-sm transition whitespace-nowrap';
            activeOrderFilter = btn.dataset.orderFilter;
            renderOrderHistory(orderListContainer, cachedOrders, activeOrderFilter, {
                onShowDetail: openOrderDetailModal
            });
        });
    });
}
