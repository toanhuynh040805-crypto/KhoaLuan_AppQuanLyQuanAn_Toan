import { clearCustomerSession, getCustomerToken, isCustomerSessionRemembered, setCustomerAccessToken } from './session.js?auth-session-2';
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
    constructor(message, { status = null, code = 'API_ERROR', field = null, errors = null, data = null, cause } = {}) {
        super(message, { cause });
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.field = field;
        this.errors = errors;
        this.data = data;
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
    return /^\/(customers\/(login|register|refresh|logout)|auth\/(forgot-password|reset-password|google\/(signin|callback|complete-profile)))(?:\?|$)/i.test(endpoint);
}

function isAnonymousEndpoint(endpoint) {
    // Lưu ý: /customers/refresh KHÔNG phải endpoint nặc danh vì Backend yêu cầu [Authorize(Roles = "Customer")].
    // Phải gửi kèm Authorization: Bearer token để làm mới phiên.
    return /^\/(customers\/(login|register)|auth\/(forgot-password|reset-password|google\/(signin|callback|complete-profile)))(?:\?|$)/i.test(endpoint);
}

async function refreshAccessToken() {
    if (!refreshPromise) {
        refreshPromise = (async () => {
            try {
                const token = getCustomerToken();
                const headers = { Accept: 'application/json' };
                if (token) {
                    headers['Authorization'] = `Bearer ${token}`;
                }
                const response = await fetch(`${API_BASE_URL}/customers/refresh`, {
                    method: 'POST',
                    credentials: 'include',
                    headers
                });
                if (!response.ok) return null;

                const data = await parseResponse(response);
                const newToken = data?.accessToken ?? data?.AccessToken;
                if (typeof newToken !== 'string' || newToken.trim() === '') return null;
                setCustomerAccessToken(newToken, isCustomerSessionRemembered());
                return newToken;
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
    const isAnonymous = isAnonymousEndpoint(endpoint);
    const token = isAnonymous ? null : getCustomerToken();
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

    if (response.status === 401 && token && !isAnonymous) {
        if (allowRefreshRetry) {
            const refreshedToken = await refreshAccessToken();
            if (refreshedToken) return request(endpoint, options, false);
        }
        clearCustomerSession();
        globalThis.dispatchEvent?.(new CustomEvent('gusto:unauthorized'));
    }

    const data = await parseResponse(response);

    if (!response.ok) {
        let serverMessage = null;
        let serverCode = null;
        let serverField = null;
        let serverErrors = null;

        if (data && typeof data === 'object') {
            serverMessage = data.message ?? data.Message ?? data.title ?? data.error ?? data.Error;
            serverCode = data.code ?? data.Code;
            serverField = data.field ?? data.Field;
            serverErrors = data.errors ?? data.Errors;
        } else if (typeof data === 'string' && data.trim()) {
            serverMessage = data.trim();
        }

        throw new ApiError(typeof serverMessage === 'string' && serverMessage ? serverMessage : (statusMessages[response.status] ?? 'Không thể hoàn tất yêu cầu.'), {
            status: response.status,
            code: typeof serverCode === 'string' ? serverCode : `HTTP_${response.status}`,
            field: serverField,
            errors: serverErrors,
            data
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
