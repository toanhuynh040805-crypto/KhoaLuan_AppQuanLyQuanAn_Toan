import { clearCustomerSession, getCustomerToken, setCustomerAccessToken } from './session.js?auth-session-2';
import { API_BASE_URL } from './config.js';
let refreshPromise = null;

const statusMessages = {
    400: 'Thông tin gửi lên chưa hợp lệ. Vui lòng kiểm tra lại.',
    401: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
    403: 'Bạn không có quyền thực hiện thao tác này.',
    404: 'Không tìm thấy dữ liệu yêu cầu.',
    409: 'Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.',
    500: 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'
};

export class ApiError extends Error {
    constructor(message, { status = null, code = 'API_ERROR', cause } = {}) {
        super(message, { cause });
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
    }
}

export function backendRequired(feature) {
    return new ApiError(`Tính năng ${feature} đang chờ Backend cung cấp API Customer chính thức.`, {
        code: 'BACKEND_REQUIRED'
    });
}

async function parseResponse(response) {
    if (response.status === 204) return null;

    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('json')) {
        try {
            return await response.json();
        } catch {
            throw new ApiError('Máy chủ trả về dữ liệu không hợp lệ.', { status: response.status, code: 'INVALID_RESPONSE' });
        }
    }

    const text = await response.text();
    return text || null;
}

function isSessionEndpoint(endpoint) {
    return /^\/customers\/(login|register|refresh|logout)(?:\?|$)/i.test(endpoint);
}

async function refreshAccessToken() {
    if (!refreshPromise) {
        refreshPromise = (async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/customers/refresh`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { Accept: 'application/json' }
                });
                if (!response.ok) return null;

                const data = await parseResponse(response);
                const token = data?.accessToken ?? data?.AccessToken;
                if (typeof token !== 'string' || token.trim() === '') return null;
                setCustomerAccessToken(token);
                return token;
            } catch {
                return null;
            } finally {
                refreshPromise = null;
            }
        })();
    }
    return refreshPromise;
}

async function request(endpoint, options = {}, allowRefreshRetry = true) {
    const token = getCustomerToken();
    const headers = new Headers(options.headers ?? {});
    if (options.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
    if (token && !headers.has('Authorization')) headers.set('Authorization', `Bearer ${token}`);

    let response;
    try {
        response = await fetch(`${API_BASE_URL}${endpoint}`, { credentials: 'include', ...options, headers });
    } catch (error) {
        throw new ApiError('Không thể kết nối máy chủ. Hãy kiểm tra kết nối mạng rồi thử lại.', {
            code: 'NETWORK_ERROR',
            cause: error
        });
    }

    if (response.status === 401 && token && !isSessionEndpoint(endpoint)) {
        if (allowRefreshRetry) {
            const refreshedToken = await refreshAccessToken();
            if (refreshedToken) return request(endpoint, options, false);
        }
        clearCustomerSession();
        globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
    }

    const data = await parseResponse(response);

    if (!response.ok) {
        const serverMessage = data && typeof data === 'object'
            ? (data.message ?? data.Message ?? data.title)
            : null;
        const serverCode = data && typeof data === 'object'
            ? (data.code ?? data.Code)
            : null;
        throw new ApiError(typeof serverMessage === 'string' ? serverMessage : (statusMessages[response.status] ?? 'Không thể hoàn tất yêu cầu.'), {
            status: response.status,
            code: typeof serverCode === 'string' ? serverCode : `HTTP_${response.status}`
        });
    }

    return data;
}

export const apiClient = {
    get: (endpoint) => request(endpoint, { method: 'GET' }),
    post: (endpoint, body) => request(endpoint, { method: 'POST', body: JSON.stringify(body) }),
    put: (endpoint, body) => request(endpoint, { method: 'PUT', body: JSON.stringify(body) }),
    delete: (endpoint) => request(endpoint, { method: 'DELETE' })
};