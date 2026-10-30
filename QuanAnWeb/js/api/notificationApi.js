import { apiClient } from './client.js?auth-session-2';

export const notificationApi = {
    getMyNotifications: async (skip = 0, take = 30) => {
        return await apiClient.get(`/customers/me/notifications?skip=${skip}&take=${take}`);
    },
    getUnreadCount: async () => {
        return await apiClient.get('/customers/me/notifications/unread-count');
    },
    markRead: async (notificationId) => {
        return await apiClient.put(`/customers/me/notifications/${notificationId}/read`, {});
    },
    markAllRead: async () => {
        return await apiClient.put('/customers/me/notifications/read-all', {});
    }
};
