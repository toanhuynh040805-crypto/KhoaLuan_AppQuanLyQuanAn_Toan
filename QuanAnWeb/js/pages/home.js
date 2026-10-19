import { menuApi } from '../api/menuApi.js?auth-session-2';
import { readCart, writeCart } from '../components/cartStore.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { renderDishes, renderEmpty, renderError, renderLoading } from '../components/menuView.js';

const menuElement = document.querySelector('[data-home-featured]');
const statusElement = document.querySelector('[data-home-menu-status]');

setupSessionNavigation();

if (menuElement && statusElement) {
    async function loadFeatured() {
        renderLoading(menuElement, 'Đang tải thực đơn...');
        statusElement.textContent = '';
        try {
            const dishes = await menuApi.getDishes();
            const availableDishes = dishes.filter((dish) => dish.isAvailable).slice(0, 6);
            if (availableDishes.length === 0) {
                renderEmpty(menuElement, 'Hiện chưa có món nào khả dụng trong thực đơn.');
                return;
            }
            renderDishes(menuElement, availableDishes, (dish) => {
                // Đã bổ sung: Chuẩn hóa và kiểm tra dữ liệu món ăn hợp lệ trước khi đẩy vào giỏ
                const dishId = dish?.id ?? dish?.Id;
                const name = dish?.name ?? dish?.Name;
                const price = Number(dish?.price ?? dish?.Price);

                if (dishId == null || typeof name !== 'string' || !Number.isFinite(price) || price < 0 || dish.isAvailable === false) {
                    statusElement.textContent = 'Không thể thêm. Dữ liệu món ăn không hợp lệ.';
                    statusElement.classList.add('text-error');
                    return;
                }

                const items = readCart();
                const existing = items.find((item) => item.id === String(dishId));
                
                if (existing) {
                    existing.quantity = Math.min(existing.quantity + 1, 99);
                } else {
                    items.push({ id: String(dishId), name: name, price: price, quantity: 1, note: '' });
                }
                
                writeCart(items);
                statusElement.textContent = 'Đã thêm món vào giỏ hàng.';
                statusElement.classList.remove('text-error');

                // Tự động ẩn thông báo sau 3 giây
                setTimeout(() => {
                    if (statusElement.textContent === 'Đã thêm món vào giỏ hàng.') {
                        statusElement.textContent = '';
                    }
                }, 3000);
            });
        } catch (error) {
            renderError(menuElement, error?.message ?? 'Không thể tải thực đơn.', loadFeatured);
        }
    }

    loadFeatured();
}