import { promotionsApi } from '../api/promotionsApi.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

const promotionsListContainer = document.querySelector('[data-promotions-list]');

if (promotionsListContainer) {
    setupSessionNavigation();

    function getPromotionStatus(startDate, endDate) {
        const now = Date.now();
        if (now < startDate.getTime()) {
            return { label: 'Sắp diễn ra', classes: 'bg-secondary-container text-on-surface' };
        }
        if (now > endDate.getTime()) {
            return { label: 'Đã hết hạn', classes: 'bg-surface-container-high text-on-surface-variant' };
        }
        return { label: 'Đang áp dụng', classes: 'bg-primary-container text-on-primary-container' };
    }

    function createPromotionCard(promotion) {
        const campaignName = promotion.campaignName ?? promotion.CampaignName;
        const startDateValue = promotion.startDate ?? promotion.StartDate;
        const endDateValue = promotion.endDate ?? promotion.EndDate;
        const discountPercent = Number(promotion.discountPercent ?? promotion.DiscountPercent);
        const startDate = new Date(startDateValue);
        const endDate = new Date(endDateValue);

        if (
            typeof campaignName !== 'string' ||
            !campaignName.trim() ||
            Number.isNaN(startDate.getTime()) ||
            Number.isNaN(endDate.getTime()) ||
            !Number.isFinite(discountPercent)
        ) {
            throw new TypeError('Promotion response contains invalid campaign data.');
        }

        const status = getPromotionStatus(startDate, endDate);
        const card = document.createElement('article');
        card.className = 'rounded-xl border border-outline-variant bg-surface-container-lowest p-6 shadow-sm';

        const header = document.createElement('div');
        header.className = 'mb-3 flex flex-wrap items-center justify-between gap-2';

        const discount = document.createElement('span');
        discount.className = 'rounded bg-primary-container px-3 py-1 font-label-md font-bold text-on-primary-container';
        discount.textContent = `Giảm ${new Intl.NumberFormat('vi-VN').format(discountPercent)}%`;

        const statusLabel = document.createElement('span');
        statusLabel.className = `rounded-full px-3 py-1 text-sm font-semibold ${status.classes}`;
        statusLabel.textContent = status.label;
        header.append(discount, statusLabel);

        const campaignTitle = document.createElement('h2');
        campaignTitle.className = 'mb-2 font-headline-sm font-bold text-on-surface';
        campaignTitle.textContent = campaignName.trim();

        const dateRange = document.createElement('p');
        dateRange.className = 'mb-3 text-sm text-on-surface-variant';
        dateRange.textContent = `${startDate.toLocaleDateString('vi-VN')} - ${endDate.toLocaleDateString('vi-VN')}`;

        card.append(header, campaignTitle, dateRange);

        const note = promotion.note ?? promotion.Note;
        if (typeof note === 'string' && note.trim()) {
            const description = document.createElement('p');
            description.className = 'mb-3 text-body-md text-on-surface-variant';
            description.textContent = note.trim();
            card.append(description);
        }

        const requiresMembership = promotion.requireMembership ?? promotion.RequireMembership;
        if (requiresMembership) {
            const minVisitCount = Number(promotion.minVisitCount ?? promotion.MinVisitCount);
            const lookbackDays = Number(promotion.visitLookbackDays ?? promotion.VisitLookbackDays);
            const eligibility = document.createElement('p');
            eligibility.className = 'text-sm font-medium text-on-surface-variant';
            eligibility.textContent = Number.isInteger(minVisitCount) && minVisitCount > 0 &&
                Number.isInteger(lookbackDays) && lookbackDays > 0
                ? `Điều kiện: khách hàng thân thiết có ít nhất ${minVisitCount} lượt ghé trong ${lookbackDays} ngày gần nhất.`
                : 'Điều kiện: chỉ áp dụng cho khách hàng thân thiết.';
            const unsupported = document.createElement('p');
            unsupported.className = 'mt-2 text-sm text-error';
            unsupported.textContent = 'Hệ thống hiện chưa hỗ trợ xác minh điều kiện hoặc áp dụng ưu đãi này.';
            card.append(eligibility, unsupported);
        }

        return card;
    }

    function showEmptyState() {
        const emptyState = document.createElement('p');
        emptyState.className = 'text-on-surface-variant';
        emptyState.textContent = 'Hiện chưa có chương trình khuyến mãi';
        promotionsListContainer.replaceChildren(emptyState);
    }

    function showErrorState(error) {
        const wrapper = document.createElement('div');
        wrapper.className = 'col-span-full rounded-xl border border-error/30 bg-surface-container-lowest p-6';
        wrapper.setAttribute('role', 'alert');

        const message = document.createElement('p');
        message.className = 'mb-4 text-error';
        message.textContent = `Không thể tải danh sách khuyến mãi. ${error instanceof Error ? error.message : 'Vui lòng thử lại.'}`;

        const retryButton = document.createElement('button');
        retryButton.type = 'button';
        retryButton.dataset.retryPromotions = '';
        retryButton.className = 'min-h-11 rounded-lg bg-primary px-5 py-2 font-semibold text-on-primary hover:opacity-90';
        retryButton.textContent = 'Thử tải lại';
        wrapper.append(message, retryButton);
        promotionsListContainer.replaceChildren(wrapper);
    }

    async function loadPromotions() {
        promotionsListContainer.setAttribute('aria-busy', 'true');
        try {
            const loading = document.createElement('p');
            loading.className = 'text-on-surface-variant';
            loading.setAttribute('role', 'status');
            loading.textContent = 'Đang tải danh sách khuyến mãi...';
            promotionsListContainer.replaceChildren(loading);

            const promotions = await promotionsApi.getPublicPromotions();
            if (promotions.length === 0) {
                showEmptyState();
                return;
            }

            promotionsListContainer.replaceChildren(...promotions.map(createPromotionCard));
        } catch (error) {
            console.error('Unable to load promotions.', error);
            showErrorState(error);
        } finally {
            promotionsListContainer.setAttribute('aria-busy', 'false');
        }
    }

    promotionsListContainer.addEventListener('click', event => {
        if (event.target instanceof Element && event.target.closest('[data-retry-promotions]')) {
            loadPromotions();
        }
    });

    async function reloadPromotionsSilently() {
        try {
            const promotions = await promotionsApi.getPublicPromotions();
            if (promotions.length === 0) {
                showEmptyState();
                return;
            }
            promotionsListContainer.replaceChildren(...promotions.map(createPromotionCard));
        } catch (e) {
            console.debug('Silent promotions reload failed:', e);
        }
    }

    realtimeClient.on('PromotionsChanged', reloadPromotionsSilently);
    realtimeClient.on('sync', reloadPromotionsSilently);
    realtimeClient.start().catch(() => {});

    loadPromotions();
}