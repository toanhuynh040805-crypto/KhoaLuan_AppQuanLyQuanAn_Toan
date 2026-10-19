export function createElement(tagName, className = '', text) {
    const element = document.createElement(tagName);

    if (className) {
        element.className = className;
    }

    if (text !== undefined) {
        element.textContent = text;
    }

    return element;
}

export function formatPrice(value) {
    const amount = Number(value);

    if (!Number.isFinite(amount)) {
        return '0đ';
    }

    return `${new Intl.NumberFormat('vi-VN').format(amount)}đ`;
}

function normalizeQuantity(value) {
    const quantity = Number(value);

    if (!Number.isFinite(quantity)) {
        return 1;
    }

    return Math.min(99, Math.max(1, Math.floor(quantity)));
}

function getItemPrice(item) {
    const price = Number(item?.price);

    return Number.isFinite(price) && price >= 0 ? price : 0;
}

function getItemName(item) {
    return item?.name || item?.menuItemName || 'Món ăn';
}

function getItemImage(item) {
    return item?.imageUrl || item?.image || item?.thumbnail || '';
}

/**
 * Render giỏ hàng checkout using server-verified product prices.
 */
export function renderCheckoutCart(
    container,
    items = [],
    onQuantityChange,
    onNoteChange
) {
    if (!container) {
        return;
    }

    if (!Array.isArray(items) || items.length === 0) {
        renderCheckoutMessage(
            container,
            'Giỏ hàng đang trống. Hãy quay lại thực đơn để chọn món.'
        );
        return;
    }

    const wrapper = createElement('div', 'space-y-4');
    let subtotal = 0;
    let hasCatalogErrors = false;

    items.forEach((item, index) => {
        const quantity = normalizeQuantity(item.quantity);
        const price = getItemPrice(item);
        const lineTotal = price * quantity;

        const catalogError = typeof item.catalogError === 'string' ? item.catalogError : '';
        hasCatalogErrors ||= Boolean(catalogError);
        if (!catalogError) subtotal += lineTotal;

        const row = createElement(
            'article',
            'rounded-xl border border-outline-variant/60 bg-surface-container-lowest p-4 transition-all hover:shadow-sm'
        );

        const top = createElement('div', 'flex gap-3');

        /* Ảnh món */
        const imageWrapper = createElement(
            'div',
            'h-16 w-16 shrink-0 overflow-hidden rounded-lg bg-surface-container'
        );

        const imageUrl = getItemImage(item);

        if (imageUrl) {
            const image = document.createElement('img');
            image.src = imageUrl;
            image.alt = getItemName(item);
            image.loading = 'lazy';
            image.className = 'h-full w-full object-cover';

            image.onerror = () => {
                image.remove();
                const fallback = createElement(
                    'span',
                    'material-symbols-outlined flex h-full w-full items-center justify-center text-2xl text-on-surface-variant',
                    'restaurant'
                );
                imageWrapper.append(fallback);
            };

            imageWrapper.append(image);
        } else {
            imageWrapper.append(
                createElement(
                    'span',
                    'material-symbols-outlined flex h-full w-full items-center justify-center text-2xl text-on-surface-variant',
                    'restaurant'
                )
            );
        }

        /* Thông tin món */
        const information = createElement('div', 'min-w-0 flex-1');
        const title = createElement('h3', 'truncate text-sm font-bold text-on-surface', getItemName(item));
        const unitPrice = createElement(
            'p',
            'mt-1 text-xs text-on-surface-variant',
            catalogError ? 'Chưa xác minh được đơn giá' : `${formatPrice(price)} / phần`
        );
        
        information.append(title, unitPrice);
        if (catalogError) {
            information.append(createElement('p', 'mt-1 text-xs font-semibold text-error', catalogError));
        }
        top.append(imageWrapper, information);

        /* Giá dòng */
        const linePrice = createElement(
            'p',
            'shrink-0 text-sm font-bold text-primary',
            catalogError ? '—' : formatPrice(lineTotal)
        );
        top.append(linePrice);

        /* Controls: Số lượng & Ghi chú */
        const bottom = createElement(
            'div',
            'mt-4 flex flex-col gap-3 border-t border-outline-variant/40 pt-3 sm:flex-row sm:items-center sm:justify-between'
        );

        const quantityGroup = createElement('div', 'flex items-center gap-2');
        const quantityLabel = createElement('span', 'mr-2 text-xs font-medium text-on-surface-variant', 'Số lượng');

        // NÚT TRỪ (-)
        const decreaseButton = createElement(
            'button',
            'flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant bg-surface text-on-surface transition hover:bg-error-container hover:text-error hover:border-error disabled:cursor-not-allowed disabled:opacity-40',
            '−'
        );
        decreaseButton.type = 'button';
        decreaseButton.setAttribute('aria-label', `Giảm số lượng ${getItemName(item)}`);

        decreaseButton.addEventListener('click', () => {
            if (typeof onQuantityChange !== 'function') return;

            // Xác nhận xóa nếu số lượng giảm về 1
            if (quantity === 1) {
                if (confirm(`Bạn có chắc chắn muốn xóa "${getItemName(item)}" khỏi đơn hàng?`)) {
                    onQuantityChange(index, 0);
                }
            } else {
                onQuantityChange(index, quantity - 1);
            }
        });

        const quantityValue = createElement(
            'span',
            'flex h-9 min-w-10 items-center justify-center rounded-lg bg-surface-container px-2 text-sm font-bold text-on-surface',
            String(quantity)
        );
        quantityValue.setAttribute('aria-label', `Số lượng ${getItemName(item)}`);

        // NÚT CỘNG (+)
        const increaseButton = createElement(
            'button',
            'flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant bg-surface text-on-surface transition hover:bg-surface-container',
            '+'
        );
        increaseButton.type = 'button';
        increaseButton.setAttribute('aria-label', `Tăng số lượng ${getItemName(item)}`);
        increaseButton.disabled = quantity >= 99;

        increaseButton.addEventListener('click', () => {
            if (typeof onQuantityChange !== 'function') return;
            onQuantityChange(index, quantity + 1);
        });

        quantityGroup.append(quantityLabel, decreaseButton, quantityValue, increaseButton);

        /* Ô nhập Ghi chú món */
        const noteWrapper = createElement('div', 'flex-1 sm:max-w-md');
        const note = document.createElement('input');
        note.type = 'text';
        note.maxLength = 255;
        note.value = item.note ?? '';
        note.placeholder = 'Ghi chú cho món, ví dụ: ít cay, không hành...';
        note.className = 'h-10 w-full rounded-lg border border-outline-variant bg-surface px-3 text-xs text-on-surface outline-none transition placeholder:text-on-surface-variant/70 focus:border-primary focus:ring-2 focus:ring-primary/10';
        note.setAttribute('aria-label', `Ghi chú ${getItemName(item)}`);

        note.addEventListener('input', () => {
            if (typeof onNoteChange === 'function') {
                onNoteChange(index, note.value.slice(0, 255));
            }
        });

        noteWrapper.append(note);
        bottom.append(quantityGroup, noteWrapper);
        row.append(top, bottom);
        wrapper.append(row);
    });

    const summary = createElement('section', 'mt-6 border-t border-outline-variant/60 pt-5');
    const subtotalRow = createElement('div', 'flex justify-between text-sm');
    subtotalRow.append(
        createElement('span', 'text-on-surface-variant', hasCatalogErrors ? 'Tạm tính món đã xác minh' : 'Tạm tính'),
        createElement('span', 'font-medium text-on-surface', hasCatalogErrors ? 'Chưa thể xác định' : formatPrice(subtotal))
    );
    summary.append(subtotalRow);

    const totalRow = createElement('div', 'mt-4 flex items-end justify-between gap-4');
    totalRow.append(createElement('div', '', ''));
    const totalLabel = totalRow.firstElementChild;
    totalLabel.append(
        createElement('p', 'text-sm font-bold text-on-surface', 'Tổng tiền dự kiến'),
        createElement('p', 'mt-1 text-xs text-on-surface-variant', `${items.length} loại món`)
    );
    totalRow.append(createElement(
        'p',
        'text-2xl font-extrabold text-primary',
        hasCatalogErrors ? '—' : formatPrice(subtotal)
    ));
    summary.append(totalRow);

    wrapper.append(summary);
    container.replaceChildren(wrapper);
}

/** Render danh sách bàn trực quan, lọc trạng thái khả dụng */
export function renderCheckoutTables(select, tables = [], { availableStatusValues = ['Trong', 0, '0'] } = {}) {
    if (!select) return;
    const normalizedTables = Array.isArray(tables) ? tables : [];
    const options = [new Option('Chọn bàn dùng món', '')];

    normalizedTables.forEach((table) => {
        const id = table?.id ?? table?.TableId ?? table?.tableId;
        if (id === undefined || id === null) return;
        const tableName = table?.tableName ?? table?.name ?? table?.TableName ?? `Bàn ${id}`;
        const areaName = table?.areaName ?? table?.AreaName ?? '';
        const capacity = Number(table?.capacity ?? table?.Capacity);
        
        const rawStatus = table?.status ?? table?.Status;
        
        const isAvailable = availableStatusValues.includes(rawStatus) || 
                            availableStatusValues.includes(Number(rawStatus)) ||
                            String(rawStatus).toLowerCase() === 'trong';
        
        const capacityText = Number.isFinite(capacity) && capacity > 0 ? ` · ${capacity} chỗ` : '';
        const areaText = areaName ? ` · ${areaName}` : '';
        const statusText = isAvailable ? 'Có thể chọn' : 'Đang bận / Không khả dụng';

        const option = new Option(`${tableName}${areaText}${capacityText} · ${statusText}`, String(id));
        
        option.disabled = !isAvailable;
        options.push(option);
    });

    select.replaceChildren(...options);
    select.disabled = normalizedTables.length === 0;
}

/** Render thông báo trạng thái hoặc lỗi dùng chung cho checkout */
export function renderCheckoutMessage(container, message, { loginHref, type = 'info' } = {}) {
    if (!container) return;
    const typeConfig = {
        info: { wrapper: 'rounded-xl border border-outline-variant/60 bg-surface-container-low p-4 text-sm text-on-surface-variant', icon: 'info', iconClass: 'text-secondary' },
        success: { wrapper: 'rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800', icon: 'check_circle', iconClass: 'text-green-600' },
        error: { wrapper: 'rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800', icon: 'error', iconClass: 'text-red-600' },
        warning: { wrapper: 'rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800', icon: 'warning', iconClass: 'text-amber-600' }
    };
    const config = typeConfig[type] ?? typeConfig.info;
    const content = createElement('div', `${config.wrapper} flex items-start gap-3`);
    content.append(createElement('span', `material-symbols-outlined shrink-0 ${config.iconClass}`, config.icon));
    const body = createElement('div', 'min-w-0 flex-1');
    body.append(createElement('p', 'leading-6', message));

    if (loginHref) {
        const link = createElement('a', 'mt-2 inline-flex font-bold text-primary underline underline-offset-2', 'Đăng nhập lại');
        link.href = loginHref;
        body.append(link);
    }
    content.append(body);
    container.replaceChildren(content);
}

export function renderCheckoutRecovery(container, message, actions = []) {
    if (!container) return;

    const content = createElement('div', 'rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950');
    content.append(createElement('p', 'leading-6', message));

    if (actions.length > 0) {
        const actionGroup = createElement('div', 'mt-3 flex flex-wrap gap-2');
        actions.forEach(({ label, onClick, disabled = false }) => {
            const button = createElement(
                'button',
                'min-h-10 rounded-lg border border-amber-800/30 bg-white px-3 py-2 font-semibold text-amber-950 hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50',
                label
            );
            button.type = 'button';
            button.disabled = disabled;
            button.addEventListener('click', onClick);
            actionGroup.append(button);
        });
        content.append(actionGroup);
    }

    container.replaceChildren(content);
}