function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

export function getReservationValue(reservation, ...keys) {
    return keys.map((key) => reservation?.[key]).find((value) => value !== undefined && value !== null);
}

export function getReservationStatusLabel(status) {
    if (typeof status === 'string' && status.trim()) {
        const stringMap = {
            'ChoXacNhan': 'Chờ xác nhận',
            'DaXacNhan': 'Đã xác nhận',
            'DaDat': 'Đã xác nhận',
            'DaNhanBan': 'Đã nhận bàn',
            'DaHuy': 'Đã hủy',
            'KhongDen': 'Không đến',
            'HoanTat': 'Hoàn tất'
        };
        if (stringMap[status]) return stringMap[status];
    }

    const numericStatus = Number(status);
    if (Number.isInteger(numericStatus)) {
        const labels = {
            0: 'Đã xác nhận',
            1: 'Đã nhận bàn',
            2: 'Đã hủy',
            3: 'Không đến',
            4: 'Hoàn tất',
            5: 'Chờ xác nhận'
        };
        return labels[numericStatus] || `Trạng thái ${numericStatus}`;
    }
    
    return status || '—';
}

export function isReservationCancellable(reservation) {
    const status = getReservationValue(reservation, 'status', 'Status');
    const normalizedStatus = Number(status);
    const reservationTime = getReservationValue(reservation, 'reservationTime', 'ReservationTime');
    const parsedTime = reservationTime ? new Date(reservationTime) : null;
    const isPastTime = parsedTime && !Number.isNaN(parsedTime.getTime()) && parsedTime <= new Date();

    if (isPastTime) return false;

    // Cho phép hủy khi Chờ xác nhận, Đã xác nhận (0), hoặc Đã nhận bàn (1)
    if (typeof status === 'string') {
        const cancellableStrings = ['ChoXacNhan', 'DaXacNhan', 'DaDat', 'DaNhanBan'];
        if (cancellableStrings.includes(status)) return true;
    }

    if (Number.isInteger(normalizedStatus) && (normalizedStatus === 0 || normalizedStatus === 1 || normalizedStatus === 5)) {
        return true;
    }

    return false;
}

export function renderBookingTableAlert(container, message, type = 'warning') {
    if (!container) return;
    if (!message) {
        container.className = 'hidden';
        container.innerHTML = '';
        return;
    }

    container.classList.remove('hidden');
    const typeConfigs = {
        warning: {
            bg: 'bg-amber-50',
            border: 'border-amber-200',
            text: 'text-amber-900',
            icon: 'warning',
            iconColor: 'text-amber-600'
        },
        error: {
            bg: 'bg-red-50',
            border: 'border-red-200',
            text: 'text-red-900',
            icon: 'error',
            iconColor: 'text-error'
        },
        info: {
            bg: 'bg-blue-50',
            border: 'border-blue-200',
            text: 'text-blue-900',
            icon: 'info',
            iconColor: 'text-blue-600'
        }
    };
    const config = typeConfigs[type] || typeConfigs.warning;
    container.className = `rounded-xl border p-3.5 flex items-start gap-2.5 text-sm transition-all shadow-xs ${config.bg} ${config.border} ${config.text}`;
    container.innerHTML = `
        <span class="material-symbols-outlined text-[20px] shrink-0 mt-0.5 ${config.iconColor}">${config.icon}</span>
        <div class="flex-1 font-medium leading-relaxed">${message}</div>
    `;
}

export function renderAreaFilterPills(container, areas, activeArea, onSelectArea) {
    if (!container) return;
    if (!areas || areas.length <= 1) {
        container.className = 'flex items-center gap-2 overflow-x-auto pb-1.5 pt-0.5 hidden';
        container.innerHTML = '';
        return;
    }

    container.className = 'flex items-center gap-2 overflow-x-auto pb-1.5 pt-0.5 scroll-smooth';
    container.replaceChildren();

    areas.forEach((area) => {
        const isActive = area.id === activeArea;
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `shrink-0 px-3 py-1.5 rounded-full text-xs transition-all flex items-center gap-1.5 cursor-pointer font-medium ${
            isActive
                ? 'bg-primary text-on-primary shadow-xs font-semibold'
                : 'bg-surface-container text-on-surface hover:bg-surface-container-high'
        }`;
        button.innerHTML = `
            <span>${area.name}</span>
            <span class="text-[10px] px-1.5 py-0.2 rounded-full ${
                isActive ? 'bg-white/20 text-white' : 'bg-surface-container-highest text-on-surface-variant'
            }">${area.count}</span>
        `;
        button.addEventListener('click', () => {
            if (typeof onSelectArea === 'function') {
                onSelectArea(area.id);
            }
        });
        container.appendChild(button);
    });
}

export function renderBookingTableCards(container, {
    tables = [],
    selectedId = '',
    requestedGuests = 0,
    state = 'loaded',
    onSelect = null
} = {}) {
    if (!container) return;
    container.replaceChildren();

    if (state === 'empty-input') {
        const box = document.createElement('div');
        box.className = 'w-full py-10 px-4 rounded-xl border border-dashed border-outline-variant/80 flex flex-col items-center justify-center text-center bg-surface-container-lowest text-on-surface-variant';
        box.innerHTML = `
            <span class="material-symbols-outlined text-[36px] text-on-surface-variant/70 mb-2">event_available</span>
            <p class="text-sm font-semibold text-on-surface">Vui lòng chọn ngày, giờ đến và số lượng khách</p>
            <p class="text-xs text-on-surface-variant/80 mt-1 max-w-sm">Hệ thống sẽ tự động tìm kiếm và gợi ý các bàn còn trống phù hợp nhất.</p>
        `;
        container.appendChild(box);
        return;
    }

    if (state === 'loading') {
        const box = document.createElement('div');
        box.className = 'w-full py-10 px-4 rounded-xl border border-outline-variant/60 flex flex-col items-center justify-center text-center bg-surface-container-lowest text-on-surface-variant';
        box.innerHTML = `
            <div class="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mb-2"></div>
            <p class="text-sm font-medium text-on-surface">Đang kiểm tra bàn trống phù hợp...</p>
        `;
        container.appendChild(box);
        return;
    }

    if (state === 'no-match' || tables.length === 0) {
        const box = document.createElement('div');
        box.className = 'w-full py-10 px-4 rounded-xl border border-dashed border-outline-variant/80 flex flex-col items-center justify-center text-center bg-surface-container-lowest text-on-surface-variant';
        box.innerHTML = `
            <span class="material-symbols-outlined text-[36px] text-error/80 mb-2">event_busy</span>
            <p class="text-sm font-semibold text-on-surface">Không tìm thấy bàn trống phù hợp</p>
            <p class="text-xs text-on-surface-variant/80 mt-1 max-w-md">Không có bàn nào đủ sức chứa cho ${requestedGuests || ''} khách vào khung giờ đã chọn. Vui lòng chọn khung giờ khác hoặc liên hệ hotline <strong class="text-primary font-bold">1900 1234</strong> để được sắp xếp.</p>
        `;
        container.appendChild(box);
        return;
    }

    const grid = document.createElement('div');
    grid.className = 'grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-4 gap-3 w-full min-w-0';

    tables.forEach((table) => {
        const id = String(table.id ?? table.tableId ?? table.Id);
        const name = table.name ?? table.tableName ?? table.TableName ?? `Bàn ${id}`;
        const capacity = Number(table.capacity ?? table.Capacity ?? 4);
        const areaName = table.areaName ?? table.AreaName ?? 'Khu vực chung';
        const isSelected = Boolean(selectedId && String(selectedId) === id);
        const isBestFit = Boolean(table.isBestFit);
        const isVip = Boolean(table.isVip || (table.tableType ?? table.TableType) === 'VIP' || (table.areaName && table.areaName.includes('VIP')));
        const isCombined = Boolean(table.isCombined);

        const card = document.createElement('button');
        card.type = 'button';
        card.setAttribute('role', 'radio');
        card.setAttribute('aria-checked', isSelected ? 'true' : 'false');
        card.setAttribute('data-table-card', id);
        card.setAttribute('data-table-capacity', String(capacity));

        const baseClasses = 'group relative text-left p-3.5 rounded-xl border transition-all duration-200 flex flex-col justify-between gap-2.5 cursor-pointer outline-none focus-visible:ring-2 focus-visible:ring-primary';
        const stateClasses = isSelected
            ? 'border-primary ring-2 ring-primary/25 bg-primary/[0.04] shadow-xs'
            : 'border-outline-variant/60 bg-surface-container-lowest hover:border-primary/50 hover:bg-surface-container-low hover:shadow-2xs';

        card.className = `${baseClasses} ${stateClasses}`;

        let badgeHtml = '';
        if (isBestFit) {
            badgeHtml += `
                <span class="inline-flex items-center gap-0.5 text-[10px] font-bold px-1.5 py-0.5 rounded-md bg-emerald-50 text-emerald-700 border border-emerald-200">
                    <span class="material-symbols-outlined text-[12px]">verified</span> Phù hợp nhất
                </span>
            `;
        }
        if (isVip) {
            badgeHtml += `
                <span class="inline-flex items-center gap-0.5 text-[10px] font-bold px-1.5 py-0.5 rounded-md bg-amber-50 text-amber-700 border border-amber-200">
                    <span class="material-symbols-outlined text-[12px]">star</span> VIP
                </span>
            `;
        }
        if (isCombined) {
            badgeHtml += `
                <span class="inline-flex items-center gap-0.5 text-[10px] font-bold px-1.5 py-0.5 rounded-md bg-blue-50 text-blue-700 border border-blue-200">
                    <span class="material-symbols-outlined text-[12px]">call_merge</span> Ghép bàn
                </span>
            `;
        }

        const checkIndicatorClasses = isSelected
            ? 'bg-primary text-on-primary ring-2 ring-primary/20'
            : 'border border-outline-variant/80 text-transparent group-hover:border-primary/50';

        card.innerHTML = `
            <div class="flex items-start justify-between gap-1 w-full">
                <div class="flex flex-wrap items-center gap-1 min-h-[20px]">
                    ${badgeHtml}
                </div>
                <div class="w-5 h-5 rounded-full flex items-center justify-center shrink-0 transition-colors ${checkIndicatorClasses}">
                    <span class="material-symbols-outlined text-[14px]">check</span>
                </div>
            </div>

            <div class="min-w-0 flex-1">
                <h4 class="font-bold text-sm text-on-surface truncate group-hover:text-primary transition-colors" title="${name}">${name}</h4>
                <p class="text-xs text-on-surface-variant truncate mt-0.5" title="${areaName}">${areaName}</p>
            </div>

            <div class="flex items-center justify-between text-xs pt-2 border-t border-outline-variant/30 w-full">
                <span class="inline-flex items-center gap-1 font-semibold text-on-surface-variant">
                    <span class="material-symbols-outlined text-[15px] text-tertiary">group</span>
                    <span>Tối đa ${capacity} khách</span>
                </span>
                <span class="inline-flex items-center gap-1 text-[11px] text-emerald-600 font-medium">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                    <span>Còn trống</span>
                </span>
            </div>
        `;

        card.addEventListener('click', () => {
            if (typeof onSelect === 'function') {
                onSelect(table);
            }
        });

        grid.appendChild(card);
    });

    container.appendChild(grid);
}

export function renderBookingTables(select, tables) {
    if (!select) return;
    const options = [new Option('Chọn bàn phù hợp', '')];
    tables.forEach((table) => {
        const id = table.TableId ?? table.tableId ?? table.Id ?? table.id;
        const rawName = table.TableName ?? table.tableName ?? table.Name ?? table.name;
        const name = typeof rawName === 'string' ? rawName : `Bàn ${id}`;
        const capacity = table.Capacity ?? table.capacity;
        if (id == null) return;
        options.push(new Option(`${name}${capacity ? ` · tối đa ${capacity} khách` : ''}`, String(id)));
    });
    select.replaceChildren(...options);
    select.disabled = tables.length === 0;
}

export function renderReservationDetails(container, reservation) {
    // ĐÃ SỬA: Đổi từ việc tìm 'Code' sang tìm 'Id' để đồng bộ với Backend DTO
    const code = getReservationValue(reservation, 'id', 'Id', 'reservationId', 'ReservationId');
    if ((typeof code !== 'string' && typeof code !== 'number') || String(code).trim() === '') {
        throw new TypeError('Reservation response did not include an ID.');
    }
    
    // BÁM SÁT CHUỖI GỐC TỪ SERVER: Cắt chuỗi trực tiếp để hiển thị chính xác y hệt giá trị thời gian
    const rawTime = getReservationValue(reservation, 'reservationTime', 'ReservationTime');
    let formattedTime = '—';

    if (rawTime) {
        const cleanTimeStr = String(rawTime).replace('Z', '');
        const parts = cleanTimeStr.split('T');
        
        if (parts.length === 2) {
            const dateParts = parts[0].split('-'); // [YYYY, MM, DD]
            if (dateParts.length === 3) {
                const year = dateParts[0];
                const month = dateParts[1];
                const day = dateParts[2];
                const timePart = parts[1].slice(0, 5); // Lấy chính xác chuỗi giờ phút (VD: 19:00)
                
                formattedTime = `${day}/${month}/${year} lúc ${timePart}`;
            } else {
                formattedTime = rawTime;
            }
        } else {
            formattedTime = rawTime;
        }
    }

    const status = getReservationValue(reservation, 'status', 'Status');

    const details = document.createElement('dl');
    details.className = 'grid w-full grid-cols-1 gap-4 text-left sm:grid-cols-2';
    const fields = [
        ['Mã đặt bàn', `#${code}`], // Thêm dấu # phía trước cho đẹp
        ['Trạng thái', getReservationStatusLabel(status)],
        ['Bàn', getReservationValue(reservation, 'tableName', 'TableName') ?? getReservationValue(reservation, 'tableId', 'TableId') ?? '—'],
        ['Tên khách', getReservationValue(reservation, 'customerName', 'CustomerName') ?? '—'],
        ['Số điện thoại', getReservationValue(reservation, 'customerPhone', 'CustomerPhone') ?? '—'],
        ['Thời gian', formattedTime],
        ['Số khách', getReservationValue(reservation, 'numberOfGuests', 'NumberOfGuests') ?? '—'],
        ['Ghi chú', getReservationValue(reservation, 'note', 'Note') ?? '—']
    ];
    
    fields.forEach(([label, value]) => {
        const item = document.createElement('div');
        const term = createElement('dt', 'text-body-sm text-on-surface-variant', label);
        const safeValue = typeof value === 'string' || typeof value === 'number' ? String(value) : '—';
        
        // Tạo class màu sắc tùy chỉnh cho Trạng thái
        let extraClass = '';
        if (label === 'Trạng thái') {
            const numStatus = Number(status);
            if (numStatus === 0) extraClass = 'text-amber-600'; // Đang chờ
            else if (numStatus === 1 || numStatus === 4) extraClass = 'text-green-600'; // Đã nhận bàn / Hoàn tất
            else if (numStatus === 2 || numStatus === 3) extraClass = 'text-red-600'; // Đã hủy / Không đến
        }

        const description = createElement('dd', `mt-1 font-label-lg text-on-surface ${extraClass}`, safeValue);
        item.append(term, description);
        details.append(item);
    });
    container.replaceChildren(details);
}