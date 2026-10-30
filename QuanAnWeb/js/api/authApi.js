import { apiClient } from './client.js?auth-session-2';

export const authApi = {
    login: async (identifier, password) => { 
        return await apiClient.post('/customers/login', { identifier, password });
    },
    register: async (customerData) => { 
        return await apiClient.post('/customers/register', customerData);
    },
    refresh: async () => {
        return await apiClient.post('/customers/refresh');
    },
    logout: async () => {
        return await apiClient.post('/customers/logout', {});
    },
    forgotPassword: async (email) => {
        return await apiClient.post('/auth/forgot-password', { email });
    },
    resetPassword: async (token, newPassword) => {
        return await apiClient.post('/auth/reset-password', { token, newPassword });
    },
    completeGoogleProfile: async (profileData) => {
        // profileData: { email, fullName, phone }
        return await apiClient.post('/auth/google/complete-profile', profileData);
    }
};