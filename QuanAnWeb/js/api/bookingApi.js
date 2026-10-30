import { apiClient } from './client.js?auth-session-2';

export const bookingApi = {
    getAvailableTables: async ({ reservationTime, durationMinutes = 30, areaId } = {}) => {
        const query = new URLSearchParams();
        if (reservationTime) {
            query.set('reservationTime', reservationTime);
            query.set('time', reservationTime);
        }
        query.set('durationMinutes', String(durationMinutes));
        if (areaId != null && Number.isInteger(Number(areaId))) query.set('areaId', String(areaId));

        try {
            const tables = await apiClient.get(`/reservations/available-tables?${query}`);
            return Array.isArray(tables) ? tables : [];
        } catch {
            const fallbackTables = await apiClient.get(`/tables/available?${query}`);
            return Array.isArray(fallbackTables) ? fallbackTables.filter(t => t.isAvailable !== false && t.IsAvailable !== false) : [];
        }
    },

    createBooking: async (bookingData) => {
        return await apiClient.post('/reservations', bookingData);
    },

    createBookingBatch: async (bookingBatchData) => {
        return await apiClient.post('/reservations/batch', bookingBatchData);
    },

    lookupBooking: async (code, phone) => {
        return await apiClient.get(`/reservations/lookup?code=${encodeURIComponent(code)}&phone=${encodeURIComponent(phone)}`);
    },

    cancelBooking: async (code, phone) => {
        return await apiClient.post(`/reservations/cancel?code=${encodeURIComponent(code)}&phone=${encodeURIComponent(phone)}`);
    },

    // Dành cho khách đã đăng nhập hủy đặt bàn của chính mình
    cancelMyBooking: async (id) => {
        return await apiClient.post(`/reservations/me/${encodeURIComponent(id)}/cancel`);
    }
};