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
    const numericStatus = Number(status);
    
    // Đã sửa: Map chính xác trạng thái từ số sang chữ giống hệt WinForms
    if (Number.isInteger(numericStatus)) {
        const labels = {
            0: 'Đã xác nhận', // Đang chờ
            1: 'Đã nhận bàn',
            2: 'Đã hủy',
            3: 'Không đến',
            4: 'Hoàn tất'
        };
        return labels[numericStatus] || `Trạng thái ${numericStatus}`;
    }
    
    if (typeof status === 'string' && status.trim()) {
        return status;
    }
    return '—';
}

export function isReservationCancellable(reservation) {
    const status = getReservationValue(reservation, 'status', 'Status');
    const normalizedStatus = Number(status);
    const reservationTime = getReservationValue(reservation, 'reservationTime', 'ReservationTime');
    const parsedTime = reservationTime ? new Date(reservationTime) : null;
    const isPastTime = parsedTime && !Number.isNaN(parsedTime.getTime()) && parsedTime <= new Date();

    if (isPastTime) return false;

    // Chỉ cho phép hủy khi Đã xác nhận (0) hoặc Đã nhận bàn (1)
    if (Number.isInteger(normalizedStatus) && (normalizedStatus === 0 || normalizedStatus === 1)) {
        return true;
    }

    return false;
}

export function renderBookingTables(select, tables) {
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