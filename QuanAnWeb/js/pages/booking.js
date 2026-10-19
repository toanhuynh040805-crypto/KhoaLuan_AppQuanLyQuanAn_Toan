import { bookingApi } from '../api/bookingApi.js?auth-session-2';
import { renderBookingTables } from '../components/bookingView.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';

const form = document.querySelector('[data-booking-form]');
if (form) {
    setupSessionNavigation();
    const dateInput = form.elements.date;
    const timeInput = form.elements.time;
    const guestsInput = form.elements.numberOfGuests;
    const tableInput = form.elements.tableId;
    const nameInput = form.elements.customerName;
    const phoneInput = form.elements.customerPhone;
    const submitButton = form.querySelector('[data-booking-submit]');
    const statusElement = document.querySelector('[data-booking-status]');
    const tableStatus = document.querySelector('[data-booking-table-state]');

    let requestVersion = 0;
    let pending = false;
    let completed = false;

    const now = new Date();
    const localToday = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    dateInput.min = localToday;

    function setMessage(element, message, isError = false) {
        if (!element) return;
        element.textContent = message;
        element.classList.toggle('text-error', isError);
        element.classList.toggle('text-on-surface-variant', !isError);
    }

    function validateGuestCount(value) {
        const raw = (value ?? '').toString().trim();
        if (!raw) return 'Vui lòng nhập số lượng khách.';
        if (!/^\d+$/.test(raw)) return 'Số khách phải là số nguyên dương.';
        const guests = Number(raw);
        if (!Number.isInteger(guests) || guests <= 0) return 'Số khách phải lớn hơn 0.';
        return '';
    }

    function validateName(value) {
        const raw = (value ?? '').trim();
        if (!raw) return 'Vui lòng nhập họ và tên.';
        if (raw.length < 2) return 'Họ và tên phải có ít nhất 2 ký tự.';
        return '';
    }

    function validatePhone(value) {
        const raw = (value ?? '').trim();
        if (!raw) return 'Vui lòng nhập số điện thoại.';
        if (/[a-zA-Z]/.test(raw)) return 'Số điện thoại không được chứa chữ cái.';
        if (!/^[\d\s()+-]+$/.test(raw)) return 'Số điện thoại chỉ được chứa chữ số và ký tự cơ bản.';
        const digits = raw.replace(/\D/g, '');
        if (digits.length < 9 || digits.length > 11) return 'Số điện thoại không hợp lệ.';
        return '';
    }

    function validateSchedule() {
        if (!dateInput.value || !timeInput.value) {
            return 'Vui lòng chọn ngày và giờ đặt bàn.';
        }
        const reservationDate = new Date(`${dateInput.value}T${timeInput.value}:00`);
        if (!Number.isFinite(reservationDate.getTime())) {
            return 'Vui lòng chọn thời gian đặt bàn hợp lệ.';
        }
        if (reservationDate < new Date()) {
            return 'Thời gian đặt bàn không được nằm trong quá khứ.';
        }
        const [hours] = timeInput.value.split(':').map(Number);
        if (hours < 8 || hours >= 22) {
            return 'Nhà hàng chỉ hoạt động từ 08:00 đến 22:00.';
        }
        return '';
    }

    function validateBookingForm() {
        return {
            date: validateSchedule(),
            guests: validateGuestCount(guestsInput.value),
            table: tableInput.value ? '' : 'Vui lòng chọn bàn phù hợp.',
            name: validateName(nameInput.value),
            phone: validatePhone(phoneInput.value)
        };
    }

    async function loadAvailability() {
        const version = ++requestVersion;
        tableInput.value = '';
        tableInput.disabled = true;
        tableInput.replaceChildren(new Option('Đang tìm bàn phù hợp...', ''));
        submitButton.disabled = true;

        const scheduleError = validateSchedule();
        const guestError = validateGuestCount(guestsInput.value);
        if (scheduleError || guestError) {
            tableInput.replaceChildren(new Option('Vui lòng nhập đủ ngày, giờ hợp lệ và số khách', ''));
            setMessage(tableStatus, scheduleError || guestError, true);
            return;
        }

        const requestedGuests = Number(guestsInput.value);

        if (requestedGuests > 30) {
            tableInput.replaceChildren(new Option('Vui lòng gọi Hotline', ''));
            setMessage(tableStatus, 'Đối với đoàn khách trên 30 người, vui lòng liên hệ trực tiếp Hotline: 1900 1234.', true);
            return;
        }

        try {
            let tables = await bookingApi.getAvailableTables({
                reservationTime: `${dateInput.value}T${timeInput.value}:00`,
                durationMinutes: 30
            });
            
            if (version !== requestVersion) return;
            if (!Array.isArray(tables)) throw new TypeError('Dữ liệu bàn không hợp lệ.');

            let processedTables = [];

            const normalTables = tables.filter(t => {
                const type = t.tableType ?? t.TableType;
                const cap = Number(t.capacity ?? t.Capacity ?? 4);
                return type !== 'VIP' && cap === 4;
            });

            const vipTables = tables.filter(t => {
                const type = t.tableType ?? t.TableType;
                const cap = Number(t.capacity ?? t.Capacity ?? 0);
                return type === 'VIP' || cap >= 8;
            });

            if (requestedGuests <= 4) {
                processedTables = tables.filter(t => Number(t.capacity ?? t.Capacity ?? 0) >= requestedGuests);
            } 
            else if (requestedGuests > 4 && requestedGuests <= 8) {
                const suitableVIPs = vipTables.filter(t => Number(t.capacity ?? t.Capacity ?? 0) >= requestedGuests);
                processedTables.push(...suitableVIPs);

                const tablesByArea = {};
                normalTables.forEach(t => {
                    const areaId = t.areaId ?? t.AreaId ?? 1;
                    if (!tablesByArea[areaId]) tablesByArea[areaId] = [];
                    tablesByArea[areaId].push(t);
                });

                Object.keys(tablesByArea).forEach(areaId => {
                    const areaTables = tablesByArea[areaId];
                    if (areaTables.length >= 2) {
                        const group = areaTables.slice(0, 2);
                        const groupIds = group.map(t => t.TableId ?? t.tableId ?? t.Id ?? t.id).join(',');
                        const groupNames = group.map(t => t.TableName ?? t.tableName ?? t.Name).join(' + ');
                        processedTables.push({
                            Id: groupIds,
                            Name: `[Hệ thống gợi ý ghép 2 bàn] ${groupNames} (Khu ${areaId})`,
                            Capacity: 8
                        });
                    }
                });
            } 
            else {
                const suitableVIPs = vipTables.filter(t => Number(t.capacity ?? t.Capacity ?? 0) >= requestedGuests);
                processedTables.push(...suitableVIPs);

                const neededTablesCount = Math.ceil(requestedGuests / 4);
                const tablesByArea = {};
                normalTables.forEach(t => {
                    const areaId = t.areaId ?? t.AreaId ?? 1;
                    if (!tablesByArea[areaId]) tablesByArea[areaId] = [];
                    tablesByArea[areaId].push(t);
                });

                Object.keys(tablesByArea).forEach(areaId => {
                    const areaTables = tablesByArea[areaId];
                    if (areaTables.length >= neededTablesCount) {
                        const group = areaTables.slice(0, neededTablesCount);
                        const groupIds = group.map(t => t.TableId ?? t.tableId ?? t.Id ?? t.id).join(',');
                        const groupNames = group.map(t => t.TableName ?? t.tableName ?? t.Name).join(' + ');
                        processedTables.push({
                            Id: groupIds,
                            Name: `[Hệ thống tự động ghép ${neededTablesCount} bàn] ${groupNames} (Khu ${areaId})`,
                            Capacity: neededTablesCount * 4
                        });
                    }
                });
            }

            renderBookingTables(tableInput, processedTables);
            tableInput.disabled = false;
            
            setMessage(
                tableStatus,
                processedTables.length ? `Tìm thấy ${processedTables.length} lựa chọn phòng VIP hoặc phương án ghép bàn phù hợp cho ${requestedGuests} khách.` : 'Không đủ bàn trống hoặc không đủ bàn ghép cho số lượng khách này.',
                processedTables.length === 0
            );

            submitButton.disabled = pending || completed || processedTables.length === 0 || !tableInput.value;

        } catch (error) {
            if (version !== requestVersion) return;
            tableInput.replaceChildren(new Option('Không thể tải danh sách bàn', ''));
            setMessage(tableStatus, error?.message ?? 'Lỗi kết nối khi tải bàn trống.', true);
        }
    }

    [dateInput, timeInput, guestsInput].forEach((input) => input.addEventListener('change', loadAvailability));
    guestsInput.addEventListener('input', loadAvailability);

    tableInput.addEventListener('change', () => {
        submitButton.disabled = !tableInput.value || pending || completed;
        if (statusElement) statusElement.textContent = '';
    });

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (pending || completed) return;

        const validation = validateBookingForm();
        const firstError = Object.values(validation).find((message) => Boolean(message));
        if (firstError) {
            setMessage(statusElement, firstError, true);
            return;
        }

        if (!tableInput.value) {
            setMessage(statusElement, 'Vui lòng chọn bàn phù hợp.', true);
            return;
        }

        pending = true;
        submitButton.disabled = true;
        setMessage(statusElement, 'Đang gửi yêu cầu đặt bàn...');

        try {
            const selectedValue = String(tableInput.value);
            const totalGuests = Number(guestsInput.value);
            
            // Xử lý thông minh: Nếu value có dấu phẩy (ghép nhiều bàn) thì gọi hàm Batch
            if (selectedValue.includes(',')) {
                const tableIds = selectedValue.split(',').map(Number);
                
                // Chia đều khách ra các bàn
                const baseGuestsPerTable = Math.floor(totalGuests / tableIds.length);
                let remainder = totalGuests % tableIds.length;
                
                const tableSlots = tableIds.map(id => {
                    const guestsForThisTable = baseGuestsPerTable + (remainder > 0 ? 1 : 0);
                    remainder--;
                    return { tableId: id, numberOfGuests: guestsForThisTable };
                });

                const batchPayload = {
                    tables: tableSlots,
                    customerName: nameInput.value.trim(),
                    customerPhone: phoneInput.value.trim(),
                    reservationTime: `${dateInput.value}T${timeInput.value}:00`,
                    durationMinutes: 30,
                    depositAmount: 0,
                    note: form.elements.note.value.trim() || null
                };

                const batchResult = await bookingApi.createBookingBatch(batchPayload);
                const groupCode = batchResult?.id ?? batchResult?.Id ?? batchResult?.reservationId ?? batchResult?.ReservationId;

                completed = true;
                if (groupCode === undefined || groupCode === null || String(groupCode).trim() === '') {
                    setMessage(statusElement, 'Đặt bàn thành công nhưng chưa nhận được mã.', true);
                    return;
                }

                localStorage.setItem('gusto_last_booking', JSON.stringify({
                    reservationCode: String(groupCode),
                    date: dateInput.value,
                    time: timeInput.value,
                    numberOfGuests: guestsInput.value,
                    customerName: nameInput.value,
                    customerPhone: phoneInput.value
                }));
                location.assign(`./success.html?code=${encodeURIComponent(String(groupCode))}`);
                return;
            }

            // Nếu đặt 1 bàn
            const singlePayload = {
                tableId: Number(selectedValue), // Bắt buộc ép về kiểu số
                customerName: nameInput.value.trim(),
                customerPhone: phoneInput.value.trim(),
                reservationTime: `${dateInput.value}T${timeInput.value}:00`,
                durationMinutes: 30,
                numberOfGuests: totalGuests,
                depositAmount: 0,
                note: form.elements.note.value.trim() || null
            };

            const reservation = await bookingApi.createBooking(singlePayload);
            
            const reservationCode = reservation?.id ?? reservation?.Id ?? reservation?.reservationId ?? reservation?.ReservationId;
            
            if (reservationCode === undefined || reservationCode === null || String(reservationCode).trim() === '') {
                completed = true;
                setMessage(statusElement, 'Đặt bàn thành công nhưng chưa nhận được mã.', true);
                return;
            }

            completed = true;
            localStorage.setItem('gusto_last_booking', JSON.stringify({
                reservationCode: String(reservationCode),
                date: dateInput.value,
                time: timeInput.value,
                numberOfGuests: guestsInput.value,
                customerName: nameInput.value,
                customerPhone: phoneInput.value
            }));

            location.assign(`./success.html?code=${encodeURIComponent(String(reservationCode))}`);
            
        } catch (error) {
            setMessage(statusElement, error?.message ?? 'Không thể đặt bàn. Vui lòng kiểm tra lại thông tin.', true);
            pending = false;
            submitButton.disabled = false;
        }
    });
}