import { bookingApi } from '../api/bookingApi.js?table-sync-3';
import {
    renderBookingTables,
    renderBookingTableCards,
    renderAreaFilterPills,
    renderBookingTableAlert
} from '../components/bookingView.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js?table-sync-3';

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

    const tableSelectorContainer = document.getElementById('booking-table-selector');
    const tableAlertContainer = document.getElementById('booking-table-alert');
    const areaFiltersContainer = document.getElementById('booking-area-filters');
    const availableCountBadge = document.getElementById('booking-available-tables-count');
    const reloadButton = document.getElementById('btn-reload-tables');
    const reloadIcon = document.getElementById('icon-reload-tables');

    let requestVersion = 0;
    let pending = false;
    let completed = false;

    let currentCandidates = [];
    let selectedTableId = '';
    let selectedTableDetails = null;
    let activeAreaFilter = 'all';

    const now = new Date();
    const localToday = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    dateInput.min = localToday;

    // Hiển thị trạng thái khởi tạo
    if (tableSelectorContainer) {
        renderBookingTableCards(tableSelectorContainer, { state: 'empty-input' });
    }

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

    function updateSubmitState() {
        if (!submitButton) return;
        const hasTable = Boolean(selectedTableId && tableInput.value);
        const hasValidSchedule = !validateSchedule();
        const hasValidGuests = !validateGuestCount(guestsInput.value);
        const hasValidName = !validateName(nameInput.value);
        const hasValidPhone = !validatePhone(phoneInput.value);

        submitButton.disabled = pending || completed || !hasTable || !hasValidSchedule || !hasValidGuests || !hasValidName || !hasValidPhone;
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

    function generateAreaCombinations(standardTables, requestedGuests) {
        if (requestedGuests <= 4) return [];
        const neededCount = Math.ceil(requestedGuests / 4);
        if (neededCount < 2) return [];

        const tablesByArea = {};
        standardTables.forEach((t) => {
            const areaId = t.areaId ?? t.AreaId ?? 1;
            const areaName = t.areaName ?? t.AreaName ?? `Khu ${areaId}`;
            if (!tablesByArea[areaId]) {
                tablesByArea[areaId] = {
                    areaId,
                    areaName,
                    tables: []
                };
            }
            tablesByArea[areaId].tables.push(t);
        });

        const combinations = [];
        Object.values(tablesByArea).forEach((group) => {
            if (group.tables.length >= neededCount) {
                const selectedSubTables = group.tables.slice(0, neededCount);
                const comboIds = selectedSubTables.map((t) => t.tableId ?? t.TableId ?? t.id ?? t.Id).join(',');
                const comboNames = selectedSubTables.map((t) => t.tableName ?? t.TableName ?? t.name ?? t.Name).join(' + ');
                const totalCapacity = neededCount * 4;
                combinations.push({
                    id: comboIds,
                    tableId: comboIds,
                    name: `Ghép ${neededCount} bàn: ${comboNames}`,
                    tableName: `Ghép ${neededCount} bàn: ${comboNames}`,
                    capacity: totalCapacity,
                    areaId: group.areaId,
                    areaName: group.areaName,
                    isCombined: true,
                    tableCount: neededCount,
                    subTableIds: selectedSubTables.map((t) => Number(t.tableId ?? t.TableId ?? t.id ?? t.Id))
                });
            }
        });
        return combinations;
    }

    function processCandidates(rawTables, requestedGuests) {
        const availableTables = rawTables.filter((t) => t.isAvailable !== false && t.IsAvailable !== false);

        const standardTables = availableTables.filter((t) => {
            const type = t.tableType ?? t.TableType;
            const cap = Number(t.capacity ?? t.Capacity ?? 4);
            const name = (t.tableName ?? t.TableName ?? '').toUpperCase();
            return type !== 'VIP' && !name.includes('VIP') && cap === 4;
        });

        const singleCandidates = availableTables.map((t) => {
            const id = t.tableId ?? t.TableId ?? t.id ?? t.Id;
            const name = t.tableName ?? t.TableName ?? t.name ?? t.Name ?? `Bàn ${id}`;
            const capacity = Number(t.capacity ?? t.Capacity ?? 4);
            const areaId = t.areaId ?? t.AreaId ?? 1;
            const areaName = t.areaName ?? t.AreaName ?? `Khu ${areaId}`;
            const type = t.tableType ?? t.TableType;
            const isVip = type === 'VIP' || name.toUpperCase().includes('VIP') || areaName.toUpperCase().includes('VIP');

            return {
                id: String(id),
                tableId: id,
                name,
                tableName: name,
                capacity,
                areaId,
                areaName,
                currentStatus: t.currentStatus ?? t.CurrentStatus ?? 0,
                isVip,
                isCombined: false,
                tableCount: 1
            };
        });

        // Tạo phương án ghép bàn trong cùng khu vực khi đi từ 5 người trở lên
        const comboCandidates = generateAreaCombinations(standardTables, requestedGuests);

        // HỢP NHẤT TOÀN BỘ LỰA CHỌN
        const all = [...singleCandidates, ...comboCandidates];

        // 1. KIỂM SOÁT SỨC CHỨA NGHIÊM NGẶT: Loại bỏ hoàn toàn bàn có capacity < requestedGuests
        const validCandidates = all.filter((t) => t.capacity >= requestedGuests);
        if (validCandidates.length === 0) return [];

        // 2. TÌM SỨC CHỨA VỪA VẶN NHẤT (nhỏ nhất trong số các bàn đủ sức chứa)
        const minCapacity = Math.min(...validCandidates.map((t) => t.capacity));

        validCandidates.forEach((t) => {
            t.isBestFit = t.capacity === minCapacity;
        });

        // 3. SẮP XẾP ƯU TIÊN:
        // - Ưu tiên 1: Chênh lệch sức chứa (capacity - requestedGuests) TĂNG DẦN
        // - Ưu tiên 2: Bàn đơn trước bàn ghép (nếu chênh lệch bằng nhau)
        // - Ưu tiên 3: Tên khu vực, tên bàn
        validCandidates.sort((a, b) => {
            const diffA = a.capacity - requestedGuests;
            const diffB = b.capacity - requestedGuests;
            if (diffA !== diffB) return diffA - diffB;
            if (Boolean(a.isCombined) !== Boolean(b.isCombined)) {
                return a.isCombined ? 1 : -1;
            }
            if (a.areaName !== b.areaName) return a.areaName.localeCompare(b.areaName);
            return a.name.localeCompare(b.name);
        });

        return validCandidates;
    }

    function renderCurrentView() {
        if (!tableSelectorContainer) return;

        const requestedGuests = Number(guestsInput.value) || 0;

        if (!currentCandidates || currentCandidates.length === 0) {
            renderBookingTableCards(tableSelectorContainer, {
                state: 'no-match',
                requestedGuests
            });
            if (areaFiltersContainer) areaFiltersContainer.classList.add('hidden');
            if (availableCountBadge) {
                availableCountBadge.classList.remove('hidden');
                availableCountBadge.textContent = '0 bàn trống';
            }
            renderBookingTables(tableInput, []);
            tableInput.value = '';
            selectedTableId = '';
            selectedTableDetails = null;
            updateSubmitState();
            return;
        }

        // Tạo danh sách tabs lọc khu vực
        const areasMap = new Map();
        currentCandidates.forEach((t) => {
            const aName = t.areaName || 'Khu vực khác';
            areasMap.set(aName, (areasMap.get(aName) || 0) + 1);
        });

        const areasList = [
            { id: 'all', name: 'Tất cả', count: currentCandidates.length }
        ];
        areasMap.forEach((count, aName) => {
            areasList.push({ id: aName, name: aName, count });
        });

        if (!areasList.some((a) => a.id === activeAreaFilter)) {
            activeAreaFilter = 'all';
        }

        renderAreaFilterPills(areaFiltersContainer, areasList, activeAreaFilter, (newAreaId) => {
            activeAreaFilter = newAreaId;
            renderCurrentView();
        });

        // Lọc danh sách card hiển thị theo tab khu vực
        const displayed = (activeAreaFilter === 'all')
            ? currentCandidates
            : currentCandidates.filter((t) => t.areaName === activeAreaFilter);

        if (availableCountBadge) {
            availableCountBadge.classList.remove('hidden');
            availableCountBadge.textContent = `${currentCandidates.length} bàn trống`;
        }

        renderBookingTableCards(tableSelectorContainer, {
            tables: displayed,
            selectedId: selectedTableId,
            requestedGuests,
            state: 'loaded',
            onSelect: handleSelectTable
        });

        renderBookingTables(tableInput, currentCandidates);
        tableInput.value = selectedTableId || '';
        updateSubmitState();
    }

    function handleSelectTable(table) {
        const idStr = String(table.id ?? table.tableId);
        selectedTableId = idStr;
        selectedTableDetails = table;
        tableInput.value = idStr;

        // Xóa thông báo cảnh báo trước đó khi khách đã chọn được bàn mới hợp lệ
        renderBookingTableAlert(tableAlertContainer, '');
        renderCurrentView();
        updateSubmitState();
        if (statusElement) statusElement.textContent = '';
    }

    async function loadAvailability(isManualReload = false) {
        const version = ++requestVersion;

        if (reloadIcon && isManualReload) {
            reloadIcon.classList.add('animate-spin');
        }

        const scheduleError = validateSchedule();
        const guestError = validateGuestCount(guestsInput.value);

        if (scheduleError || guestError) {
            if (reloadIcon) reloadIcon.classList.remove('animate-spin');
            tableInput.value = '';
            tableInput.disabled = true;
            selectedTableId = '';
            selectedTableDetails = null;
            currentCandidates = [];
            if (availableCountBadge) availableCountBadge.classList.add('hidden');
            if (areaFiltersContainer) areaFiltersContainer.classList.add('hidden');
            renderBookingTableCards(tableSelectorContainer, { state: 'empty-input' });
            renderBookingTableAlert(tableAlertContainer, '');
            setMessage(tableStatus, scheduleError || guestError, Boolean(scheduleError || guestError));
            updateSubmitState();
            return;
        }

        const requestedGuests = Number(guestsInput.value);

        if (requestedGuests > 30) {
            if (reloadIcon) reloadIcon.classList.remove('animate-spin');
            tableInput.value = '';
            tableInput.disabled = true;
            selectedTableId = '';
            selectedTableDetails = null;
            currentCandidates = [];
            if (availableCountBadge) availableCountBadge.classList.add('hidden');
            if (areaFiltersContainer) areaFiltersContainer.classList.add('hidden');
            renderBookingTableCards(tableSelectorContainer, {
                state: 'no-match',
                requestedGuests
            });
            renderBookingTableAlert(
                tableAlertContainer,
                'Đối với đoàn khách trên 30 người, vui lòng liên hệ trực tiếp Hotline: 1900 1234 để nhân viên sắp xếp không gian tiệc tốt nhất.',
                'info'
            );
            setMessage(tableStatus, 'Đoàn trên 30 người vui lòng liên hệ Hotline.', true);
            updateSubmitState();
            return;
        }

        if (currentCandidates.length === 0) {
            renderBookingTableCards(tableSelectorContainer, { state: 'loading' });
        }

        try {
            const rawTables = await bookingApi.getAvailableTables({
                reservationTime: `${dateInput.value}T${timeInput.value}:00`,
                durationMinutes: 30
            });

            if (reloadIcon) reloadIcon.classList.remove('animate-spin');
            if (version !== requestVersion) return;
            if (!Array.isArray(rawTables)) throw new TypeError('Dữ liệu bàn không hợp lệ.');

            const candidates = processCandidates(rawTables, requestedGuests);
            currentCandidates = candidates;

            // KIỂM SOÁT VÀ TỰ ĐỘNG BỎ CHỌN BÀN KHI THAY ĐỔI SỐ KHÁCH:
            if (selectedTableId) {
                const stillValid = candidates.find((t) => String(t.id) === String(selectedTableId));
                if (!stillValid) {
                    const previousCapacity = selectedTableDetails?.capacity;
                    selectedTableId = '';
                    selectedTableDetails = null;
                    tableInput.value = '';

                    const warningMsg = previousCapacity
                        ? `Bàn bạn vừa chọn (sức chứa tối đa ${previousCapacity} người) không đủ cho ${requestedGuests} khách. Vui lòng chọn lại bàn phù hợp bên dưới.`
                        : `Bàn đã chọn không còn phù hợp với số lượng ${requestedGuests} khách. Vui lòng chọn lại bàn khác.`;

                    renderBookingTableAlert(tableAlertContainer, warningMsg, 'warning');
                } else {
                    selectedTableDetails = stillValid;
                }
            }

            tableInput.disabled = candidates.length === 0;

            const bestFitCount = candidates.filter((t) => t.isBestFit).length;
            setMessage(
                tableStatus,
                candidates.length
                    ? `Tìm thấy ${candidates.length} phương án khả dụng (${bestFitCount} lựa chọn vừa vặn nhất) cho ${requestedGuests} khách.`
                    : `Không có bàn nào đủ sức chứa cho ${requestedGuests} khách vào khung giờ này.`,
                candidates.length === 0
            );

            renderCurrentView();

        } catch (error) {
            if (reloadIcon) reloadIcon.classList.remove('animate-spin');
            if (version !== requestVersion) return;
            currentCandidates = [];
            selectedTableId = '';
            selectedTableDetails = null;
            tableInput.value = '';
            tableInput.disabled = true;
            if (availableCountBadge) availableCountBadge.classList.add('hidden');
            if (areaFiltersContainer) areaFiltersContainer.classList.add('hidden');
            renderBookingTableCards(tableSelectorContainer, {
                state: 'no-match',
                requestedGuests
            });
            renderBookingTableAlert(
                tableAlertContainer,
                error?.message ?? 'Lỗi kết nối khi tải danh sách bàn trống.',
                'error'
            );
            setMessage(tableStatus, error?.message ?? 'Lỗi kết nối khi tải bàn trống.', true);
            updateSubmitState();
        }
    }

    [dateInput, timeInput].forEach((input) => input.addEventListener('change', () => loadAvailability(false)));
    guestsInput.addEventListener('input', () => loadAvailability(false));
    guestsInput.addEventListener('change', () => loadAvailability(false));

    [nameInput, phoneInput].forEach((input) => {
        input.addEventListener('input', updateSubmitState);
        input.addEventListener('change', updateSubmitState);
    });

    if (reloadButton) {
        reloadButton.addEventListener('click', () => {
            if (!pending && !completed) {
                loadAvailability(true);
            }
        });
    }

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

            // Đặt ghép nhiều bàn (giá trị có chứa dấu phẩy)
            if (selectedValue.includes(',')) {
                const tableIds = selectedValue.split(',').map((id) => Number(id.trim()));

                const baseGuestsPerTable = Math.floor(totalGuests / tableIds.length);
                let remainder = totalGuests % tableIds.length;

                const tableSlots = tableIds.map((id) => {
                    const guestsForThisTable = baseGuestsPerTable + (remainder > 0 ? 1 : 0);
                    remainder--;
                    return { tableId: id, numberOfGuests: Math.max(guestsForThisTable, 1) };
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
                const groupCode = batchResult?.id ?? batchResult?.Id ?? batchResult?.Ids?.[0] ?? batchResult?.ids?.[0] ?? batchResult?.reservationId ?? batchResult?.ReservationId;

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
                    customerPhone: phoneInput.value,
                    durationMinutes: Number(batchResult?.durationMinutes ?? batchResult?.DurationMinutes ?? 30),
                    holdUntil: batchResult?.holdUntil ?? batchResult?.HoldUntil ?? null
                }));
                location.assign(`./success.html?code=${encodeURIComponent(String(groupCode))}`);
                return;
            }

            // Đặt bàn đơn
            const singlePayload = {
                tableId: Number(selectedValue),
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
                customerPhone: phoneInput.value,
                durationMinutes: Number(reservation?.durationMinutes ?? reservation?.DurationMinutes ?? 30),
                holdUntil: reservation?.holdUntil ?? reservation?.HoldUntil ?? null
            }));

            location.assign(`./success.html?code=${encodeURIComponent(String(reservationCode))}`);

        } catch (error) {
            setMessage(statusElement, error?.message ?? 'Không thể đặt bàn. Vui lòng kiểm tra lại thông tin.', true);
            pending = false;
            updateSubmitState();
        }
    });

    // Realtime dùng chung cho Web/Mobile/WinForms. Không tự sửa một card
    // bằng dữ liệu event vì một bàn có thể có TableId trùng cũ; luôn tải lại
    // nguồn chuẩn /tables/available để giữ đúng cùng trạng thái với hai nền tảng kia.
    let realtimeReloadTimer = null;

    function scheduleAvailabilitySync(delay = 180) {
        if (!dateInput.value || !timeInput.value || pending || completed) return;

        if (realtimeReloadTimer) {
            clearTimeout(realtimeReloadTimer);
        }

        realtimeReloadTimer = setTimeout(() => {
            realtimeReloadTimer = null;
            loadAvailability(false);
        }, delay);
    }

    realtimeClient.on('TableStatusUpdated', (data) => {
        const changedId = data?.TableId ?? data?.tableId ?? data?.id;
        const changedName = data?.TableName ?? data?.tableName ?? (changedId ? `Bàn ${changedId}` : '');
        const rawStatus = data?.Status ?? data?.status;
        const numericStatus = typeof rawStatus === 'number'
            ? rawStatus
            : Number(rawStatus);

        if (Number.isFinite(numericStatus) && numericStatus !== 0 && changedId) {
            const idStr = String(changedId);
            const selectedIds = selectedTableId
                ? selectedTableId.split(',').map((value) => value.trim())
                : [];

            if (selectedIds.includes(idStr)) {
                selectedTableId = '';
                selectedTableDetails = null;
                tableInput.value = '';
                updateSubmitState();
                renderBookingTableAlert(
                    tableAlertContainer,
                    `Bàn ${changedName} vừa chuyển sang trạng thái bận/đã đặt. Danh sách đang được đồng bộ lại.`,
                    'warning'
                );
            }
        }

        scheduleAvailabilitySync();
    });

    // ReservationUpdated rất quan trọng với đặt bàn xa giờ: Table.Status có thể
    // chưa chuyển DaDat ngay nhưng khung giờ tương lai đã không còn khả dụng.
    realtimeClient.on('ReservationUpdated', () => scheduleAvailabilitySync());
    realtimeClient.on('PaymentCompleted', () => scheduleAvailabilitySync());
    realtimeClient.on('OrderUpdated', () => scheduleAvailabilitySync());
    realtimeClient.on('reconnected', () => scheduleAvailabilitySync(0));
    realtimeClient.on('sync', () => scheduleAvailabilitySync(0));

    realtimeClient.start().catch(() => {});
}
