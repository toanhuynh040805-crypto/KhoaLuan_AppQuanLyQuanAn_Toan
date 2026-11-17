import { authApi } from './authApi.js?auth-session-2';
import { profileApi } from './profileApi.js?auth-session-2';
import {
    clearCustomerSession,
    getCurrentCustomer,
    getCustomerToken,
    hasCustomerSession,
    isCustomerSessionRemembered,
    isTokenExpired,
    saveCustomerSession,
    setCurrentCustomer
} from './session.js?auth-session-2';

let restorePromise = null;

export function ensureCustomerSession() {
    if (!hasCustomerSession()) {
        clearCustomerSession();
        return Promise.resolve(false);
    }
    if (restorePromise) return restorePromise;

    restorePromise = (async () => {
        try {
            // 1. Nếu đã có thông tin khách hàng và token chưa hết hạn, phiên hoàn toàn sẵn sàng
            if (getCurrentCustomer() && !isTokenExpired()) {
                return true;
            }

            // 2. Thử lấy thông tin hồ sơ bằng token hiện có nếu chưa hết hạn
            if (!isTokenExpired()) {
                try {
                    const customer = await profileApi.getMyProfile();
                    setCurrentCustomer(customer);
                    return true;
                } catch (err) {
                    // 403 là token hợp lệ nhưng không có quyền Customer: refresh lại cùng
                    // một danh tính sẽ không sửa được lỗi quyền, nên kết thúc phiên ngay.
                    if (err?.status === 403) {
                        clearCustomerSession();
                        globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
                        return false;
                    }
                    // Nếu lỗi không phải 401 (ví dụ mất mạng tạm thời), không vội xóa phiên.
                    if (err?.status !== 401) {
                        return true;
                    }
                    // Nếu 401 (token hết hạn/bị từ chối), tiếp tục thử refresh token ở bước dưới
                }
            }

            // 3. Token đã hết hạn hoặc bị 401: Thử làm mới token qua Backend
            try {
                const response = await authApi.refresh();
                if (response?.accessToken) {
                    saveCustomerSession(response, isCustomerSessionRemembered());
                    const customer = await profileApi.getMyProfile();
                    setCurrentCustomer(customer);
                    return true;
                }
            } catch (refreshErr) {
                if (refreshErr?.status === 401 || refreshErr?.status === 403) {
                    clearCustomerSession();
                    globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
                    return false;
                }
                // Nếu lỗi mạng, giữ nguyên phiên lưu trữ
                return hasCustomerSession() && !isTokenExpired();
            }

            clearCustomerSession();
            return false;
        } catch (err) {
            if (err?.status === 401 || err?.status === 403) {
                clearCustomerSession();
                globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
                return false;
            }
            return hasCustomerSession() && !isTokenExpired();
        } finally {
            restorePromise = null;
        }
    })();

    return restorePromise;
}

export async function logoutCustomerSession() {
    try {
        await authApi.logout();
    } catch (error) {
        if (error?.status === 401) {
            clearCustomerSession();
            globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
        }
        throw error;
    }

    clearCustomerSession();
    globalThis.dispatchEvent?.(new CustomEvent('gusto:session-updated'));
}

export function hasLoadedCustomer() {
    return Boolean(getCustomerToken() && getCurrentCustomer());
}
