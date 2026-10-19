import { apiClient } from './client.js?auth-session-2';

export const orderApi = {
    // Tạo đơn dine-in; giá và trạng thái đơn được xác minh, tính tại Backend.
    createCustomerOrder: async (orderData) => {
        // orderData bao gồm: { tableId, clientRequestId, items: [{ menuItemId, quantity, note }] }
        return await apiClient.post('/orders/customer-order', orderData);
    },
    getCustomerOrderByRequestId: async (clientRequestId) => {
        return await apiClient.get(`/orders/customer-order/${encodeURIComponent(clientRequestId)}`);
    }
};