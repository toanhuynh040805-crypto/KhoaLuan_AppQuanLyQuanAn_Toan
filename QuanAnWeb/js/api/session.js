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
        const params = new URLSearchParams(location.search);
        const token = params.get('accessToken') ?? params.get('token');
        if (typeof token === 'string' && token.trim()) {
            setCustomerAccessToken(token);
            const cleanUrl = new URL(location.href);
            cleanUrl.searchParams.delete('accessToken');
            cleanUrl.searchParams.delete('token');
            if (cleanUrl.search === '') {
                history.replaceState(null, '', cleanUrl.pathname + cleanUrl.hash);
            } else {
                history.replaceState(null, '', `${cleanUrl.pathname}${cleanUrl.search}${cleanUrl.hash}`);
            }
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

export function setCustomerAccessToken(token) {
    if (typeof token !== 'string' || token.trim() === '') {
        throw new TypeError('Customer authentication response did not include an access token.');
    }
    customerAccessToken = token;
    currentCustomer = null;
    try {
        localStorage.setItem(CUSTOMER_TOKEN_STORAGE_KEY, token);
        for (const key of LEGACY_TOKEN_STORAGE_KEYS) {
            localStorage.setItem(key, token);
        }
    } catch {
        // Storage may be unavailable.
    }
}

export function hasCustomerSession() {
    return Boolean(getCustomerToken());
}

export function saveCustomerSession(response) {
    const accessToken = response?.accessToken ?? response?.token ?? response;
    setCustomerAccessToken(accessToken);
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
        localStorage.removeItem(CUSTOMER_TOKEN_STORAGE_KEY);
        for (const key of LEGACY_TOKEN_STORAGE_KEYS) {
            localStorage.removeItem(key);
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
