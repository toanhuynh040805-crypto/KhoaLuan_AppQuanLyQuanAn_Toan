import { API_ORIGIN } from '../api/config.js';
import { getCurrentCustomer, getCustomerToken, hasCustomerSession } from '../api/session.js?auth-session-2';

let connection = null;
let isStarting = false;
let startPromise = null;
const listeners = new Map();
let fallbackTimer = null;
let lastSyncTimestamp = Date.now();

// 1. Tải thư viện SignalR từ CDN khi cần thiết
let signalRLoaderPromise = null;
export async function loadSignalRLibrary() {
    if (globalThis.signalR) return globalThis.signalR;
    if (signalRLoaderPromise) return signalRLoaderPromise;

    signalRLoaderPromise = new Promise((resolve) => {
        if (globalThis.signalR) {
            resolve(globalThis.signalR);
            return;
        }

        const existingScript = document.querySelector('script[src*="signalr"]');
        if (existingScript) {
            if (globalThis.signalR) {
                resolve(globalThis.signalR);
                return;
            }
            existingScript.addEventListener('load', () => resolve(globalThis.signalR));
            existingScript.addEventListener('error', () => resolve(null));
            return;
        }

        const script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js';
        script.crossOrigin = 'anonymous';
        script.onload = () => resolve(globalThis.signalR);
        script.onerror = () => {
            console.warn('[Realtime] Không thể tải thư viện SignalR từ CDN.');
            resolve(null);
        };
        document.head.appendChild(script);
    });

    return signalRLoaderPromise;
}

// 2. Phát tán sự kiện tới các listeners đã đăng ký
function dispatch(event, data) {
    const handlers = listeners.get(event);
    if (handlers) {
        handlers.forEach((callback) => {
            try {
                callback(data);
            } catch (err) {
                console.error(`[Realtime] Lỗi khi xử lý sự kiện "${event}":`, err);
            }
        });
    }

    // Phát custom event trên window để các thành phần độc lập có thể lắng nghe nếu muốn
    try {
        globalThis.dispatchEvent?.(new CustomEvent(`gusto:${event}`, { detail: data }));
    } catch {
        // Ignored
    }
}

// 3. Đăng ký & Hủy đăng ký listener
export function on(event, callback) {
    if (typeof callback !== 'function') return () => {};
    if (!listeners.has(event)) {
        listeners.set(event, new Set());
    }
    listeners.get(event).add(callback);

    // Tự động khởi tạo kết nối realtime nếu chưa bắt đầu
    void initRealtime();

    return () => off(event, callback);
}

export function off(event, callback) {
    const handlers = listeners.get(event);
    if (handlers) {
        handlers.delete(callback);
        if (handlers.size === 0) {
            listeners.delete(event);
        }
    }
}

// 4. Gia nhập nhóm khách hàng riêng tư nếu đã xác thực
async function joinPrivateCustomerGroup() {
    if (!connection || connection.state !== 'Connected') return;
    const customer = getCurrentCustomer();
    const customerId = customer?.customerId ?? customer?.CustomerId;
    if (customerId && hasCustomerSession()) {
        try {
            await connection.invoke('JoinCustomerGroup', Number(customerId));
        } catch {
            // Backend có thể đã tự động gia nhập trong OnConnectedAsync
        }
    }
}

// 5. Khởi tạo kết nối SignalR dùng chung
export async function initRealtime() {
    if (connection && connection.state !== 'Disconnected') {
        return connection;
    }
    if (startPromise) return startPromise;

    startPromise = (async () => {
        try {
            isStarting = true;
            const signalR = await loadSignalRLibrary();
            if (!signalR) {
                startFallbackPolling();
                return null;
            }

            if (!connection) {
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_ORIGIN}/hubs/order`, {
                        accessTokenFactory: () => getCustomerToken() || ''
                    })
                    .withAutomaticReconnect([0, 1500, 3000, 5000, 10000, 30000])
                    .configureLogging(signalR.LogLevel.None)
                    .build();

                // Lắng nghe các sự kiện nghiệp vụ thực tế từ Backend
                const registerHubEvent = (hubEvent, mappedEvents = []) => {
                    connection.on(hubEvent, (data) => {
                        dispatch(hubEvent, data);
                        mappedEvents.forEach((alias) => dispatch(alias, data));
                    });
                };

                // Danh mục sự kiện
                registerHubEvent('NewCustomerNotification', ['notification']);
                registerHubEvent('TableStatusUpdated', ['table']);
                registerHubEvent('PaymentCompleted', ['payment', 'order']);
                registerHubEvent('OrderUpdated', ['order']);
                registerHubEvent('OrderCreated', ['order']);
                registerHubEvent('DishStatusChanged', ['dish', 'menu']);
                registerHubEvent('MenuItemUpdated', ['dish', 'menu']);
                registerHubEvent('ReviewsChanged', ['review']);
                registerHubEvent('CustomerProfileUpdated', ['profile']);
                registerHubEvent('ReservationUpdated', ['reservation']);

                // Vòng đời kết nối
                connection.onreconnecting(() => {
                    dispatch('reconnecting');
                });

                connection.onreconnected(async () => {
                    lastSyncTimestamp = Date.now();
                    await joinPrivateCustomerGroup();
                    dispatch('reconnected');
                    dispatch('sync');
                });

                connection.onclose(() => {
                    dispatch('closed');
                    startFallbackPolling();
                });
            }

            if (connection.state === 'Disconnected') {
                await connection.start();
                lastSyncTimestamp = Date.now();
                await joinPrivateCustomerGroup();
                dispatch('connected');
            }

            return connection;
        } catch (err) {
            console.warn('[Realtime] Kết nối thời gian thực tạm thời gián đoạn. Chuyển sang cơ chế đồng bộ dự phòng.');
            startFallbackPolling();
            return null;
        } finally {
            isStarting = false;
            startPromise = null;
        }
    })();

    return startPromise;
}

// 6. Dừng kết nối
export async function stopRealtime() {
    if (connection) {
        try {
            await connection.stop();
        } catch {
            // Ignored
        }
        connection = null;
    }
}

// 7. Polling dự phòng khi SignalR mất kết nối
function startFallbackPolling() {
    if (fallbackTimer) return;
    fallbackTimer = setInterval(() => {
        if (document.visibilityState === 'visible') {
            // Thử tái kết nối SignalR
            if (!connection || connection.state === 'Disconnected') {
                void initRealtime();
            }
            // Kích hoạt đồng bộ dữ liệu cho các trang
            dispatch('sync');
        }
    }, 30000);
}

// 8. Đồng bộ khi người dùng quay lại tab trình duyệt
if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            const elapsed = Date.now() - lastSyncTimestamp;
            if (elapsed > 15000) {
                lastSyncTimestamp = Date.now();
                if (!connection || connection.state === 'Disconnected') {
                    void initRealtime();
                }
                dispatch('sync');
            }
        }
    });

    // Đồng bộ khi đăng nhập / đăng xuất
    globalThis.addEventListener?.('gusto:session-updated', async () => {
        if (connection && connection.state === 'Connected') {
            await joinPrivateCustomerGroup();
        } else {
            void initRealtime();
        }
        dispatch('sync');
    });

    globalThis.addEventListener?.('gusto:unauthorized', async () => {
        // Tái kết nối nặc danh để vẫn nhận được sự kiện công khai (thực đơn, bàn)
        if (connection && connection.state === 'Connected') {
            try {
                await connection.stop();
            } catch {
                // Ignored
            }
        }
        void initRealtime();
        dispatch('sync');
    });
}

export const startRealtime = initRealtime;
export const start = initRealtime;
export const init = initRealtime;
export const stop = stopRealtime;

export const realtimeClient = {
    init: initRealtime,
    start: initRealtime,
    stop: stopRealtime,
    on,
    off,
    dispatch,
    getConnection: () => connection,
    getState: () => connection?.state || 'Disconnected'
};

if (typeof globalThis !== 'undefined') {
    globalThis.__gustoRealtime = realtimeClient;
    globalThis.addEventListener?.('gusto:trigger-realtime', (e) => {
        if (e.detail?.event) dispatch(e.detail.event, e.detail.data);
    });
}

export default realtimeClient;

