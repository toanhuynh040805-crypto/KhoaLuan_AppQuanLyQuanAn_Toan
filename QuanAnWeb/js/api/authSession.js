import { authApi } from './authApi.js?auth-session-2';
import { profileApi } from './profileApi.js?auth-session-2';
import {
    clearCustomerSession,
    getCurrentCustomer,
    getCustomerToken,
    hasCustomerSession,
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
            const response = await authApi.refresh();
            if (!response?.accessToken) {
                clearCustomerSession();
                return false;
            }
            saveCustomerSession(response);
            const customer = await profileApi.getMyProfile();
            setCurrentCustomer(customer);
            return true;
        } catch {
            clearCustomerSession();
            return false;
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