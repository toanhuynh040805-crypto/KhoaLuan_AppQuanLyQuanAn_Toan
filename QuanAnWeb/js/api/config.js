// Nguồn duy nhất cấu hình địa chỉ Backend. Khi deploy chỉ cần sửa PRODUCTION_API_ORIGIN.
const PRODUCTION_API_ORIGIN = 'https://TEN-MIEN-BACKEND-CUA-BAN'; // <-- sửa khi deploy

const host = globalThis.location?.hostname ?? '';
const isLocal = host === '' || host === 'localhost' || host === '127.0.0.1';

// Dùng cùng host với trang web (localhost hoặc 127.0.0.1) để cookie refresh được coi là same-site.
export const API_ORIGIN = isLocal ? `http://${host || 'localhost'}:5000` : PRODUCTION_API_ORIGIN;
export const API_BASE_URL = `${API_ORIGIN}/api`;

// Thông tin chuẩn của nhà hàng GustoPOS
export const RESTAURANT_INFO = {
    name: 'GustoPOS',
    address: '140 Lê Trọng Tấn, phường Tây Thạnh, quận Tân Phú, TP. Hồ Chí Minh',
    phone: '1900 1234',
    email: 'booking@gustopos.vn',
    googleMapsDirectionsUrl: 'https://www.google.com/maps/dir/?api=1&destination=140+L%C3%AA+Tr%E1%BB%8Dng+T%E1%BA%A5n%2C+ph%C6%B0%E1%BB%9Dng+T%C3%A2y+Th%E1%BA%A1nh%2C+qu%E1%BA%ADn+T%C3%A2n+Ph%C3%BA%2C+TP.+H%E1%BB%93+Ch%C3%AD+Minh'
};

// Backend trả ảnh dạng "/images/menu/xxx.jpg"; đổi thành URL đầy đủ trỏ về backend.
export function resolveAssetUrl(path) {
    if (typeof path !== 'string' || !path.trim()) return null;
    const value = path.trim();
    if (/^(https?:|data:)/i.test(value)) return value;
    return `${API_ORIGIN}${value.startsWith('/') ? '' : '/'}${value}`;
}