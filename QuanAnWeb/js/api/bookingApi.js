import { apiClient } from './client.js?auth-session-2';

export const bookingApi = {
    getAvailableTables: async ({ reservationTime, durationMinutes = 30, areaId } = {}) => {
        const query = new URLSearchParams();
        query.set('reservationTime', reservationTime);
        query.set('durationMinutes', String(durationMinutes));
        if (areaId != null && Number.isInteger(Number(areaId))) query.set('areaId', String(areaId));

        const tables = await apiClient.get(`/reservations/available-tables?${query}`);
        return Array.isArray(tables) ? tables : [];
    },

    createBooking: async (bookingData) => {
        return await apiClient.post('/reservations/web', bookingData);
    },

    createBookingBatch: async (bookingBatchData) => {
        return await apiClient.post('/reservations/web', bookingBatchData);
    },

    lookupBooking: async (code, phone) => {
        return await apiClient.get(`/reservations/lookup?code=${encodeURIComponent(code)}&phone=${encodeURIComponent(phone)}`);
    },

    cancelBooking: async (code, phone) => {
        return await apiClient.post(`/reservations/cancel?code=${encodeURIComponent(code)}&phone=${encodeURIComponent(phone)}`);
    }
};