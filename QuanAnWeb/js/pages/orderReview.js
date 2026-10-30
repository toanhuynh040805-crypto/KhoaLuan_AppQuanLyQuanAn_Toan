import { hasCustomerSession, getCurrentCustomer } from '../api/session.js?auth-session-2';
import { ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { reviewApi } from '../api/reviewApi.js?auth-session-2';

const stateElement = document.getElementById('review-page-state');
const contentElement = document.getElementById('review-content');
const subtitleElement = document.getElementById('order-subtitle');
const metaBadgeElement = document.getElementById('order-meta-badge');
const toastContainer = document.getElementById('toast-container');

function showToast(message, type = 'info') {
    if (!toastContainer) return;
    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center gap-3 px-4 py-3 rounded-xl shadow-lg border text-sm font-medium transition-all duration-300 transform translate-y-2 opacity-0 ${
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

    toastContainer.appendChild(toast);
    requestAnimationFrame(() => {
        toast.classList.remove('translate-y-2', 'opacity-0');
    });

    setTimeout(() => {
        toast.classList.add('opacity-0', 'translate-y-2');
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

const ratingLabels = {
    1: 'Rất tệ (1 sao)',
    2: 'Tệ (2 sao)',
    3: 'Bình thường (3 sao)',
    4: 'Hài lòng (4 sao)',
    5: 'Tuyệt vời (5 sao)'
};

function createStarPicker(initialRating = 5, onChange) {
    let currentRating = initialRating;
    const container = document.createElement('div');
    container.className = 'flex items-center gap-1.5';

    const stars = [];
    for (let i = 1; i <= 5; i++) {
        const star = document.createElement('button');
        star.type = 'button';
        star.className = 'text-amber-400 p-0.5 hover:scale-110 transition-transform focus:outline-none';
        star.setAttribute('aria-label', `${i} sao`);
        star.innerHTML = `<span class="material-symbols-outlined text-2xl md:text-3xl ${i <= currentRating ? 'fill-active' : ''}">star</span>`;
        
        star.addEventListener('mouseenter', () => {
            updateStarsVisual(i);
        });

        star.addEventListener('mouseleave', () => {
            updateStarsVisual(currentRating);
        });

        star.addEventListener('click', () => {
            currentRating = i;
            updateStarsVisual(currentRating);
            if (typeof onChange === 'function') onChange(currentRating);
        });

        stars.push(star);
        container.appendChild(star);
    }

    function updateStarsVisual(rating) {
        stars.forEach((s, idx) => {
            const icon = s.querySelector('.material-symbols-outlined');
            if (idx + 1 <= rating) {
                icon.classList.add('fill-active');
            } else {
                icon.classList.remove('fill-active');
            }
        });
    }

    return {
        element: container,
        getRating: () => currentRating,
        setRating: (r) => {
            currentRating = r;
            updateStarsVisual(r);
        }
    };
}

function getModerationBadge(moderationStatus) {
    if (!moderationStatus) return '';
    if (moderationStatus === 'Public') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200"><span class="material-symbols-outlined text-[14px]">verified</span> Đã duyệt & công khai</span>';
    }
    if (moderationStatus === 'Pending') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-200"><span class="material-symbols-outlined text-[14px]">schedule</span> Đang chờ duyệt</span>';
    }
    if (moderationStatus === 'Rejected') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-800 border border-rose-200"><span class="material-symbols-outlined text-[14px]">cancel</span> Cần sửa từ ngữ</span>';
    }
    if (moderationStatus === 'Hidden') {
        return '<span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-neutral-100 text-neutral-700 border border-neutral-300">Đã ẩn</span>';
    }
    return '';
}

// Kiểm tra phiên đăng nhập
const authenticated = hasCustomerSession() || await ensureCustomerSession();
if (!authenticated) {
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;
    const loginUrl = new URL('../auth/login_register.html', location.href);
    loginUrl.searchParams.set('returnUrl', returnUrl);
    window.location.replace(loginUrl);
} else {
    setupSessionNavigation();

    const params = new URLSearchParams(location.search);
    const orderIdParam = params.get('id');
    const orderId = Number(orderIdParam);

    if (!orderIdParam || !Number.isInteger(orderId) || orderId <= 0) {
        stateElement.innerHTML = `
            <div class="rounded-2xl border border-outline-variant bg-surface-container-low p-8 text-center space-y-4">
                <span class="material-symbols-outlined text-4xl text-error">error</span>
                <p class="text-base text-on-surface font-semibold">Mã đơn hàng không hợp lệ.</p>
                <a href="./index.html#orders" class="inline-flex items-center gap-2 bg-primary text-on-primary text-sm font-semibold px-5 py-2.5 rounded-xl hover:opacity-95">
                    <span class="material-symbols-outlined text-sm">arrow_back</span> Quay lại danh sách đơn hàng
                </a>
            </div>
        `;
    } else {
        loadOrderReview(orderId);
    }
}

async function loadOrderReview(orderId) {
    stateElement.innerHTML = `
        <div class="rounded-2xl border border-outline-variant bg-surface-container-low p-8 text-center animate-pulse space-y-3">
            <div class="h-6 bg-surface-variant rounded w-1/3 mx-auto"></div>
            <div class="h-4 bg-surface-variant rounded w-1/2 mx-auto"></div>
            <p class="text-xs text-on-surface-variant">Đang tải thông tin đánh giá đơn hàng #${orderId}...</p>
        </div>
    `;

    try {
        const data = await reviewApi.getOrderItemsForReview(orderId);
        renderReviewPage(data, orderId);
    } catch (error) {
        stateElement.innerHTML = `
            <div class="rounded-2xl border border-error/30 bg-rose-50 p-8 text-center space-y-4 text-rose-950">
                <span class="material-symbols-outlined text-4xl text-error">sentiment_dissatisfied</span>
                <p class="text-base font-semibold">${error.message || 'Không thể tải thông tin đánh giá cho đơn hàng này.'}</p>
                <div class="flex justify-center gap-3">
                    <button type="button" id="btn-retry" class="inline-flex items-center gap-2 bg-primary text-on-primary text-sm font-semibold px-5 py-2.5 rounded-xl hover:opacity-95">
                        <span class="material-symbols-outlined text-sm">refresh</span> Thử lại
                    </button>
                    <a href="./index.html#orders" class="inline-flex items-center gap-2 bg-surface-container-high text-on-surface text-sm font-semibold px-5 py-2.5 rounded-xl hover:bg-surface-variant">
                        Quay lại hồ sơ
                    </a>
                </div>
            </div>
        `;
        document.getElementById('btn-retry')?.addEventListener('click', () => loadOrderReview(orderId));
    }
}

function renderReviewPage(data, orderId) {
    stateElement.innerHTML = '';
    contentElement.classList.remove('hidden');

    subtitleElement.textContent = `Đơn hàng #${orderId} · Hãy để lại ý kiến giúp chúng tôi nâng cao chất lượng phục vụ`;

    // Render badge trạng thái đơn hàng
    metaBadgeElement.classList.remove('hidden');
    const isCancelled = data.isCancelled || data.IsCancelled;
    if (isCancelled) {
        metaBadgeElement.innerHTML = '<span class="px-3 py-1 rounded-full text-xs font-bold bg-rose-100 text-rose-800 border border-rose-200">Đơn đã hủy</span>';
    } else {
        metaBadgeElement.innerHTML = '<span class="px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">Đã thanh toán</span>';
    }

    // ==========================================
    // 1. SETUP ĐÁNH GIÁ TRẢI NGHIỆM CHUNG (EXPERIENCE)
    // ==========================================
    const canReviewExp = Boolean(data.canReviewExperience ?? data.CanReviewExperience);
    const existingExp = data.experienceReview ?? data.ExperienceReview;
    const expForm = document.getElementById('experience-review-form');
    const expStarsContainer = document.getElementById('exp-stars-container');
    const expStarLabel = document.getElementById('exp-star-label');
    const expComment = document.getElementById('exp-comment');
    const expCharCount = document.getElementById('exp-char-count');
    const expFeedback = document.getElementById('exp-feedback-msg');
    const expSubmitBtn = document.getElementById('exp-submit-btn');
    const expSubmitText = document.getElementById('exp-submit-text');
    const expModerationBadge = document.getElementById('exp-moderation-badge');

    expStarsContainer.innerHTML = '';
    let expRating = existingExp?.rating ?? existingExp?.Rating ?? 5;
    const expStarPicker = createStarPicker(expRating, (newRating) => {
        expRating = newRating;
        expStarLabel.textContent = ratingLabels[newRating] || `${newRating} sao`;
    });
    expStarsContainer.appendChild(expStarPicker.element);
    expStarLabel.textContent = ratingLabels[expRating] || `${expRating} sao`;

    if (existingExp) {
        expComment.value = existingExp.comment ?? existingExp.Comment ?? '';
        expCharCount.textContent = expComment.value.length;
        expSubmitText.textContent = 'Cập nhật đánh giá trải nghiệm';
        const modStatus = existingExp.moderationStatus ?? existingExp.ModerationStatus;
        expModerationBadge.innerHTML = getModerationBadge(modStatus);
    }

    expComment.addEventListener('input', () => {
        expCharCount.textContent = expComment.value.length;
    });

    if (!canReviewExp && !existingExp) {
        expForm.innerHTML = `
            <div class="rounded-xl bg-surface-container-low p-4 text-xs text-on-surface-variant border border-outline-variant/60 flex items-center gap-2">
                <span class="material-symbols-outlined text-secondary">info</span>
                <span>Chỉ có thể đánh giá trải nghiệm khi tất cả món trong đơn đã được phục vụ hoặc hủy và đơn hàng đã thanh toán.</span>
            </div>
        `;
    } else {
        expForm.onsubmit = async (e) => {
            e.preventDefault();
            expFeedback.textContent = '';
            expSubmitBtn.disabled = true;
            const originalText = expSubmitText.textContent;
            expSubmitText.textContent = 'Đang lưu...';

            const payload = {
                rating: expRating,
                comment: expComment.value.trim() || null
            };

            try {
                let res;
                if (existingExp) {
                    res = await reviewApi.updateOrderExperienceReview(orderId, payload);
                } else {
                    res = await reviewApi.createOrderExperienceReview(orderId, payload);
                }

                if (res?.code === 'REVIEW_PENDING') {
                    showToast(res.message || 'Đánh giá đã lưu và đang chờ duyệt!', 'info');
                    expModerationBadge.innerHTML = getModerationBadge('Pending');
                } else {
                    showToast('Cảm ơn bạn! Đánh giá trải nghiệm đã được ghi nhận.', 'success');
                    expModerationBadge.innerHTML = getModerationBadge('Public');
                }
                expSubmitText.textContent = 'Cập nhật đánh giá trải nghiệm';
            } catch (err) {
                const message = err.message || 'Không thể lưu đánh giá trải nghiệm.';
                expFeedback.textContent = message;
                showToast(message, 'error');
                expSubmitText.textContent = originalText;
            } finally {
                expSubmitBtn.disabled = false;
            }
        };
    }

    // ==========================================
    // 2. SETUP ĐÁNH GIÁ TỪNG MÓN ĂN (ITEMS)
    // ==========================================
    const dishesList = document.getElementById('dishes-review-list');
    dishesList.innerHTML = '';
    const items = data.items ?? data.Items ?? [];

    if (!Array.isArray(items) || items.length === 0) {
        dishesList.innerHTML = `
            <p class="text-sm text-on-surface-variant italic py-4">Đơn hàng không có món ăn nào để đánh giá.</p>
        `;
        return;
    }

    items.forEach((item, index) => {
        const orderItemId = item.orderItemId ?? item.OrderItemId;
        const itemName = item.itemName ?? item.ItemName ?? 'Món ăn';
        const quantity = item.quantity ?? item.Quantity ?? 1;
        const canReviewItem = Boolean(item.canReview ?? item.CanReview);
        const existingReview = item.review ?? item.Review;

        const dishCard = document.createElement('div');
        dishCard.className = 'rounded-xl border border-outline-variant/60 bg-surface-container-low p-5 space-y-4';

        const headerRow = document.createElement('div');
        headerRow.className = 'flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-outline-variant/40';

        const titleDiv = document.createElement('div');
        titleDiv.className = 'flex items-center gap-3';
        titleDiv.innerHTML = `
            <div class="w-10 h-10 rounded-lg bg-surface-container flex items-center justify-center font-bold text-primary text-sm">
                ${index + 1}
            </div>
            <div>
                <h4 class="font-bold text-base text-on-surface">${itemName}</h4>
                <p class="text-xs text-on-surface-variant">Số lượng: ${quantity}</p>
            </div>
        `;

        const badgeDiv = document.createElement('div');
        if (existingReview) {
            badgeDiv.innerHTML = getModerationBadge(existingReview.moderationStatus ?? existingReview.ModerationStatus);
        } else if (!canReviewItem) {
            badgeDiv.innerHTML = '<span class="text-xs text-on-surface-variant bg-surface-variant px-2.5 py-1 rounded-full">Chưa đủ điều kiện đánh giá</span>';
        }

        headerRow.appendChild(titleDiv);
        headerRow.appendChild(badgeDiv);
        dishCard.appendChild(headerRow);

        if (!canReviewItem && !existingReview) {
            const notice = document.createElement('p');
            notice.className = 'text-xs text-on-surface-variant italic';
            notice.textContent = 'Món này chỉ có thể đánh giá sau khi được phục vụ và đơn hàng đã hoàn tất thanh toán.';
            dishCard.appendChild(notice);
            dishesList.appendChild(dishCard);
            return;
        }

        // Form đánh giá món
        const form = document.createElement('form');
        form.className = 'space-y-4 pt-1';

        const starWrapper = document.createElement('div');
        starWrapper.innerHTML = '<label class="block text-xs font-semibold text-on-surface mb-1.5">Chấm điểm món này:</label>';
        
        let itemRating = existingReview?.rating ?? existingReview?.Rating ?? 5;
        const starLabel = document.createElement('span');
        starLabel.className = 'text-xs font-semibold text-primary ml-2 inline-block';
        starLabel.textContent = ratingLabels[itemRating];

        const starPicker = createStarPicker(itemRating, (r) => {
            itemRating = r;
            starLabel.textContent = ratingLabels[r];
        });

        const starFlex = document.createElement('div');
        starFlex.className = 'flex items-center gap-2 flex-wrap';
        starFlex.appendChild(starPicker.element);
        starFlex.appendChild(starLabel);
        starWrapper.appendChild(starFlex);
        form.appendChild(starWrapper);

        // Ô nhận xét
        const commentWrapper = document.createElement('div');
        const initialComment = existingReview?.comment ?? existingReview?.Comment ?? '';
        commentWrapper.innerHTML = `
            <label class="block text-xs font-semibold text-on-surface mb-1.5">
                Nhận xét món ăn:
            </label>
            <textarea rows="2" maxlength="1000" placeholder="Hương vị món ăn như thế nào? Bạn có thích món này không?" class="w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-3.5 py-2.5 text-sm text-on-surface placeholder:text-on-surface-variant/60 focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none transition">${initialComment}</textarea>
            <div class="flex justify-between items-center text-xs text-on-surface-variant mt-1">
                <span class="feedback-msg text-error font-medium"></span>
                <span><span class="char-count">${initialComment.length}</span>/1000</span>
            </div>
        `;

        const textarea = commentWrapper.querySelector('textarea');
        const charCount = commentWrapper.querySelector('.char-count');
        const feedbackMsg = commentWrapper.querySelector('.feedback-msg');
        textarea.addEventListener('input', () => {
            charCount.textContent = textarea.value.length;
        });
        form.appendChild(commentWrapper);

        // Nút gửi
        const actionRow = document.createElement('div');
        actionRow.className = 'flex justify-end';
        const submitBtn = document.createElement('button');
        submitBtn.type = 'submit';
        submitBtn.className = 'inline-flex items-center gap-1.5 bg-primary text-on-primary font-semibold text-xs px-5 py-2 rounded-lg shadow-sm hover:opacity-95 disabled:opacity-50 transition';
        submitBtn.innerHTML = `
            <span class="material-symbols-outlined text-[16px]">check</span>
            <span>${existingReview ? 'Cập nhật đánh giá món' : 'Gửi đánh giá món'}</span>
        `;
        actionRow.appendChild(submitBtn);
        form.appendChild(actionRow);

        form.onsubmit = async (e) => {
            e.preventDefault();
            feedbackMsg.textContent = '';
            submitBtn.disabled = true;
            const btnSpan = submitBtn.querySelector('span:last-child');
            const originalText = btnSpan.textContent;
            btnSpan.textContent = 'Đang lưu...';

            const payload = {
                rating: itemRating,
                comment: textarea.value.trim() || null
            };

            try {
                let res;
                if (existingReview) {
                    res = await reviewApi.updateReview(orderId, orderItemId, payload);
                } else {
                    res = await reviewApi.createReview(orderId, orderItemId, payload);
                }

                if (res?.code === 'REVIEW_PENDING') {
                    showToast(`Đánh giá món "${itemName}" đã lưu và đang chờ duyệt!`, 'info');
                    badgeDiv.innerHTML = getModerationBadge('Pending');
                } else {
                    showToast(`Đã lưu đánh giá cho món "${itemName}" thành công!`, 'success');
                    badgeDiv.innerHTML = getModerationBadge('Public');
                }
                btnSpan.textContent = 'Cập nhật đánh giá món';
            } catch (err) {
                const message = err.message || 'Không thể lưu đánh giá món ăn.';
                feedbackMsg.textContent = message;
                showToast(message, 'error');
                btnSpan.textContent = originalText;
            } finally {
                submitBtn.disabled = false;
            }
        };

        dishCard.appendChild(form);
        dishesList.appendChild(dishCard);
    });
}

