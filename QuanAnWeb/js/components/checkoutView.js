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

/** Render thông báo cảnh báo riêng cho khu vực chọn bàn */
export function renderCheckoutTableAlert(container, message, type = 'warning') {
    if (!container) return;
    if (!message) {
        container.classList.add('hidden');
        container.replaceChildren();
        return;
    }
    const bgClass = type === 'error' ? 'bg-red-50 border-red-200 text-red-900' : 'bg-amber-50 border-amber-200 text-amber-900';
    const iconClass = type === 'error' ? 'text-red-600' : 'text-amber-600';
    const iconName = type === 'error' ? 'error' : 'warning';

    const box = createElement('div', `rounded-xl border ${bgClass} p-3.5 text-xs md:text-sm flex items-start justify-between gap-2.5 shadow-xs`);
    const content = createElement('div', 'flex items-start gap-2 min-w-0');
    content.append(createElement('span', `material-symbols-outlined text-[18px] shrink-0 mt-0.5 ${iconClass}`, iconName));
    content.append(createElement('p', 'leading-snug', message));
    
    const closeBtn = createElement('button', 'shrink-0 text-current opacity-70 hover:opacity-100 transition');
    closeBtn.type = 'button';
    closeBtn.innerHTML = '<span class="material-symbols-outlined text-[16px]">close</span>';
    closeBtn.addEventListener('click', () => {
        container.classList.add('hidden');
        container.replaceChildren();
    });

    box.append(content, closeBtn);
    container.replaceChildren(box);
    container.classList.remove('hidden');
}

/** Render danh sách bàn trực quan theo lưới thẻ cân đối, CHỈ hiển thị bàn trống */
export function renderCheckoutTables(select, tables = [], options = {}) {
    const {
        gridContainer = document.getElementById('checkout-table-selector'),
        countBadge = document.getElementById('checkout-available-tables-count'),
        selectedTableId = '',
        activeAreaFilter = 'all',
        onSelectTable = () => {},
        onSelectArea = () => {}
    } = options;

    const normalizedTables = Array.isArray(tables) ? tables : [];
    
    // LỌC NGHIÊM NGẶT: Chỉ giữ lại các bàn thực sự đang TRỐNG (Status = 'Trong' hoặc 0)
    const availableTables = normalizedTables.filter((table) => {
        const rawStatus = table?.status ?? table?.Status;
        return rawStatus === 0 || rawStatus === '0' || String(rawStatus).toLowerCase() === 'trong';
    });

    // 1. Đồng bộ phần tử <select> ẩn để duy trì tính hợp lệ của Form và tính tương thích
    if (select) {
        const selectOptions = [new Option('Chọn bàn dùng món', '')];
        if (availableTables.length === 0) {
            const noTableOpt = new Option('Hiện không có bàn trống', '');
            noTableOpt.disabled = true;
            selectOptions.push(noTableOpt);
        } else {
            availableTables.forEach((table) => {
                const id = table?.id ?? table?.TableId ?? table?.tableId;
                if (id === undefined || id === null) return;
                const tableName = table?.tableName ?? table?.name ?? table?.TableName ?? `Bàn ${id}`;
                const areaName = table?.areaName ?? table?.AreaName ?? '';
                const capacity = Number(table?.capacity ?? table?.Capacity);
                const capacityText = Number.isFinite(capacity) && capacity > 0 ? ` · ${capacity} chỗ` : '';
                const areaText = areaName ? ` · ${areaName}` : '';
                selectOptions.push(new Option(`${tableName}${areaText}${capacityText}`, String(id)));
            });
        }
        select.replaceChildren(...selectOptions);
        select.disabled = availableTables.length === 0;
        if (selectedTableId && availableTables.some(t => String(t.id ?? t.TableId) === String(selectedTableId))) {
            select.value = String(selectedTableId);
        } else {
            select.value = '';
        }
    }

    // 2. Cập nhật Badge số lượng bàn trống
    if (countBadge) {
        if (availableTables.length > 0) {
            countBadge.textContent = `${availableTables.length} bàn trống`;
            countBadge.className = 'text-xs font-semibold text-[#00685c] bg-[#00685c]/10 px-2.5 py-1 rounded-full';
            countBadge.classList.remove('hidden');
        } else {
            countBadge.textContent = 'Hết bàn trống';
            countBadge.className = 'text-xs font-semibold text-error bg-red-50 text-red-700 px-2.5 py-1 rounded-full';
            countBadge.classList.remove('hidden');
        }
    }

    // 3. Render lưới bàn trực quan vào gridContainer
    if (!gridContainer) return;

    // Trường hợp KHÔNG CÓ BÀN TRỐNG: Hiển thị Empty State thân thiện
    if (availableTables.length === 0) {
        const emptyBox = createElement('div', 'rounded-2xl border border-amber-200 bg-amber-50/70 p-6 text-center');
        const icon = createElement('span', 'material-symbols-outlined mx-auto text-3xl text-amber-600 block', 'table_restaurant');
        const title = createElement('p', 'mt-2 text-sm font-bold text-amber-900', 'Hiện không có bàn trống. Vui lòng thử lại sau.');
        const subtext = createElement('p', 'mt-1 text-xs text-amber-700', 'Tất cả các bàn hiện đang phục vụ hoặc đang dọn dẹp. Danh sách sẽ tự động hiển thị ngay khi có bàn trống.');
        emptyBox.append(icon, title, subtext);
        gridContainer.replaceChildren(emptyBox);
        return;
    }

    const wrapper = createElement('div', 'space-y-3.5 w-full min-w-0');

    // Thu thập danh sách các khu vực duy nhất có bàn trống
    const areas = [];
    const areaCounts = new Map();
    availableTables.forEach(t => {
        const area = t.areaName || 'Khu vực chính';
        areaCounts.set(area, (areaCounts.get(area) || 0) + 1);
        if (!areas.includes(area)) areas.push(area);
    });

    // Nếu có từ 2 khu vực trở lên, tạo thanh lọc khu vực (Pills)
    if (areas.length > 1) {
        const filterBar = createElement('div', 'flex items-center gap-1.5 overflow-x-auto pb-1.5 scrollbar-none text-xs w-full min-w-0');
        
        const allBtn = createElement(
            'button',
            `px-3 py-1.5 rounded-full font-semibold transition shrink-0 ${activeAreaFilter === 'all' ? 'bg-[#ac2d00] text-white shadow-sm' : 'bg-[#f3f3f3] text-[#5b4139] hover:bg-[#e8e8e8]'}`,
            `Tất cả (${availableTables.length})`
        );
        allBtn.type = 'button';
        allBtn.addEventListener('click', () => onSelectArea('all'));
        filterBar.append(allBtn);

        areas.forEach(area => {
            const count = areaCounts.get(area) || 0;
            const isAreaActive = activeAreaFilter === area;
            const btn = createElement(
                'button',
                `px-3 py-1.5 rounded-full font-medium transition shrink-0 ${isAreaActive ? 'bg-[#ac2d00] text-white font-semibold shadow-sm' : 'bg-[#f3f3f3] text-[#5b4139] hover:bg-[#e8e8e8]'}`,
                `${area} (${count})`
            );
            btn.type = 'button';
            btn.addEventListener('click', () => onSelectArea(area));
            filterBar.append(btn);
        });

        wrapper.append(filterBar);
    }

    // Lọc bàn hiển thị theo khu vực đã chọn
    const tablesToShow = (activeAreaFilter && activeAreaFilter !== 'all')
        ? availableTables.filter(t => (t.areaName || 'Khu vực chính') === activeAreaFilter)
        : availableTables;

    // Lưới thẻ bàn responsive: 2 cột trên điện thoại, 3 cột trên tablet, 4 cột trên desktop
    const grid = createElement('div', 'grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 xl:grid-cols-4 gap-2.5 w-full min-w-0');

    tablesToShow.forEach((table) => {
        const id = table?.id ?? table?.TableId ?? table?.tableId;
        const tableName = table?.tableName ?? table?.name ?? table?.TableName ?? `Bàn ${id}`;
        const areaName = table?.areaName ?? table?.AreaName ?? 'Khu vực chung';
        const capacity = Number(table?.capacity ?? table?.Capacity ?? 4);
        const isSelected = String(selectedTableId) === String(id);

        const card = createElement(
            'button',
            `group relative flex flex-col justify-between p-3 rounded-xl border text-left transition-all cursor-pointer focus:outline-none min-w-0 min-h-[92px] ${
                isSelected
                    ? 'border-2 border-[#ac2d00] bg-[#fff6f4] shadow-sm ring-2 ring-[#ac2d00]/20'
                    : 'border-[#d8c8c2] bg-white hover:border-[#ac2d00] hover:bg-[#fffcfb] hover:shadow-xs'
            }`
        );
        card.type = 'button';
        card.dataset.tableCardId = String(id);
        card.setAttribute('aria-pressed', isSelected ? 'true' : 'false');

        // Hàng trên: Tên bàn + Sức chứa / Dấu tick
        const topRow = createElement('div', 'flex items-center justify-between w-full min-w-0 gap-1');
        const nameEl = createElement(
            'span',
            `font-extrabold text-sm tracking-tight truncate ${isSelected ? 'text-[#ac2d00]' : 'text-[#1a1c1c]'}`,
            tableName
        );
        topRow.append(nameEl);

        if (isSelected) {
            const checkIcon = createElement('span', 'material-symbols-outlined text-[18px] text-[#ac2d00] shrink-0', 'check_circle');
            topRow.append(checkIcon);
        } else {
            const capBadge = createElement('span', 'inline-flex items-center gap-0.5 text-[11px] font-semibold text-[#00685c] bg-[#00685c]/10 px-1.5 py-0.5 rounded shrink-0');
            capBadge.innerHTML = `<span class="material-symbols-outlined text-[13px]">person</span>${capacity}`;
            topRow.append(capBadge);
        }

        // Hàng giữa: Khu vực / Tầng
        const areaEl = createElement('p', 'text-xs text-[#5b4139] truncate font-medium mt-1', areaName);

        // Hàng dưới: Trạng thái trống / Đang chọn
        const bottomRow = createElement('div', 'mt-2 flex items-center justify-between text-[11px] pt-1.5 border-t border-[#f2eae6] w-full min-w-0 gap-1');
        const statusBadge = createElement('span', 'inline-flex items-center gap-1 font-semibold text-emerald-700 shrink-0');
        statusBadge.innerHTML = `<span class="inline-block h-1.5 w-1.5 rounded-full bg-emerald-500"></span>Trống`;
        bottomRow.append(statusBadge);

        if (isSelected) {
            const selectedText = createElement('span', 'font-bold text-[#ac2d00] shrink-0', 'Đang chọn');
            bottomRow.append(selectedText);
        } else {
            const seatsText = createElement('span', 'text-[11px] text-[#5b4139] shrink-0', `${capacity} chỗ`);
            bottomRow.append(seatsText);
        }

        card.append(topRow, areaEl, bottomRow);

        card.addEventListener('click', () => {
            onSelectTable(id);
        });

        grid.append(card);
    });

    wrapper.append(grid);
    gridContainer.replaceChildren(wrapper);
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