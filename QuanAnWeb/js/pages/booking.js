import { bookingApi } from '../api/bookingApi.js?table-sync-3';
import {
    renderBookingTables,
    renderBookingTableCards,
    renderAreaFilterPills,
    renderBookingTableAlert,
    renderBookingPagination
} from '../components/bookingView.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js?table-sync-3';

const form = document.querySelector('[data-booking-form]');
if (form) {
    setupSessionNavigation();
    // Đồng bộ cấu hình thời lượng đặt bàn từ Backend khi trang tải
    bookingApi.getConfig().catch(() => {});
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
    const tablePaginationContainer = document.getElementById('booking-table-pagination');
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
    let currentPage = 1;
    const pageSize = 12;

    const padZero = (n) => String(n).padStart(2, '0');
    const getLocalDateString = (d = new Date()) => `${d.getFullYear()}-${padZero(d.getMonth() + 1)}-${padZero(d.getDate())}`;
    const getLocalTimeString = (d = new Date()) => `${padZero(d.getHours())}:${padZero(d.getMinutes())}`;
    dateInput.min = getLocalDateString();

    // Các thành phần của Custom Time Picker Dropdown
    const timeBtn = document.getElementById('booking-time-btn');
    const timeLabel = document.getElementById('booking-time-label');
    const timeArrow = document.getElementById('booking-time-arrow');
    const timeDropdown = document.getElementById('booking-time-dropdown');

    // Cấu trúc 4 buổi với 29 mốc giờ (08:00 - 22:00, bước 30 phút)
    const TIME_PERIODS = [
        {
            name: 'Buổi sáng',
            timeRange: '08:00 - 11:30',
            icon: 'wb_sunny',
            slots: ['08:00', '08:30', '09:00', '09:30', '10:00', '10:30', '11:00', '11:30']
        },
        {
            name: 'Buổi trưa',
            timeRange: '12:00 - 13:30',
            icon: 'light_mode',
            slots: ['12:00', '12:30', '13:00', '13:30']
        },
        {
            name: 'Buổi chiều',
            timeRange: '14:00 - 17:30',
            icon: 'wb_twilight',
            slots: ['14:00', '14:30', '15:00', '15:30', '16:00', '16:30', '17:00', '17:30']
        },
        {
            name: 'Buổi tối',
            timeRange: '18:00 - 20:30',
            icon: 'bedtime',
            slots: ['18:00', '18:30', '19:00', '19:30', '20:00', '20:30']
        }
    ];

    /**
     * Khởi tạo giao diện Custom Dropdown phân nhóm 4 buổi
     * Đồng thời điền options vào thẻ <select> ẩn để bảo toàn 100% logic form & API
     */
    function renderCustomTimeDropdown() {
        if (!timeDropdown) return;

        // Điền options cho thẻ select ẩn (nếu có)
        if (timeInput) {
            timeInput.innerHTML = '<option value="">-- Chọn giờ đến --</option>';
            TIME_PERIODS.forEach((period) => {
                period.slots.forEach((timeStr) => {
                    const opt = document.createElement('option');
                    opt.value = timeStr;
                    opt.textContent = timeStr;
                    timeInput.appendChild(opt);
                });
            });
        }

        // Render giao diện phân nhóm cho Custom Dropdown
        timeDropdown.innerHTML = '';
        TIME_PERIODS.forEach((period) => {
            const section = document.createElement('div');
            section.className = 'space-y-1.5';

            // Header của buổi
            const header = document.createElement('div');
            header.className = 'text-[11px] font-bold uppercase tracking-wider text-on-surface-variant flex items-center justify-between px-1';
            header.innerHTML = `
                <span class="flex items-center gap-1.5 text-primary">
                    <span class="material-symbols-outlined text-[14px]">${period.icon}</span>
                    <span>${period.name}</span>
                </span>
                <span class="text-[10px] text-on-surface-variant/70 font-normal">${period.timeRange}</span>
            `;
            section.appendChild(header);

            // Grid khung giờ 4 cột
            const grid = document.createElement('div');
            grid.className = 'grid grid-cols-4 gap-1.5';

            period.slots.forEach((timeStr) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.dataset.timeSlot = timeStr;
                btn.setAttribute('role', 'option');
                btn.setAttribute('aria-selected', 'false');
                btn.textContent = timeStr;
                btn.className = 'h-9 rounded-lg text-xs font-semibold flex items-center justify-center transition-all bg-surface hover:bg-primary/10 hover:text-primary text-on-surface border border-outline-variant/60 cursor-pointer';

                btn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    if (btn.disabled) return;
                    selectTimeSlot(timeStr);
                });

                grid.appendChild(btn);
            });

            section.appendChild(grid);
            timeDropdown.appendChild(section);
        });
    }

    function selectTimeSlot(timeStr) {
        if (!timeInput) return;
        timeInput.value = timeStr;
        closeTimeDropdown();
        updateTimeSlotStates();
        // Kích hoạt sự kiện change để gọi hàm kiểm tra và gọi API tải bàn trống
        timeInput.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function openTimeDropdown() {
        if (!timeDropdown || !timeBtn) return;
        // Kiểm tra vị trí để mở lên trên nếu thiếu không gian bên dưới
        const rect = timeBtn.getBoundingClientRect();
        const dropdownHeight = 280;
        const spaceBelow = window.innerHeight - rect.bottom;
        if (spaceBelow < dropdownHeight && rect.top > dropdownHeight) {
            timeDropdown.classList.add('bottom-full', 'mb-1');
            timeDropdown.classList.remove('top-full', 'mt-1');
        } else {
            timeDropdown.classList.add('top-full', 'mt-1');
            timeDropdown.classList.remove('bottom-full', 'mb-1');
        }

        timeDropdown.classList.remove('hidden');
        timeBtn.setAttribute('aria-expanded', 'true');
        if (timeArrow) timeArrow.classList.add('rotate-180');
    }

    function closeTimeDropdown() {
        if (!timeDropdown || !timeBtn) return;
        timeDropdown.classList.add('hidden');
        timeBtn.setAttribute('aria-expanded', 'false');
        if (timeArrow) timeArrow.classList.remove('rotate-180');
    }

    function toggleTimeDropdown() {
        if (!timeDropdown) return;
        if (timeDropdown.classList.contains('hidden')) {
            openTimeDropdown();
        } else {
            closeTimeDropdown();
        }
    }

    /**
     * Cập nhật trạng thái disabled, làm mờ các khung giờ quá khứ
     * và đồng bộ nhãn hiển thị trên Custom Dropdown
     */
    function updateTimeSlotStates() {
        if (!timeInput) return false;

        const selectedDate = dateInput?.value;
        const now = new Date();
        const currentDateStr = getLocalDateString(now);
        const currentTimeStr = getLocalTimeString(now);

        const isToday = Boolean(selectedDate && selectedDate === currentDateStr);
        let selectedTimeReset = false;

        // Cập nhật các option trong thẻ select ẩn
        Array.from(timeInput.options).forEach((option) => {
            if (!option.value) return;

            if (isToday && option.value <= currentTimeStr) {
                option.disabled = true;
                if (timeInput.value === option.value) {
                    selectedTimeReset = true;
                }
            } else {
                option.disabled = false;
            }
        });

        // Cập nhật các nút button [data-time-slot] trong Custom Dropdown
        const slotButtons = timeDropdown ? timeDropdown.querySelectorAll('[data-time-slot]') : [];
        slotButtons.forEach((btn) => {
            const slot = btn.dataset.timeSlot;
            const isPast = isToday && slot <= currentTimeStr;
            const isSelected = !isPast && timeInput.value === slot;

            if (isPast) {
                btn.disabled = true;
                btn.className = 'h-9 rounded-lg text-xs font-semibold flex items-center justify-center transition-all opacity-30 cursor-not-allowed bg-surface-container-low text-on-surface-variant/50 line-through border border-transparent select-none';
                btn.setAttribute('aria-selected', 'false');
            } else if (isSelected) {
                btn.disabled = false;
                btn.className = 'h-9 rounded-lg text-xs font-semibold flex items-center justify-center transition-all bg-primary text-on-primary shadow-sm ring-2 ring-primary/30 border border-primary font-bold cursor-pointer';
                btn.setAttribute('aria-selected', 'true');
            } else {
                btn.disabled = false;
                btn.className = 'h-9 rounded-lg text-xs font-semibold flex items-center justify-center transition-all bg-surface hover:bg-primary/10 hover:text-primary text-on-surface border border-outline-variant/60 cursor-pointer';
                btn.setAttribute('aria-selected', 'false');
            }
        });

        // Nếu giờ khách chọn trước đó bị rơi vào khung giờ quá khứ -> reset
        if (selectedTimeReset) {
            timeInput.value = '';
        }

        // Cập nhật nhãn trên nút trigger
        if (timeLabel) {
            if (timeInput.value) {
                timeLabel.textContent = `Giờ đến: ${timeInput.value}`;
                timeLabel.className = 'text-on-surface font-semibold truncate';
            } else {
                timeLabel.textContent = '-- Chọn giờ đến --';
                timeLabel.className = 'text-on-surface-variant truncate';
            }
        }

        return selectedTimeReset;
    }

    // Khởi tạo các khung giờ cho Custom Dropdown và thẻ select
    renderCustomTimeDropdown();
    updateTimeSlotStates();

    // Sự kiện mở/đóng Custom Dropdown
    if (timeBtn) {
        timeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleTimeDropdown();
        });

        timeBtn.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                openTimeDropdown();
            } else if (e.key === 'Escape') {
                closeTimeDropdown();
            }
        });
    }

    // Đóng dropdown khi click ra ngoài
    document.addEventListener('click', (e) => {
        if (!timeDropdown || timeDropdown.classList.contains('hidden')) return;
        const wrapper = document.getElementById('booking-time-picker-wrapper');
        if (wrapper && !wrapper.contains(e.target)) {
            closeTimeDropdown();
        }
    });

    // Đóng dropdown khi nhấn Escape
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && timeDropdown && !timeDropdown.classList.contains('hidden')) {
            closeTimeDropdown();
            timeBtn?.focus();
        }
    });

    // Hiển thị trạng thái khởi tạo
    if (tableSelectorContainer) {
        renderBookingTableCards(tableSelectorContainer, { state: 'empty-input' });
    }
    if (tablePaginationContainer) {
        renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
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
        const now = new Date();
        const currentDateStr = getLocalDateString(now);
        const currentTimeStr = getLocalTimeString(now);

        // Chặn ngày quá khứ
        if (dateInput.value < currentDateStr) {
            return 'Ngày đặt bàn không được nằm trong quá khứ.';
        }

        // Chặn khung giờ nhận đặt bàn (08:00 - 20:30)
        // Để khách có ít nhất 1 tiếng 30 phút dùng món trước khi đóng cửa lúc 22:00
        if (timeInput.value < '08:00' || timeInput.value > '20:30') {
            return 'Nhà hàng chỉ nhận đặt bàn từ 08:00 đến 20:30 (đóng cửa lúc 22:00).';
        }

        // Chặn giờ quá khứ nếu chọn ngày hôm nay
        if (dateInput.value === currentDateStr) {
            if (currentTimeStr >= '20:30') {
                return 'Hôm nay nhà hàng đã hết giờ nhận đặt bàn (nhận bàn muộn nhất lúc 20:30).';
            }
            if (timeInput.value <= currentTimeStr) {
                return `Giờ đặt bàn hôm nay phải sau thời điểm hiện tại (${currentTimeStr}).`;
            }
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
            if (tablePaginationContainer) {
                renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
            }
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
            currentPage = 1; // Luôn đưa về trang 1 khi thay đổi bộ lọc khu vực
            renderCurrentView();
        });

        // Lọc danh sách card hiển thị theo tab khu vực
        const displayed = (activeAreaFilter === 'all')
            ? currentCandidates
            : currentCandidates.filter((t) => t.areaName === activeAreaFilter);

        const totalItems = displayed.length;
        const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
        if (currentPage > totalPages) {
            currentPage = 1;
        }
        if (currentPage < 1) {
            currentPage = 1;
        }

        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = Math.min(startIndex + pageSize, totalItems);
        const paginatedTables = displayed.slice(startIndex, endIndex);

        if (availableCountBadge) {
            availableCountBadge.classList.remove('hidden');
            availableCountBadge.textContent = `${currentCandidates.length} bàn trống`;
        }

        renderBookingTableCards(tableSelectorContainer, {
            tables: paginatedTables,
            selectedId: selectedTableId,
            requestedGuests,
            state: 'loaded',
            onSelect: handleSelectTable
        });

        if (tablePaginationContainer) {
            renderBookingPagination(tablePaginationContainer, {
                currentPage,
                totalPages,
                totalItems,
                startIndex,
                endIndex,
                pageSize,
                onPageChange: (newPage) => {
                    currentPage = newPage;
                    renderCurrentView();
                    tableSelectorContainer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
            });
        }

        renderBookingTables(tableInput, currentCandidates);
        tableInput.value = selectedTableId || '';
        updateSubmitState();
    }

    function handleSelectTable(table) {
        const idStr = String(table.id ?? table.tableId);
        if (selectedTableId === idStr) {
            // Nhấn lại bàn đang chọn -> Bỏ chọn (Deselect)
            selectedTableId = '';
            selectedTableDetails = null;
            tableInput.value = '';
        } else {
            selectedTableId = idStr;
            selectedTableDetails = table;
            tableInput.value = idStr;
            // Xóa thông báo cảnh báo trước đó khi khách đã chọn được bàn mới hợp lệ
            renderBookingTableAlert(tableAlertContainer, '');
        }

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
            currentPage = 1;
            if (tablePaginationContainer) {
                renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
            }
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
            currentPage = 1;
            if (tablePaginationContainer) {
                renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
            }
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
            if (tablePaginationContainer) {
                renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
            }
        }

        try {
            const rawTables = await bookingApi.getAvailableTables({
                reservationTime: `${dateInput.value}T${timeInput.value}:00`
            });

            if (reloadIcon) reloadIcon.classList.remove('animate-spin');
            if (version !== requestVersion) return;
            if (!Array.isArray(rawTables)) throw new TypeError('Dữ liệu bàn không hợp lệ.');

            const candidates = processCandidates(rawTables, requestedGuests);
            currentCandidates = candidates;
            currentPage = 1; // Đưa về trang 1 khi tải dữ liệu mới

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
            currentPage = 1;
            selectedTableId = '';
            selectedTableDetails = null;
            tableInput.value = '';
            tableInput.disabled = true;
            if (availableCountBadge) availableCountBadge.classList.add('hidden');
            if (areaFiltersContainer) areaFiltersContainer.classList.add('hidden');
            if (tablePaginationContainer) {
                renderBookingPagination(tablePaginationContainer, { totalItems: 0 });
            }
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

    function handleDateTimeChange() {
        // Cập nhật lại trạng thái làm mờ / disabled cho select giờ
        const timeWasReset = updateTimeSlotStates();

        if (timeWasReset) {
            setMessage(statusElement, 'Khung giờ đã chọn không hợp lệ cho ngày hôm nay. Vui lòng chọn lại giờ đến!', true);
            updateSubmitState();
            return;
        }

        if (!dateInput.value && !timeInput.value) {
            setMessage(statusElement, '');
            updateSubmitState();
            return;
        }

        const now = new Date();
        const currentDateStr = getLocalDateString(now);
        const currentTimeStr = getLocalTimeString(now);

        // 1. Kiểm tra ngày đặt bàn
        if (dateInput.value && dateInput.value < currentDateStr) {
            setMessage(statusElement, 'Ngày đặt bàn không được chọn trong quá khứ. Vui lòng chọn lại!', true);
            dateInput.value = '';
            timeInput.value = '';
            updateTimeSlotStates();
            updateSubmitState();
            return;
        }

        // 2. Kiểm tra giờ đặt bàn nếu người dùng đã chọn giờ
        if (timeInput.value) {
            // Giới hạn khung giờ nhận đặt bàn 08:00 - 20:30 (dành 1h30 dùng bữa trước khi đóng cửa 22:00)
            if (timeInput.value < '08:00' || timeInput.value > '20:30') {
                setMessage(statusElement, 'Nhà hàng chỉ nhận đặt bàn từ 08:00 đến 20:30 để quý khách có ít nhất 1 tiếng 30 phút dùng bữa trước giờ đóng cửa (22:00)!', true);
                timeInput.value = '';
                updateTimeSlotStates();
                updateSubmitState();
                return;
            }

            // Nếu là ngày hôm nay -> không được chọn giờ trong quá khứ
            if (dateInput.value === currentDateStr) {
                if (currentTimeStr >= '20:30') {
                    setMessage(statusElement, 'Nhà hàng đã hết giờ nhận đặt bàn trong ngày hôm nay (nhận bàn muộn nhất lúc 20:30). Quý khách vui lòng chọn ngày mai trở đi!', true);
                    timeInput.value = '';
                    updateTimeSlotStates();
                    updateSubmitState();
                    return;
                }
                if (timeInput.value <= currentTimeStr) {
                    setMessage(statusElement, `Bạn chọn ngày hôm nay, giờ đến phải sau thời điểm hiện tại (sau ${currentTimeStr}). Vui lòng chọn lại!`, true);
                    timeInput.value = '';
                    updateTimeSlotStates();
                    updateSubmitState();
                    return;
                }
            }
        }

        // Hợp lệ: Xóa thông báo lỗi và tải lại danh sách bàn trống
        if (statusElement) setMessage(statusElement, '');
        currentPage = 1;
        loadAvailability(false);
    }

    [dateInput, timeInput].forEach((input) => {
        input.addEventListener('change', handleDateTimeChange);
        input.addEventListener('input', handleDateTimeChange);
    });
    guestsInput.addEventListener('input', () => {
        currentPage = 1;
        loadAvailability(false);
    });
    guestsInput.addEventListener('change', () => {
        currentPage = 1;
        loadAvailability(false);
    });

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
                    durationMinutes: Number(
                        batchResult?.durationMinutes ??
                        batchResult?.DurationMinutes ??
                        window.AppConfig?.bookingDuration ??
                        30
                    ),
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
                durationMinutes: Number(
                    reservation?.durationMinutes ??
                    reservation?.DurationMinutes ??
                    window.AppConfig?.bookingDuration ??
                    30
                ),
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
