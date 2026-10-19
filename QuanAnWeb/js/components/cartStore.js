const CART_KEY = 'gusto_cart';

export function readCart() {
    try {
        const value = JSON.parse(localStorage.getItem(CART_KEY) ?? '[]');
        if (!Array.isArray(value)) return [];
        return value.flatMap((item) => {
            const id = item?.id ?? item?.Id;
            const name = item?.name ?? item?.Name;
            const numericId = Number(id);
            const price = Number(item?.price ?? item?.Price);
            const quantity = Number(item?.quantity ?? item?.Quantity);
            
            if (id == null || !Number.isInteger(numericId) || numericId < 1 || typeof name !== 'string' || !Number.isFinite(price) || price < 0
                || !Number.isInteger(quantity) || quantity < 1 || quantity > 99) return [];
                
            // Đã sửa: Gỡ bỏ .slice(0, 255) để không cắt xén ngầm dữ liệu ghi chú của khách
            return [{ id: String(id), name, price, quantity, note: String(item?.note ?? item?.Note ?? '') }];
        });
    } catch {
        return [];
    }
}

export function writeCart(items) {
    try {
        localStorage.setItem(CART_KEY, JSON.stringify(items));
        return true;
    } catch {
        return false;
    }
}