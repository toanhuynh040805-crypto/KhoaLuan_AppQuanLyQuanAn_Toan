import { apiClient } from './client.js?auth-session-2';

export const promotionsApi = {
    // Dành cho màn hình quản lý, cần quyền QuanLy.
    getAllPromotions: async () => {
        const data = await apiClient.get('/discountcampaigns');
        if (!Array.isArray(data)) throw new TypeError('Promotions response must be an array.');
        return data;
    },

    // Dành cho trang công khai: các chiến dịch được bật, gồm cả lịch sắp tới và đã qua.
    getPublicPromotions: async () => {
        const data = await apiClient.get('/discountcampaigns/public');
        if (!Array.isArray(data)) throw new TypeError('Public promotions response must be an array.');
        return data;
    },

    // Dành cho thu ngân: các chiến dịch có thể áp dụng ngay.
    getActivePromotions: async () => {
        const data = await apiClient.get('/discountcampaigns/active');
        if (!Array.isArray(data)) throw new TypeError('Active promotions response must be an array.');
        return data;
    }
};