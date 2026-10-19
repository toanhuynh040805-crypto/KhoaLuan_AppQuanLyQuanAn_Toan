// Nguồn duy nhất cấu hình địa chỉ Backend. Khi deploy chỉ cần sửa PRODUCTION_API_ORIGIN.
const PRODUCTION_API_ORIGIN = 'https://TEN-MIEN-BACKEND-CUA-BAN'; // <-- sửa khi deploy

const host = globalThis.location?.hostname ?? '';
const isLocal = host === '' || host === 'localhost' || host === '127.0.0.1';

// Dùng cùng host với trang web (localhost hoặc 127.0.0.1) để cookie refresh được coi là same-site.
export const API_ORIGIN = isLocal ? `http://${host || 'localhost'}:5000` : PRODUCTION_API_ORIGIN;
export const API_BASE_URL = `${API_ORIGIN}/api`;
// Backend trả ảnh dạng "/images/menu/xxx.jpg"; đổi thành URL đầy đủ trỏ về backend.
export function resolveAssetUrl(path) {
    if (typeof path !== 'string' || !path.trim()) return null;
    const value = path.trim();
    if (/^(https?:|data:)/i.test(value)) return value;
    return `${API_ORIGIN}${value.startsWith('/') ? '' : '/'}${value}`;
}