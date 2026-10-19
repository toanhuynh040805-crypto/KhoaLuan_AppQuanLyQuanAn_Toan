function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

function getValue(source, ...keys) {
    if (!source || typeof source !== 'object') return undefined;

    for (const key of keys) {
        if (source[key] !== undefined && source[key] !== null) return source[key];
    }

    return undefined;
}

const memberLabels = {
    ThanhVien: 'Thành viên',
    Bac: 'Bạc',
    Vang: 'Vàng',
    KimCuong: 'Kim cương'
};

function formatDate(value) {
    if (typeof value !== 'string' && typeof value !== 'number') return '—';
    if (!value) return '—';

    const date = new Date(value);
    return Number.isNaN(date.getTime())
        ? '—'
        : new Intl.DateTimeFormat('vi-VN', {
            dateStyle: 'medium',
            timeStyle: 'short'
        }).format(date);
}

function formatAmount(value) {
    if (value === null || value === undefined || value === '') return '—';
    const amount = Number(value);
    return Number.isFinite(amount)
        ? `${new Intl.NumberFormat('vi-VN').format(amount)}đ`
        : '—';
}

export function renderUserInfo(container, customer) {
    const nameValue = getValue(customer, 'FullName', 'fullName');
    const name = typeof nameValue === 'string' ? nameValue.trim() || '—' : '—';
    const phoneValue = getValue(customer, 'Phone', 'phone');
    const emailValue = getValue(customer, 'Email', 'email');
    const phone = typeof phoneValue === 'string' ? phoneValue : '—';
    const email = typeof emailValue === 'string' ? emailValue : '—';
    const points = getValue(customer, 'LoyaltyPoint', 'loyaltyPoint');
    const level = getValue(customer, 'MemberLevel', 'memberLevel');
    const levelText = memberLabels[level] ?? (level == null ? '—' : String(level));

    const heading = createElement(
        'h2',
        'font-headline-md text-headline-md font-bold text-on-surface',
        name
    );
    const contact = createElement(
        'p',
        'mt-1 text-body-md text-on-surface-variant',
        [phone, email].filter((value) => value !== '—').join(' · ') || 'Chưa có thông tin liên hệ'
    );
    const details = createElement('dl', 'mt-6 grid gap-4 sm:grid-cols-2');

    [
        ['Điểm tích lũy', points == null || !Number.isFinite(Number(points)) ? '—' : new Intl.NumberFormat('vi-VN').format(Number(points))],
        ['Hạng thành viên', levelText]
    ].forEach(([label, value]) => {
        const item = createElement('div', 'rounded-lg bg-surface-container-low p-4');
        item.append(
            createElement('dt', 'text-body-sm text-on-surface-variant', label),
            createElement('dd', 'mt-1 font-label-lg text-label-lg font-semibold text-on-surface', value)
        );
        details.append(item);
    });

    container.replaceChildren(heading, contact, details);
}

export function renderOrderHistory(container, orders) {
    const heading = createElement(
        'h2',
        'font-headline-sm text-headline-sm font-bold text-on-surface',
        'Đơn hàng gần đây'
    );

    if (!Array.isArray(orders) || orders.length === 0) {
        const empty = createElement(
            'p',
            'mt-4 rounded-lg border border-outline-variant bg-surface-container-low p-5 text-body-md text-on-surface-variant',
            'Chưa có đơn hàng nào.'
        );
        container.replaceChildren(heading, empty);
        return;
    }

    const list = createElement('ul', 'mt-4 divide-y divide-outline-variant');
    
    orders.forEach((order) => {
        const rawId = getValue(order, 'OrderId', 'orderId', 'Id', 'id');
        const id = typeof rawId === 'string' || typeof rawId === 'number' ? rawId : null;
        const time = getValue(order, 'OrderTime', 'orderTime');
        const amount = getValue(order, 'TotalAmount', 'totalAmount');
        const status = getValue(order, 'Status', 'status');
        const paymentStatus = getValue(order, 'PaymentStatus', 'paymentStatus');

        // SỬ DỤNG CSS GRID (grid-cols-12) THAY VÌ FLEXBOX CHO HÀNG
        const item = createElement('li', 'flex flex-col md:grid md:grid-cols-12 gap-4 py-4 items-center');

        // CỘT 1 (3/12): Mã đơn & Thời gian
        const summary = createElement('div', 'md:col-span-3 w-full text-left');
        summary.append(
            createElement('h3', 'font-label-lg text-label-lg font-semibold text-on-surface', id == null ? 'Đơn hàng' : `Đơn hàng #${id}`),
            createElement('p', 'mt-1 text-body-sm text-on-surface-variant', formatDate(time))
        );

        // CỘT 2 (4/12): Giá tiền, trạng thái đơn và thanh toán
        const values = createElement('div', 'md:col-span-4 w-full flex flex-wrap items-center justify-start md:justify-center gap-2');
        
        const statusLabels = { DangMo: 'Đang mở', DaThanhToan: 'Đã thanh toán', DaHuy: 'Đã hủy', 0: 'Đang mở', 1: 'Đã thanh toán', 2: 'Đã hủy' };
        const statusColors = { 
            DangMo: 'bg-amber-100 text-amber-800 border border-amber-200', 
            DaThanhToan: 'bg-green-100 text-green-800 border border-green-200', 
            DaHuy: 'bg-red-100 text-red-800 border border-red-200', 
            0: 'bg-amber-100 text-amber-800 border border-amber-200', 
            1: 'bg-green-100 text-green-800 border border-green-200', 
            2: 'bg-red-100 text-red-800 border border-red-200' 
        };

        const labelText = typeof status === 'string' || typeof status === 'number' ? (statusLabels[status] ?? String(status)) : '—';
        const colorClass = statusColors[status] || 'bg-secondary-container text-on-surface';
        const paymentLabels = {
            ThanhCong: 'Thanh toán: Đã ghi nhận',
            ThatBai: 'Thanh toán: Thất bại',
            HoanTien: 'Thanh toán: Đã hoàn tiền'
        };
        const paymentLabel = paymentLabels[paymentStatus] ?? 'Thanh toán: Chưa ghi nhận';
        const paymentColor = paymentStatus === 'ThanhCong'
            ? 'bg-green-100 text-green-800 border border-green-200'
            : paymentStatus === 'ThatBai'
                ? 'bg-red-100 text-red-800 border border-red-200'
                : 'bg-surface-container-low text-on-surface-variant border border-outline-variant';
        
        values.append(
            createElement('span', 'font-label-lg text-label-lg font-semibold text-primary whitespace-nowrap', formatAmount(amount)),
            createElement('span', `rounded-full px-3 py-1 text-body-sm whitespace-nowrap ${colorClass}`, `Đơn: ${labelText}`),
            createElement('span', `rounded-full px-3 py-1 text-body-sm whitespace-nowrap ${paymentColor}`, paymentLabel)
        );

        // CỘT 3 (5/12): Danh sách món ăn
        const itemSummary = createElement('div', 'md:col-span-5 w-full text-left md:text-right text-body-sm text-on-surface-variant truncate');
        
        const orderItems = getValue(order, 'OrderItems', 'orderItems', 'Items', 'items');
        if (Array.isArray(orderItems) && orderItems.length > 0) {
            const itemsText = orderItems.map((orderItem) => {
                const menuItem = getValue(orderItem, 'MenuItem', 'menuItem');
                const itemName = getValue(menuItem, 'Name', 'name', 'ItemName', 'itemName') ?? 'Món ăn';
                const quantity = Number(getValue(orderItem, 'Quantity', 'quantity'));
                return `${itemName} × ${Number.isFinite(quantity) && quantity > 0 ? quantity : 1}`;
            }).join(', ');
            
            itemSummary.textContent = itemsText;
            itemSummary.title = itemsText; // Hover hiện full tên nếu bị cắt ngắn
        } else {
            itemSummary.textContent = '—';
        }

        // Đưa 3 cột vào hàng
        item.append(summary, values, itemSummary);
        list.append(item);
    });

    container.replaceChildren(heading, list);
}

export function renderLoading(container, message) {
    container.replaceChildren(
        createElement('p', 'animate-pulse text-body-md text-on-surface-variant', message)
    );
}

export function clearContent(container) {
    container.replaceChildren();
}

export function renderStatus(container, message, { onRetry, loginHref } = {}) {
    const content = createElement(
        'div',
        'rounded-lg border border-outline-variant bg-surface-container-low p-4 text-body-md text-on-surface-variant',
        message
    );

    if (onRetry) {
        const retry = createElement(
            'button',
            'mt-3 rounded-lg bg-primary px-4 py-2 font-label-md text-label-md text-on-primary',
            'Thử lại'
        );
        retry.type = 'button';
        retry.addEventListener('click', onRetry);
        content.append(retry);
    }

    if (loginHref) {
        const login = createElement(
            'a',
            'ml-3 font-label-md text-label-md font-semibold text-primary underline',
            'Đăng nhập'
        );
        login.href = loginHref;
        content.append(login);
    }

    container.replaceChildren(content);
}