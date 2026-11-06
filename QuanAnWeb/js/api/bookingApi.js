import { apiClient } from './client.js?auth-session-2';

// Khởi tạo đối tượng cấu hình toàn cục đồng bộ từ Backend
window.AppConfig = window.AppConfig || {};
if (typeof window.AppConfig.bookingDuration !== 'number') {
    window.AppConfig.bookingDuration = 30; // Fallback an toàn tạm thời trước khi API trả về
}

export const bookingApi = {
    /**
     * Lấy cấu hình đặt bàn từ Backend (defaultDurationMinutes, min, max)
     * và tự động lưu vào window.AppConfig.bookingDuration
     */
    getConfig: async () => {
        try {
            const config = await apiClient.get('/config/booking');
            if (config && typeof config.defaultDurationMinutes === 'number') {
                window.AppConfig = window.AppConfig || {};
                window.AppConfig.bookingDuration = config.defaultDurationMinutes;
                window.AppConfig.minBookingDuration = config.minDurationMinutes;
                window.AppConfig.maxBookingDuration = config.maxDurationMinutes;
            }
            return config;
        } catch (error) {
            console.warn('Không thể tải cấu hình đặt bàn từ Backend, sử dụng cấu hình mặc định:', error);
            return null;
        }
    },

    getAvailableTables: async ({ reservationTime, areaId } = {}) => {
        const query = new URLSearchParams();
        if (reservationTime) {
            query.set('reservationTime', reservationTime);
            query.set('time', reservationTime);
        }
        // TUYỆT ĐỐI KHÔNG truyền durationMinutes lên URL để Backend tự động sử dụng cấu hình mặc định
        if (areaId != null && Number.isInteger(Number(areaId))) query.set('areaId', String(areaId));

        // /tables/available là nguồn chuẩn dùng chung cho Web và WinForms.
        // Endpoint này xử lý trùng TableId cũ, trạng thái hiện tại và lịch đặt
        // giao nhau trên cùng một bàn vật lý.
        try {
            const tables = await apiClient.get(`/tables/available?${query}`);
            return Array.isArray(tables) ? tables : [];
        } catch {
            // Chỉ giữ fallback legacy để tương thích khi Backend cũ chưa được restart.
            const fallbackTables = await apiClient.get(`/reservations/available-tables?${query}`);
            return Array.isArray(fallbackTables) ? fallbackTables : [];
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
    },

    // Lấy danh sách đặt bàn của tài khoản hiện tại (khớp Backend WebProfileController: GET api/customers/me/bookings)
    getMyBookings: async () => {
        const res = await apiClient.get('/customers/me/bookings');
        return Array.isArray(res) ? res : (res?.bookings ?? res?.data ?? []);
    }
};

// Tự động gọi nạp cấu hình hệ thống ngay khi module được nạp
bookingApi.getConfig().catch(() => {});
