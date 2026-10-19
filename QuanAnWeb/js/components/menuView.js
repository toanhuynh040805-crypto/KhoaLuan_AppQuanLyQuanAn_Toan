const categoryStyles = {
    active: 'px-6 py-2 rounded-full bg-primary text-on-primary font-label-lg text-label-lg whitespace-nowrap shadow-sm',
    inactive: 'px-6 py-2 rounded-full bg-surface-container-highest text-on-surface-variant font-label-lg text-label-lg whitespace-nowrap hover:bg-surface-variant transition-colors'
};

function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

export function renderLoading(container, message) {
    container.replaceChildren(createElement('p', 'bg-surface-container-lowest rounded-xl border border-surface-variant p-6 text-on-surface-variant', message));
}

export function renderEmpty(container, message) {
    container.replaceChildren(createElement('p', 'bg-surface-container-lowest rounded-xl border border-surface-variant p-6 text-on-surface-variant', message));
}

export function renderError(container, message, onRetry) {
    const state = createElement('div', 'bg-surface-container-lowest rounded-xl border border-error p-6 text-center');
    state.append(createElement('p', 'font-body-md text-body-md text-error mb-4', message));

    const retryButton = createElement('button', 'bg-primary text-on-primary font-label-lg text-label-lg py-2 px-6 rounded-lg hover:opacity-90 transition-opacity', 'Thử lại');
    retryButton.type = 'button';
    retryButton.addEventListener('click', onRetry);
    state.append(retryButton);
    container.replaceChildren(state);
}

export function renderCategories(container, categories, selectedCategoryId, onSelect) {
    const allButton = createElement('button', categoryStyles[selectedCategoryId === '' ? 'active' : 'inactive'], 'Tất cả');
    allButton.type = 'button';
    allButton.setAttribute('aria-pressed', String(selectedCategoryId === ''));
    allButton.addEventListener('click', () => onSelect(''));

    const buttons = [allButton];
    categories.forEach((category) => {
        const categoryId = String(category.id);
        const isSelected = categoryId === selectedCategoryId;
        const button = createElement('button', categoryStyles[isSelected ? 'active' : 'inactive'], category.name);
        button.type = 'button';
        button.setAttribute('aria-pressed', String(isSelected));
        button.addEventListener('click', () => onSelect(categoryId));
        buttons.push(button);
    });

    container.replaceChildren(...buttons);
}

export function renderDishes(container, dishes, onAddToCart, onShowDetail) {
    const cards = dishes.map((dish) => {
        const available = dish.isAvailable !== false;
        
        const card = createElement('article', `flex flex-col h-full bg-surface-container-lowest rounded-xl shadow-sm overflow-hidden border border-surface-variant ${available ? ' hover:shadow-md transition-shadow' : ' opacity-60 relative'}`);
        
        const imageArea = createElement('div', `h-48 shrink-0 w-full relative overflow-hidden bg-surface-container-low flex items-center justify-center ${available ? '' : ' grayscale'}`);
        const imageUrl = dish.imageUrl;
        if (typeof imageUrl === 'string' && imageUrl.trim()) {
            try {
                const resolvedUrl = new URL(imageUrl, location.href);
                if (resolvedUrl.protocol === 'https:' || resolvedUrl.protocol === 'http:') {
                    const image = createElement('img', 'h-full w-full object-cover');
                    image.src = resolvedUrl.href;
                    image.alt = dish.name ?? '';
                    image.loading = 'lazy';
                    image.addEventListener('error', () => {
                        image.replaceWith(createElement('span', 'material-symbols-outlined text-5xl text-on-surface-variant', 'restaurant_menu'));
                    }, { once: true });
                    imageArea.append(image);
                }
            } catch {}
        }
        if (!imageArea.hasChildNodes()) {
            const imagePlaceholder = createElement('span', 'material-symbols-outlined text-5xl text-on-surface-variant', 'restaurant_menu');
            imagePlaceholder.setAttribute('aria-hidden', 'true');
            imageArea.append(imagePlaceholder);
        }

        if (dish.categoryName) {
            const categoryBadge = createElement('span', 'absolute top-3 left-3 bg-primary-container bg-opacity-90 text-on-primary-container px-2 py-1 rounded font-label-md text-label-md', dish.categoryName);
            imageArea.append(categoryBadge);
        }

        // THÊM NÚT MENU BA CHẤM (⋮) Ở GÓC TRÊN BÊN PHẢI ẢNH
        const detailBtn = createElement('button', 'absolute top-3 right-3 w-8 h-8 rounded-full bg-surface-container-lowest/90 backdrop-blur-sm text-on-surface flex items-center justify-center shadow hover:bg-surface-container-lowest transition-colors', '');
        detailBtn.type = 'button';
        detailBtn.title = 'Xem thông tin chi tiết và thành phần';
        detailBtn.innerHTML = '<span class="material-symbols-outlined text-lg">more_vert</span>';
        detailBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (typeof onShowDetail === 'function') onShowDetail(dish);
        });
        imageArea.append(detailBtn);

        if (!available) {
            const soldOut = createElement('div', 'absolute inset-0 z-10 flex items-center justify-center pointer-events-none');
            soldOut.append(createElement('span', 'bg-surface-container-lowest border-2 border-error text-error font-headline-sm text-headline-sm font-bold py-2 px-6 transform -rotate-12 rounded', 'HẾT MÓN'));
            card.append(soldOut);
        }

        const content = createElement('div', 'p-4 flex-1 flex flex-col');
        const heading = createElement('div', 'flex justify-between items-start mb-2 gap-2');
        
        // Hỗ trợ tên dài tự động xuống dòng tối đa 2 dòng kèm dấu "..."
        const titleElement = createElement('h3', 'font-headline-sm text-headline-sm text-on-surface line-clamp-2', dish.name ?? '');
        titleElement.title = dish.name ?? '';
        heading.append(titleElement);

        const price = Number(dish.price);
        heading.append(createElement('span', 'font-label-lg text-label-lg text-primary font-bold whitespace-nowrap shrink-0 mt-0.5', Number.isFinite(price) ? `${new Intl.NumberFormat('vi-VN').format(price)}đ` : ''));

        content.append(heading);
        
        if (dish.description) {
            const descElement = createElement('p', 'font-body-sm text-body-sm text-on-surface-variant line-clamp-3 mb-4', dish.description);
            descElement.title = dish.description;
            content.append(descElement);
        }
        
        const addButton = createElement('button', 'mt-auto flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 font-label-md text-label-md font-semibold text-on-primary transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50', 'Thêm vào giỏ');
        addButton.type = 'button';
        addButton.disabled = !available;
        addButton.addEventListener('click', () => onAddToCart(dish));
        content.append(addButton);
        
        card.append(imageArea, content);
        return card;
    });

    container.replaceChildren(...cards);
}

/** Render Tab/Drawer nhỏ trượt bên phải hiển thị chi tiết món ăn và thành phần nguyên liệu */
export function renderDishDetailDrawer(container, dish, onClose) {
    if (!container) return;
    if (!dish) {
        container.replaceChildren();
        return;
    }

    const price = Number(dish.price);
    const formattedPrice = Number.isFinite(price) ? `${new Intl.NumberFormat('vi-VN').format(price)}đ` : '';

    container.innerHTML = `
        <div class="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs transition-opacity" data-dish-detail-backdrop>
            <div class="absolute inset-y-0 right-0 max-w-full flex pl-10">
                <div class="w-screen max-w-md bg-surface-container-lowest shadow-xl flex flex-col">
                    <!-- Header Drawer -->
                    <div class="flex items-center justify-between px-6 py-4 border-b border-surface-variant">
                        <h2 class="font-headline-md text-lg font-bold text-on-surface">Thông tin chi tiết món ăn</h2>
                        <button type="button" class="text-on-surface-variant hover:text-on-surface p-1 rounded-lg" data-dish-detail-close>
                            <span class="material-symbols-outlined">close</span>
                        </button>
                    </div>

                    <!-- Body Drawer -->
                    <div class="flex-1 overflow-y-auto p-6 space-y-6">
                        <!-- Ảnh món -->
                        <div class="h-56 w-full rounded-xl overflow-hidden bg-surface-container-low flex items-center justify-center">
                            ${dish.imageUrl ? `<img src="${dish.imageUrl}" alt="${dish.name}" class="w-full h-full object-cover">` : '<span class="material-symbols-outlined text-6xl text-on-surface-variant">restaurant_menu</span>'}
                        </div>

                        <!-- Tên & Giá -->
                        <div>
                            <span class="text-xs font-semibold text-primary uppercase tracking-wider">${dish.categoryName || 'Món ăn'}</span>
                            <h3 class="text-xl font-bold text-on-surface mt-1">${dish.name}</h3>
                            <p class="text-lg font-bold text-primary mt-2">${formattedPrice}</p>
                        </div>

                        <!-- Mô tả -->
                        <div>
                            <h4 class="text-sm font-semibold text-on-surface mb-2">Mô tả hương vị</h4>
                            <p class="text-sm text-on-surface-variant leading-relaxed">${dish.description || 'Chưa có mô tả chi tiết cho món ăn này.'}</p>
                        </div>

                        <!-- Thành phần nguyên liệu (Mô phỏng chuẩn nhà hàng lớn) -->
                        <div class="border-t border-surface-variant pt-4">
                            <h4 class="text-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                                <span class="material-symbols-outlined text-primary text-base">nutrition</span> Thành phần nguyên liệu chính
                            </h4>
                            <ul class="space-y-2 text-sm text-on-surface-variant">
                                <li class="flex items-center gap-2"><span class="w-1.5 h-1.5 rounded-full bg-primary"></span> Nguyên liệu tươi sạch tiêu chuẩn nhà hàng</li>
                                <li class="flex items-center gap-2"><span class="w-1.5 h-1.5 rounded-full bg-primary"></span> Gia vị tự nhiên, không chất bảo quản</li>
                                <li class="flex items-center gap-2"><span class="w-1.5 h-1.5 rounded-full bg-primary"></span> Chế biến trực tiếp sau khi khách gọi món</li>
                            </ul>
                        </div>
                    </div>

                    <!-- Footer Drawer -->
                    <div class="p-6 border-t border-surface-variant bg-surface-container-low">
                        <button type="button" class="w-full h-12 rounded-lg bg-primary text-on-primary font-semibold text-sm flex items-center justify-center gap-2 hover:opacity-90 transition-opacity" data-dish-detail-add>
                            <span class="material-symbols-outlined text-lg">shopping_bag</span> Thêm vào giỏ hàng
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Gắn sự kiện đóng/mở và thêm giỏ hàng từ Drawer
    container.querySelector('[data-dish-detail-backdrop]').addEventListener('click', (e) => {
        if (e.target.hasAttribute('data-dish-detail-backdrop') || e.target.closest('[data-dish-detail-close]')) {
            onClose();
        }
    });

    container.querySelector('[data-dish-detail-add]').addEventListener('click', () => {
        if (dish.isAvailable !== false) {
            onClose();
        }
    });
}