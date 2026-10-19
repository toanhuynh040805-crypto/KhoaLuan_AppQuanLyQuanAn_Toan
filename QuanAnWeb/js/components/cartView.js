function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

function formatPrice(value) {
    return `${new Intl.NumberFormat('vi-VN').format(value)}đ`;
}

export function renderCartDrawer(container, cartItems, onUpdateQty, onRemove, onUpdateNote, onClose) {
    const layer = createElement('div', 'fixed inset-0 z-[70] hidden');
    layer.dataset.cartLayer = '';

    const backdrop = createElement('button', 'absolute inset-0 h-full w-full cursor-default bg-black/40');
    backdrop.type = 'button';
    backdrop.setAttribute('aria-label', 'Đóng giỏ hàng');
    backdrop.addEventListener('click', onClose);

    const drawer = createElement('section', 'absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-surface-container-lowest shadow-xl');
    drawer.setAttribute('role', 'dialog');
    drawer.setAttribute('aria-modal', 'true');
    drawer.setAttribute('aria-labelledby', 'cart-heading');

    const header = createElement('header', 'flex items-center justify-between border-b border-outline-variant px-5 py-4');
    const title = createElement('h2', 'font-headline-sm text-headline-sm font-bold text-on-surface', 'Giỏ hàng');
    title.id = 'cart-heading';
    const closeButton = createElement('button', 'flex h-10 w-10 items-center justify-center rounded-full text-on-surface-variant hover:bg-surface-container-high');
    closeButton.type = 'button';
    closeButton.dataset.cartClose = '';
    closeButton.setAttribute('aria-label', 'Đóng giỏ hàng');
    closeButton.append(createElement('span', 'material-symbols-outlined', 'close'));
    header.append(title, closeButton);

    const list = createElement('div', 'flex-1 overflow-y-auto px-5');
    if (cartItems.length === 0) {
        list.append(createElement('p', 'py-10 text-center text-body-md text-on-surface-variant', 'Giỏ hàng đang trống.'));
    } else {
        cartItems.forEach((item) => {
            const row = createElement('article', 'border-b border-outline-variant py-5');
            const details = createElement('div', 'flex items-start justify-between gap-3');
            const product = createElement('div', 'min-w-0');
            product.append(
                createElement('h3', 'break-words font-label-lg text-label-lg font-semibold text-on-surface', item.name),
                createElement('p', 'mt-1 text-body-sm text-on-surface-variant', formatPrice(item.price))
            );
            
            // Xử lý nút Xóa (Thùng rác) có thông báo
            const removeButton = createElement('button', 'flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-error hover:bg-error-container/40 transition-colors');
            removeButton.type = 'button';
            removeButton.setAttribute('aria-label', `Xóa ${item.name} khỏi giỏ hàng`);
            removeButton.append(createElement('span', 'material-symbols-outlined', 'delete'));
            removeButton.addEventListener('click', () => {
                if (confirm(`Bạn có chắc chắn muốn xóa "${item.name}" khỏi giỏ hàng không?`)) {
                    onRemove(item.id);
                }
            });
            details.append(product, removeButton);

            const controls = createElement('div', 'mt-4 flex items-center justify-between gap-4');
            const quantityGroup = createElement('div', 'flex items-center gap-3');
            
            // Xử lý nút Giảm số lượng (-) có thông báo nếu giảm về 0
            const decreaseButton = createElement('button', 'flex h-9 w-9 items-center justify-center rounded-full border border-outline-variant text-on-surface hover:bg-surface-container-high transition-colors', '−');
            decreaseButton.type = 'button';
            decreaseButton.setAttribute('aria-label', `Giảm số lượng ${item.name}`);
            decreaseButton.addEventListener('click', () => {
                if (item.quantity === 1) {
                    if (confirm(`Bạn có chắc chắn muốn xóa "${item.name}" khỏi giỏ hàng không?`)) {
                        onRemove(item.id);
                    }
                } else {
                    onUpdateQty(item.id, item.quantity - 1);
                }
            });
            
            const quantity = createElement('span', 'min-w-6 text-center font-label-md text-label-md font-semibold text-on-surface', String(item.quantity));
            
            // Nút Tăng số lượng (+)
            const increaseButton = createElement('button', 'flex h-9 w-9 items-center justify-center rounded-full border border-outline-variant text-on-surface hover:bg-surface-container-high transition-colors', '+');
            increaseButton.type = 'button';
            increaseButton.setAttribute('aria-label', `Tăng số lượng ${item.name}`);
            increaseButton.addEventListener('click', () => onUpdateQty(item.id, item.quantity + 1));
            
            quantityGroup.append(decreaseButton, quantity, increaseButton);
            controls.append(quantityGroup, createElement('span', 'font-label-lg text-label-lg font-semibold text-primary', formatPrice(item.price * item.quantity)));

            const note = createElement('textarea', 'mt-4 w-full resize-y rounded-lg border border-outline-variant bg-surface px-3 py-2 text-body-sm text-on-surface placeholder:text-on-surface-variant focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary');
            note.rows = 2;
            note.maxLength = 255;
            note.placeholder = 'Ghi chú cho món này (không bắt buộc)';
            note.value = item.note ?? '';
            note.setAttribute('aria-label', `Ghi chú cho ${item.name}`);
            note.addEventListener('input', () => onUpdateNote(item.id, note.value));

            row.append(details, controls, note);
            list.append(row);
        });
    }

    const total = cartItems.reduce((sum, item) => sum + (Number(item.price) || 0) * item.quantity, 0);
    const totalRow = createElement('div', 'flex items-center justify-between gap-4');
    totalRow.append(
        createElement('span', 'font-label-lg text-label-lg font-semibold text-on-surface', 'Tạm tính'),
        createElement('span', 'font-headline-sm text-headline-sm font-bold text-primary', formatPrice(total))
    );
    const footer = createElement('footer', 'border-t border-outline-variant px-5 py-5');
    
    // Đổi tên nút thành "Tiến hành thanh toán" cho chuyên nghiệp
    const checkoutLink = createElement('a', `mt-4 flex h-11 items-center justify-center rounded-lg bg-primary px-4 font-label-md text-on-primary transition-opacity hover:opacity-90 shadow-sm${cartItems.length ? '' : ' pointer-events-none opacity-50'}`, 'Tiến hành thanh toán');
    checkoutLink.href = '/pages/checkout/index.html';
    
    if (cartItems.length === 0) {
        checkoutLink.setAttribute('aria-disabled', 'true');
        checkoutLink.tabIndex = -1;
    }
    
    footer.append(
        totalRow,
        // Thay đổi câu lưu ý dưới Tạm tính
        createElement('p', 'mt-2 text-body-sm text-on-surface-variant', 'Tạm tính được xác nhận lại từ thực đơn khi checkout. Thanh toán được nhân viên ghi nhận tại quầy.'),
        checkoutLink
    );

    drawer.append(header, list, footer);
    layer.append(backdrop, drawer);
    container.replaceChildren(layer);
}