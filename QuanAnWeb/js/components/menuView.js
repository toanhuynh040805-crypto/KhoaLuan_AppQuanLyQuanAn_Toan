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
        
        // Vùng ảnh: Khi click hoặc bấm phím Enter/Space sẽ mở modal chi tiết món ăn
        const imageArea = createElement('div', `h-48 shrink-0 w-full relative overflow-hidden bg-surface-container-low flex items-center justify-center cursor-pointer group select-none ${available ? '' : ' grayscale'}`);
        imageArea.setAttribute('role', 'button');
        imageArea.setAttribute('tabindex', '0');
        imageArea.setAttribute('aria-label', `Xem chi tiết món ${dish.name ?? ''}`);
        imageArea.title = `Nhấn vào ảnh để xem chi tiết ${dish.name ?? 'món ăn'}`;

        const imageUrl = dish.imageUrl;
        if (typeof imageUrl === 'string' && imageUrl.trim()) {
            try {
                const resolvedUrl = new URL(imageUrl, location.href);
                if (resolvedUrl.protocol === 'https:' || resolvedUrl.protocol === 'http:') {
                    const image = createElement('img', 'h-full w-full object-cover transition-transform duration-300 ease-out group-hover:scale-105');
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

        // Lớp phủ hover tinh tế báo hiệu ảnh có thể nhấn xem chi tiết
        const hoverHint = createElement('div', 'absolute inset-0 bg-black/15 opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100 transition-opacity duration-200 flex items-center justify-center pointer-events-none');
        hoverHint.innerHTML = '<span class="material-symbols-outlined text-white text-3xl drop-shadow-md transform scale-90 group-hover:scale-100 transition-transform">zoom_in</span>';
        imageArea.append(hoverHint);

        if (dish.categoryName) {
            const categoryBadge = createElement('span', 'absolute top-3 left-3 bg-primary-container bg-opacity-90 text-on-primary-container px-2 py-1 rounded font-label-md text-label-md pointer-events-none z-10', dish.categoryName);
            imageArea.append(categoryBadge);
        }

        // Sự kiện click / keydown vào ảnh để xem chi tiết món ăn
        imageArea.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            if (typeof onShowDetail === 'function') onShowDetail(dish);
        });
        imageArea.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                e.stopPropagation();
                if (typeof onShowDetail === 'function') onShowDetail(dish);
            }
        });

        if (!available) {
            const soldOut = createElement('div', 'absolute inset-0 z-20 flex items-center justify-center pointer-events-none');
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
        addButton.addEventListener('click', (e) => {
            e.stopPropagation();
            onAddToCart(dish);
        });
        content.append(addButton);
        
        card.append(imageArea, content);
        return card;
    });

    container.replaceChildren(...cards);
}

/** Hiển thị thông báo Toast góc màn hình */
export function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center gap-3 px-4 py-3 rounded-2xl shadow-xl border text-sm font-medium transition-all duration-300 transform translate-y-2 opacity-0 ${
        type === 'success'
            ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
            : type === 'error'
            ? 'bg-rose-50 text-rose-800 border-rose-200'
            : 'bg-surface-container-lowest text-on-surface border-surface-variant'
    }`;

    const iconName = type === 'success' ? 'check_circle' : type === 'error' ? 'error' : 'info';
    const iconColor = type === 'success' ? 'text-emerald-600' : type === 'error' ? 'text-rose-600' : 'text-primary';

    toast.innerHTML = `
        <span class="material-symbols-outlined ${iconColor} text-xl">${iconName}</span>
        <span class="flex-1">${message}</span>
    `;

    container.appendChild(toast);

    requestAnimationFrame(() => {
        toast.classList.remove('translate-y-2', 'opacity-0');
    });

    setTimeout(() => {
        toast.classList.add('opacity-0', 'translate-y-2');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

/** Render Modal xem chi tiết món ăn (chuẩn giao diện GustoPOS) */
export function renderDishDetailModal(container, dish, callbacks = {}) {
    if (!container) return;
    if (!dish) {
        container.replaceChildren();
        return;
    }

    const { onClose, onAddToCart } = callbacks;
    const price = Number(dish.price);
    const formattedPrice = Number.isFinite(price) ? `${new Intl.NumberFormat('vi-VN').format(price)}đ` : '';
    const isAvailable = dish.isAvailable !== false;

    container.setAttribute('role', 'dialog');
    container.setAttribute('aria-modal', 'true');
    container.setAttribute('aria-labelledby', 'dish-detail-title');
    container.setAttribute('aria-describedby', 'dish-detail-desc');

    const statusBadge = isAvailable
        ? `<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-600/90 text-white shadow-sm backdrop-blur-xs">
             <span class="w-1.5 h-1.5 rounded-full bg-white"></span> Đang phục vụ
           </span>`
        : `<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-red-600/90 text-white shadow-sm backdrop-blur-xs">
             <span class="w-1.5 h-1.5 rounded-full bg-white"></span> Tạm hết món
           </span>`;

    const categoryBadge = dish.categoryName
        ? `<span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-primary/90 text-white shadow-sm backdrop-blur-xs">
             ${dish.categoryName}
           </span>`
        : '';

    container.innerHTML = `
        <div class="fixed inset-0 z-[60] bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 sm:p-6 overflow-y-auto" data-dish-detail-backdrop>
            <div class="relative w-full max-w-lg bg-surface-container-lowest rounded-2xl shadow-2xl border border-surface-variant overflow-hidden flex flex-col my-auto max-h-[90vh] transition-all" data-dish-detail-card role="document">
                
                <!-- Nút đóng nổi góc trên bên phải -->
                <button type="button" class="absolute top-3 right-3 z-20 w-9 h-9 rounded-full bg-black/50 hover:bg-black/70 text-white flex items-center justify-center shadow-md transition-colors focus:outline-none focus:ring-2 focus:ring-white" data-dish-detail-close aria-label="Đóng cửa sổ chi tiết">
                    <span class="material-symbols-outlined text-xl">close</span>
                </button>

                <!-- Ảnh lớn món ăn -->
                <div class="relative h-56 sm:h-64 w-full bg-surface-container-low shrink-0 overflow-hidden flex items-center justify-center">
                    ${dish.imageUrl
                        ? `<img src="${dish.imageUrl}" alt="${dish.name ?? ''}" class="w-full h-full object-cover" id="dish-detail-image">`
                        : `<span class="material-symbols-outlined text-6xl text-on-surface-variant" aria-hidden="true">restaurant_menu</span>`
                    }
                    <!-- Badges overlay góc trên ảnh -->
                    <div class="absolute top-3 left-3 z-10 flex flex-wrap items-center gap-2 pointer-events-none">
                        ${categoryBadge}
                        ${statusBadge}
                    </div>
                </div>

                <!-- Thân modal (cuộn nếu dài) -->
                <div class="flex-1 overflow-y-auto p-5 sm:p-6 space-y-5">
                    <!-- Tên món & Giá bán -->
                    <div class="flex items-start justify-between gap-4 border-b border-surface-variant/60 pb-4">
                        <div>
                            <h3 id="dish-detail-title" class="text-xl sm:text-2xl font-bold text-on-surface leading-snug">
                                ${dish.name ?? ''}
                            </h3>
                            ${dish.categoryName ? `<p class="text-xs text-on-surface-variant font-medium mt-1">Danh mục: <span class="text-on-surface font-semibold">${dish.categoryName}</span></p>` : ''}
                        </div>
                        <div class="text-right shrink-0">
                            <span class="text-xl sm:text-2xl font-extrabold text-primary block">${formattedPrice}</span>
                            <span class="text-[11px] text-on-surface-variant">Đã gồm thuế VAT</span>
                        </div>
                    </div>

                    <!-- Mô tả hương vị -->
                    <div>
                        <h4 class="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-1.5 flex items-center gap-1.5">
                            <span class="material-symbols-outlined text-primary text-base">description</span> Mô tả món ăn
                        </h4>
                        <p id="dish-detail-desc" class="text-sm text-on-surface leading-relaxed whitespace-pre-line bg-surface-container-low/50 p-3.5 rounded-xl border border-surface-variant/40">
                            ${dish.description ? dish.description : '<span class="text-on-surface-variant italic">Chưa có mô tả chi tiết cho món ăn này.</span>'}
                        </p>
                    </div>

                    <!-- Thành phần nguyên liệu (Dữ liệu thật từ backend) -->
                    <div>
                        <h4 class="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2.5 flex items-center gap-1.5">
                            <span class="material-symbols-outlined text-primary text-base">nutrition</span> Thành phần & nguyên liệu
                        </h4>
                        <div data-dish-ingredients-root class="min-h-[40px] flex items-center">
                            <div class="flex items-center gap-2 text-xs text-on-surface-variant py-2">
                                <span class="material-symbols-outlined text-base animate-spin text-primary">progress_activity</span>
                                <span>Đang tải thông tin nguyên liệu từ nhà bếp...</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Footer cố định chứa nút hành động -->
                <div class="p-4 sm:p-5 border-t border-surface-variant bg-surface-container-low flex items-center justify-between gap-3 shrink-0">
                    <button type="button" class="px-4 py-2.5 rounded-xl border border-outline-variant text-sm font-semibold text-on-surface hover:bg-surface-container-high transition-colors" data-dish-detail-secondary-close>
                        Đóng
                    </button>
                    <button type="button" class="flex-1 sm:flex-initial sm:min-w-[200px] h-11 px-5 rounded-xl bg-primary text-on-primary text-sm font-semibold flex items-center justify-center gap-2 hover:opacity-95 shadow-md shadow-primary/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none" data-dish-detail-add ${isAvailable ? '' : 'disabled'}>
                        <span class="material-symbols-outlined text-lg">${isAvailable ? 'shopping_bag' : 'block'}</span>
                        <span>${isAvailable ? 'Thêm vào giỏ hàng' : 'Tạm hết món'}</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    // Fallback lỗi ảnh
    const modalImg = container.querySelector('#dish-detail-image');
    if (modalImg) {
        modalImg.addEventListener('error', () => {
            modalImg.replaceWith(createElement('span', 'material-symbols-outlined text-6xl text-on-surface-variant', 'restaurant_menu'));
        }, { once: true });
    }

    // Đóng khi click backdrop hoặc nút đóng
    const backdrop = container.querySelector('[data-dish-detail-backdrop]');
    backdrop?.addEventListener('click', (e) => {
        if (e.target === backdrop || e.target.closest('[data-dish-detail-close]') || e.target.closest('[data-dish-detail-secondary-close]')) {
            if (typeof onClose === 'function') onClose();
        }
    });

    // Thêm vào giỏ từ bên trong modal
    const addButton = container.querySelector('[data-dish-detail-add]');
    if (addButton && isAvailable) {
        let isSubmitting = false;
        addButton.addEventListener('click', (e) => {
            e.stopPropagation();
            if (isSubmitting) return;
            isSubmitting = true;
            addButton.disabled = true;

            try {
                if (typeof onAddToCart === 'function') {
                    onAddToCart(dish);
                }
            } finally {
                setTimeout(() => {
                    isSubmitting = false;
                    addButton.disabled = false;
                }, 400);
            }
        });
    }
}

/** Cập nhật danh sách nguyên liệu thật từ Backend vào Modal */
export function updateModalIngredients(container, ingredients) {
    if (!container) return;
    const ingredientsRoot = container.querySelector('[data-dish-ingredients-root]');
    if (!ingredientsRoot) return;

    if (Array.isArray(ingredients) && ingredients.length > 0) {
        const chips = ingredients.map((item) => {
            const chip = document.createElement('span');
            chip.className = 'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-surface-container-high text-on-surface text-xs font-medium border border-outline-variant/40 shadow-xs';
            const dot = document.createElement('span');
            dot.className = 'w-1.5 h-1.5 rounded-full bg-primary shrink-0';
            const text = document.createElement('span');
            text.textContent = item;
            chip.append(dot, text);
            return chip;
        });

        const listContainer = document.createElement('div');
        listContainer.className = 'flex flex-wrap gap-2 w-full';
        listContainer.append(...chips);
        ingredientsRoot.replaceChildren(listContainer);
    } else {
        const emptyMsg = document.createElement('p');
        emptyMsg.className = 'text-xs text-on-surface-variant italic py-1';
        emptyMsg.textContent = 'Món ăn theo công thức độc quyền chuẩn vị, chưa có thông tin bóc tách nguyên liệu phụ.';
        ingredientsRoot.replaceChildren(emptyMsg);
    }
}

/** Giữ lại alias để tương thích ngược */
export const renderDishDetailDrawer = renderDishDetailModal;
