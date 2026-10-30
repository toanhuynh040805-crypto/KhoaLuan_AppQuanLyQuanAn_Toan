import { apiClient } from './client.js?auth-session-2';

export const reviewApi = {
    // 1. Dành cho khách vãng lai và mọi khách hàng xem món ăn được đánh giá ngon
    getMenuItemReviewSummaries: async (params = {}) => {
        const query = new URLSearchParams();
        if (params.skip !== undefined) query.set('skip', params.skip);
        if (params.take !== undefined) query.set('take', params.take);
        if (params.search) query.set('search', params.search);
        if (params.categoryId) query.set('categoryId', params.categoryId);
        if (params.sortBy) query.set('sortBy', params.sortBy);
        const qs = query.toString();
        return await apiClient.get(`/web-feedbacks/menu-items/review-summaries${qs ? `?${qs}` : ''}`);
    },

    getMenuItemReviews: async (menuItemId, params = {}) => {
        const query = new URLSearchParams();
        if (params.skip !== undefined) query.set('skip', params.skip);
        if (params.take !== undefined) query.set('take', params.take);
        if (params.rating) query.set('rating', params.rating);
        if (params.sort) query.set('sort', params.sort);
        const qs = query.toString();
        return await apiClient.get(`/web-feedbacks/menu-items/${menuItemId}${qs ? `?${qs}` : ''}`);
    },

    // 2. Dành cho khách hàng xem và gửi đánh giá các món từ đơn hàng đã ăn
    getMyReviewableItems: async (skip = 0, take = 50) => {
        return await apiClient.get(`/web-feedbacks/my-reviewable-items?skip=${skip}&take=${take}`);
    },

    getOrderItemsForReview: async (orderId) => {
        return await apiClient.get(`/web-feedbacks/orders/${orderId}/items`);
    },

    createReview: async (orderId, orderItemId, reviewData) => {
        // reviewData: { rating, comment }
        return await apiClient.post(`/web-feedbacks/orders/${orderId}/items/${orderItemId}`, reviewData);
    },

    updateReview: async (orderId, orderItemId, reviewData) => {
        return await apiClient.put(`/web-feedbacks/orders/${orderId}/items/${orderItemId}`, reviewData);
    },

    createOrderExperienceReview: async (orderId, reviewData) => {
        return await apiClient.post(`/web-feedbacks/orders/${orderId}/experience`, reviewData);
    },

    updateOrderExperienceReview: async (orderId, reviewData) => {
        return await apiClient.put(`/web-feedbacks/orders/${orderId}/experience`, reviewData);
    },

    // 3. Dành cho Quản lý kiểm duyệt đánh giá
    getModerationReviews: async (params = {}) => {
        const query = new URLSearchParams();
        if (params.skip !== undefined) query.set('skip', params.skip);
        if (params.take !== undefined) query.set('take', params.take);
        if (params.status) query.set('status', params.status);
        const qs = query.toString();
        return await apiClient.get(`/web-feedbacks/moderation${qs ? `?${qs}` : ''}`);
    },

    setModerationDecision: async (reviewId, decision) => {
        return await apiClient.put(`/web-feedbacks/moderation/${reviewId}/decision`, { decision });
    },

    setModerationVisibility: async (reviewId, isVisible) => {
        return await apiClient.put(`/web-feedbacks/moderation/${reviewId}/visibility`, { isVisible });
    },

    // 4. Danh mục loại món để phục vụ bộ lọc
    getCategories: async () => {
        return await apiClient.get('/categories');
    }
};
