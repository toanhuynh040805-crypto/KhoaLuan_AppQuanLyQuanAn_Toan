export function ensureCanonicalOrigin() {
    try {
        if (typeof location === 'undefined') return;
        // Thống nhất hostname phát triển về localhost:5500 để tránh phân mảnh localStorage giữa 127.0.0.1 và localhost
        if (location.hostname === '127.0.0.1' && location.port === '5500') {
            const canonical = new URL(location.href);
            canonical.hostname = 'localhost';
            location.replace(canonical.toString());
        }
    } catch {
        // Storage or location may be unavailable.
    }
}

ensureCanonicalOrigin();

let customerAccessToken = null;
let currentCustomer = null;

const CUSTOMER_TOKEN_STORAGE_KEY = 'gusto_customer_access_token';
const LEGACY_TOKEN_STORAGE_KEYS = ['customer_token', 'gusto_token'];

function removeLegacyAuthStorage() {
    try {
        localStorage.removeItem('gusto_user');
    } catch {
        // Storage may be unavailable.
    }
    try {
        sessionStorage.removeItem('gusto_token');
        sessionStorage.removeItem('gusto_user');
    } catch {
        // Storage may be unavailable.
    }
}

function readStoredToken() {
    try {
        // Đọc Session trước (nếu đăng nhập không check "Ghi nhớ")
        const sessionToken = sessionStorage.getItem(CUSTOMER_TOKEN_STORAGE_KEY);
        if (typeof sessionToken === 'string' && sessionToken.trim()) return sessionToken;

        // Đọc Local sau (nếu đã check "Ghi nhớ" từ trước)
        const direct = localStorage.getItem(CUSTOMER_TOKEN_STORAGE_KEY);
        if (typeof direct === 'string' && direct.trim()) return direct;

        for (const key of LEGACY_TOKEN_STORAGE_KEYS) {
            const value = localStorage.getItem(key);
            if (typeof value === 'string' && value.trim()) return value;
        }
    } catch {
        // Storage may be unavailable.
    }

    return null;
}

function hydrateTokenFromUrl() {
    try {
        if (typeof location !== 'undefined') {
            const pathname = (location.pathname || '').toLowerCase();
            // Tuyệt đối không can thiệp hoặc xóa tham số token trên trang đặt lại mật khẩu
            if (pathname.includes('reset-password')) {
                return;
            }
        }

        const params = new URLSearchParams(location.search);
        const token = params.get('accessToken') ?? params.get('token');
        if (typeof token === 'string' && token.trim()) {
            setCustomerAccessToken(token, true);
            const cleanUrl = new URL(location.href);
            cleanUrl.searchParams.delete('accessToken');
            cleanUrl.searchParams.delete('token');
            if (cleanUrl.search === '') {
                history.replaceState(null, '', cleanUrl.pathname + cleanUrl.hash);
            } else {
                history.replaceState(null, '', `${cleanUrl.pathname}${cleanUrl.search}${cleanUrl.hash}`);
            }
            globalThis.dispatchEvent?.(new CustomEvent('gusto:session-updated'));
        }
    } catch {
        // Ignore invalid URL state.
    }
}

removeLegacyAuthStorage();
if (!customerAccessToken) {
    const persistedToken = readStoredToken();
    if (persistedToken) {
        customerAccessToken = persistedToken;
    }
}
hydrateTokenFromUrl();

export function getCustomerToken() {
    if (!customerAccessToken) {
        const storedToken = readStoredToken();
        if (storedToken) customerAccessToken = storedToken;
    }
    return customerAccessToken;
}

// Bổ sung tham số rememberMe (mặc định true)
export function setCustomerAccessToken(token, rememberMe = true) {
    if (typeof token !== 'string' || token.trim() === '') {
        throw new TypeError('Customer authentication response did not include an access token.');
    }
    customerAccessToken = token;
    currentCustomer = null;
    try {
        // Remove the previous session mode first so an old persistent token cannot
        // override a new session-only login.
        localStorage.removeItem(CUSTOMER_TOKEN_STORAGE_KEY);
        sessionStorage.removeItem(CUSTOMER_TOKEN_STORAGE_KEY);
        for (const key of LEGACY_TOKEN_STORAGE_KEYS) {
            localStorage.removeItem(key);
            sessionStorage.removeItem(key);
        }
        if (rememberMe) {
            localStorage.setItem(CUSTOMER_TOKEN_STORAGE_KEY, token);
        } else {
            sessionStorage.setItem(CUSTOMER_TOKEN_STORAGE_KEY, token);
        }
    } catch {
        // Storage may be unavailable.
    }
}

export function isCustomerSessionRemembered() {
    try {
        if (sessionStorage.getItem(CUSTOMER_TOKEN_STORAGE_KEY)) return false;
        return Boolean(
            localStorage.getItem(CUSTOMER_TOKEN_STORAGE_KEY) ||
            LEGACY_TOKEN_STORAGE_KEYS.some((key) => localStorage.getItem(key))
        );
    } catch {
        return false;
    }
}

export function hasCustomerSession() {
    return Boolean(getCustomerToken());
}

export function getCustomerTokenPayload() {
    const token = getCustomerToken();
    if (!token || typeof token !== 'string') return null;
    try {
        const parts = token.split('.');
        if (parts.length !== 3) return null;
        const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(
            atob(base64)
                .split('')
                .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
                .join('')
        );
        return JSON.parse(jsonPayload);
    } catch {
        return null;
    }
}

export function isTokenExpired() {
    const payload = getCustomerTokenPayload();
    if (!payload?.exp) return false;
    // Buffer 5 giây tránh lệch đồng hồ
    return Date.now() >= (payload.exp * 1000 - 5000);
}

export function getCustomerRoles() {
    const payload = getCustomerTokenPayload();
    if (!payload) return [];
    const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload['role'];
    if (Array.isArray(role)) return role;
    if (typeof role === 'string' && role.trim()) return [role];
    return [];
}

export function hasCustomerRole() {
    const roles = getCustomerRoles();
    return roles.includes('Customer');
}

// Bổ sung tham số rememberMe
export function saveCustomerSession(response, rememberMe = true) {
    const accessToken = response?.accessToken ?? response?.token ?? response;
    setCustomerAccessToken(accessToken, rememberMe);
}

export function getCurrentCustomer() {
    return currentCustomer;
}

export function setCurrentCustomer(customer) {
    currentCustomer = customer && typeof customer === 'object' ? customer : null;
}

export function clearCustomerSession() {
    customerAccessToken = null;
    currentCustomer = null;
    try {
        // Xóa ở cả 2 nơi khi đăng xuất
        localStorage.removeItem(CUSTOMER_TOKEN_STORAGE_KEY);
        sessionStorage.removeItem(CUSTOMER_TOKEN_STORAGE_KEY);
        
        for (const key of LEGACY_TOKEN_STORAGE_KEYS) {
            localStorage.removeItem(key);
            sessionStorage.removeItem(key);
        }
    } catch {
        // Storage may be unavailable.
    }
    removeLegacyAuthStorage();
}

export function requireCustomerSession() {
    if (hasCustomerSession()) return true;
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;
    const loginUrl = new URL('../auth/login_register.html', location.href);
    loginUrl.searchParams.set('returnUrl', returnUrl);
    location.replace(loginUrl);
    return false;
}
