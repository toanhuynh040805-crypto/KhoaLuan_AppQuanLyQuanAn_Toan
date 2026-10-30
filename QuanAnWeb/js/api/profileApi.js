import { apiClient } from './client.js?auth-session-2';
import { getCustomerToken } from './session.js?auth-session-2';
import { API_BASE_URL } from './config.js';

export const profileApi = {
    getMyProfile: async () => { return await apiClient.get('/customers/me'); },
    getMyOrderHistory: async () => { return await apiClient.get('/customers/me/orders'); },
    
    // THÊM MỚI:
    getMyOrderDetail: async (orderId) => { 
        return await apiClient.get(`/customers/me/orders/${orderId}`); 
    },
    getMyBookingHistory: async () => { 
        return await apiClient.get('/customers/me/bookings'); 
    },
    updateMyProfile: async (profileData) => { 
        return await apiClient.put('/customers/me', profileData); 
    },
    uploadMyAvatar: async (imageFile) => {
        // Avatar dùng FormData nên gọi fetch thủ công thay vì apiClient.post (nó stringify JSON)
        const formData = new FormData();
        formData.append('avatar', imageFile);
        const response = await fetch(`${API_BASE_URL}/customers/me/avatar`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${getCustomerToken()}` },
            body: formData
        });
        if (!response.ok) {
            let errorMsg = 'Upload ảnh thất bại';
            try {
                const errData = await response.json();
                if (errData?.message) errorMsg = errData.message;
            } catch {
                // Ignore parse error
            }
            throw new Error(errorMsg);
        }
        return await response.json();
    }
};