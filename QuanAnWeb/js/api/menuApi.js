import { apiClient } from './client.js?auth-session-2';
import { resolveAssetUrl } from './config.js';
function normalizeDish(dish) {
    const id = dish.Id ?? dish.id;
    const name = dish.Name ?? dish.name;
    const price = Number(dish.Price ?? dish.price);
    const availability = dish.IsAvailable ?? dish.isAvailable;
    if ((typeof id !== 'string' && typeof id !== 'number') || typeof name !== 'string' || !Number.isFinite(price) || price < 0) {
        throw new TypeError('Menu item response contains invalid fields.');
    }
    return {
        id,
        name,
        price,
        imageUrl: resolveAssetUrl(dish.ImageUrl ?? dish.imageUrl),
        description: typeof (dish.Description ?? dish.description) === 'string' ? (dish.Description ?? dish.description) : null,
        isAvailable: availability === true,
        categoryId: dish.CategoryId ?? dish.categoryId,
        categoryName: typeof (dish.CategoryName ?? dish.categoryName) === 'string' ? (dish.CategoryName ?? dish.categoryName) : ''
    };
}

function normalizeCategory(category) {
    const id = category.Id ?? category.id;
    const name = category.Name ?? category.name;
    if ((typeof id !== 'string' && typeof id !== 'number') || typeof name !== 'string') {
        throw new TypeError('Category response contains invalid fields.');
    }
    return {
        id,
        name,
        type: category.Type ?? category.type
    };
}

export const menuApi = {
    getDishes: async (categoryId = '') => {
        const query = categoryId ? `?categoryId=${encodeURIComponent(categoryId)}` : '';
        // Đã sửa: /Menu -> /menu
        const data = await apiClient.get(`/menu${query}`);
        if (!Array.isArray(data)) throw new TypeError('Menu response must be an array.');
        return data.map(normalizeDish);
    },

    getCategories: async () => {
        // Đã sửa: /Categories -> /categories
        const data = await apiClient.get('/categories');
        if (!Array.isArray(data)) throw new TypeError('Category response must be an array.');
        return data.map(normalizeCategory);
    },

    getDishDetail: async (dishId) => {
        // Đã sửa: /Menu/ -> /menu/
        return normalizeDish(await apiClient.get(`/menu/${encodeURIComponent(dishId)}`));
    }
};