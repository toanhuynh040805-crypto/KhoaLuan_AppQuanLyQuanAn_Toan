import { menuApi } from '../api/menuApi.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

const menuElement = document.querySelector('[data-home-featured]');
const statusElement = document.querySelector('[data-home-menu-status]');
const placeholderImage = './assets/menu-placeholder.svg';

setupSessionNavigation();

function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

function createDishCard(dish, featured) {
    const card = createElement(
        'a',
        featured
            ? 'lg:col-span-7 min-w-0 rounded-2xl overflow-hidden relative group shadow-sm bg-surface-container-low border border-outline-variant/40 block'
            : 'min-w-0 rounded-2xl overflow-hidden relative group shadow-sm bg-surface-container-low border border-outline-variant/40 flex flex-col sm:flex-row items-stretch flex-1'
    );
    card.href = './pages/menu/index.html';

    const imageArea = createElement('div', featured ? 'relative h-80 sm:h-[420px] overflow-hidden bg-surface-container-high' : 'relative sm:w-2/5 h-48 sm:h-auto overflow-hidden bg-surface-container-high');
    const image = createElement('img', 'w-full h-full object-cover transition-transform duration-700 group-hover:scale-105');
    const name = typeof dish.name === 'string' && dish.name.trim() ? dish.name.trim() : 'Món ăn';
    image.alt = name;
    image.loading = 'lazy';
    image.src = dish.imageUrl || placeholderImage;
    image.dataset.fallbackApplied = dish.imageUrl ? 'false' : 'true';
    const imageIcon = createElement('span', 'material-symbols-outlined absolute inset-0 hidden items-center justify-center text-5xl text-on-surface-variant', 'restaurant_menu');
    imageIcon.setAttribute('aria-hidden', 'true');
    image.addEventListener('error', () => {
        if (image.dataset.fallbackApplied !== 'true') {
            image.dataset.fallbackApplied = 'true';
            image.src = placeholderImage;
            return;
        }
        image.classList.add('hidden');
        imageIcon.classList.remove('hidden');
        imageIcon.classList.add('flex');
    });
    imageArea.append(image, imageIcon);

    const content = createElement('div', featured
        ? 'absolute inset-0 bg-gradient-to-t from-black/85 via-black/20 to-transparent flex flex-col justify-end p-6 md:p-8'
        : 'min-w-0 p-5 sm:w-3/5 flex flex-col justify-center bg-surface-container-lowest');
    const category = (dish.categoryName || '').trim();
    if (category) {
        content.append(createElement('span', featured
            ? 'w-fit bg-primary text-on-primary text-xs font-bold px-3 py-1 rounded-md shadow mb-2'
            : 'text-xs font-semibold text-tertiary uppercase tracking-wider mb-1 break-words', category));
    }

    const title = createElement('h3', featured
        ? 'text-2xl font-bold text-white mb-2 line-clamp-2 break-words'
        : 'text-lg font-bold text-on-surface mb-1 line-clamp-2 break-words', name);
    title.title = name;
    content.append(title);

    const descriptionText = typeof dish.description === 'string' ? dish.description.trim() : '';
    if (descriptionText) {
        const description = createElement('p', featured
            ? 'text-sm text-white/90 font-light leading-relaxed line-clamp-3 break-words'
            : 'text-xs text-on-surface-variant line-clamp-2 leading-relaxed break-words', descriptionText);
        description.title = descriptionText;
        content.append(description);
    }

    if (Number.isFinite(dish.price)) {
        content.append(createElement('p', featured
            ? 'mt-3 font-semibold text-white'
            : 'mt-2 font-semibold text-primary', `${new Intl.NumberFormat('vi-VN').format(dish.price)}đ`));
    }

    card.append(imageArea, content);
    return card;
}

function renderFeaturedDishes(dishes) {
    const cards = dishes.slice(0, 3).map((dish, index) => createDishCard(dish, index === 0));
    if (!cards.length) {
        menuElement.replaceChildren(createElement('p', 'lg:col-span-12 rounded-xl border border-outline-variant bg-surface-container-lowest p-6 text-center text-on-surface-variant', 'Hiện chưa có món ăn đặc sắc.'));
        return;
    }

    if (cards.length > 1) {
        const supportingCards = createElement('div', 'lg:col-span-5 min-w-0 flex flex-col gap-6 justify-between');
        supportingCards.append(...cards.slice(1));
        menuElement.replaceChildren(cards[0], supportingCards);
    } else {
        menuElement.replaceChildren(cards[0]);
    }
}

if (menuElement && statusElement) {
    async function loadFeatured() {
        menuElement.replaceChildren(createElement('p', 'lg:col-span-12 animate-pulse rounded-xl border border-outline-variant bg-surface-container-lowest p-6 text-center text-on-surface-variant', 'Đang tải món ăn đặc sắc...'));
        statusElement.textContent = '';
        try {
            const dishes = await menuApi.getFeaturedDishes();
            renderFeaturedDishes(dishes);
        } catch {
            menuElement.replaceChildren();
            statusElement.textContent = 'Không thể tải món ăn đặc sắc. Vui lòng thử lại.';
            const retry = createElement('button', 'mx-auto mb-5 block rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-on-primary hover:opacity-90', 'Thử lại');
            retry.type = 'button';
            retry.addEventListener('click', loadFeatured);
            menuElement.append(retry);
        }
    }

    async function reloadFeaturedSilently() {
        try {
            const dishes = await menuApi.getFeaturedDishes();
            renderFeaturedDishes(dishes);
        } catch (e) {
            console.debug('Silent featured reload failed:', e);
        }
    }

    realtimeClient.on('MenuItemUpdated', reloadFeaturedSilently);
    realtimeClient.on('DishStatusChanged', reloadFeaturedSilently);
    realtimeClient.on('sync', reloadFeaturedSilently);
    realtimeClient.start().catch(() => {});

    void loadFeatured();
}
