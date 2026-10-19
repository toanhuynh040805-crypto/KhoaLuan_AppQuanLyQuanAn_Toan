import { apiClient } from './client.js?auth-session-2';

export const profileApi = {
    // Lấy thông tin hồ sơ cá nhân dựa vào JWT Token đăng nhập
    getMyProfile: async () => { 
        return await apiClient.get('/customers/me'); 
    },
    
    // Lấy danh sách lịch sử đơn hàng của khách hàng hiện tại
    getMyOrderHistory: async () => { 
        return await apiClient.get('/customers/me/orders'); 
    }
};