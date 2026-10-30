import { hasCustomerSession, hasCustomerRole, isTokenExpired } from '../api/session.js?auth-session-2';
import { ensureCustomerSession } from '../api/authSession.js?auth-session-2';
import { menuApi } from '../api/menuApi.js?auth-session-2';
import { orderApi } from '../api/orderApi.js?auth-session-2';
import { profileApi } from '../api/profileApi.js?auth-session-2';
import { tableApi } from '../api/tableApi.js?auth-session-2';
import { readCart, writeCart } from '../components/cartStore.js';
import { renderCheckoutCart, renderCheckoutMessage, renderCheckoutRecovery, renderCheckoutTables, renderCheckoutTableAlert } from '../components/checkoutView.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';

const authState = document.querySelector('[data-checkout-auth-state]');
const checkoutForm = document.querySelector('[data-checkout-form]');
const cartElement = document.querySelector('[data-checkout-cart]');
const customerElement = document.querySelector('[data-checkout-customer]');
const tableSelect = document.querySelector('[data-checkout-table]');
const tableGridContainer = document.getElementById('checkout-table-selector');
const tableCountBadge = document.getElementById('checkout-available-tables-count');
const tableAlertContainer = document.getElementById('checkout-table-alert');
const statusElement = document.querySelector('[data-checkout-status]');
const recoveryElement = document.querySelector('[data-checkout-recovery]');
const submitButton = document.querySelector('[data-checkout-submit]');
const customSubmitButton = document.getElementById('custom-submit-btn');
const PENDING_REQUEST_KEY = 'gusto_pending_checkout_request';

async function initializeCheckout() {
    if (!authState || !checkoutForm || !cartElement || !customerElement || !tableSelect || !statusElement || !submitButton) {
        return;
    }

    setupSessionNavigation();
    await ensureCustomerSession();

    let cart = normalizeCart(readCart());
    let customerLoaded = false;
    let tablesLoaded = false;
    let pending = false;
    let sessionExpired = false;
    let uncertainSubmission = false;
    let savedRequest = null;
    let selectedTableId = '';
    let activeAreaFilter = 'all';
    let cachedAvailableTables = [];
    const catalogErrors = new Map();
    const AVAILABLE_TABLE_STATUS = [0];

    globalThis.addEventListener('gusto:unauthorized', handleUnauthorized);

    function handleUnauthorized() {
        sessionExpired = true;
        pending = false;
        checkoutForm.classList.add('hidden');
        authState.classList.remove('hidden');
        renderCheckoutMessage(
            authState,
            'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.',
            {
                loginHref: '/pages/auth/login_register.html?returnUrl=%2Fpages%2Fcheckout%2Findex.html',
                type: 'warning'
            }
        );
        updateSubmit();
    }

    function normalizeCart(source) {
        if (!Array.isArray(source)) return [];
        return source.flatMap((item) => {
            const id = Number(item?.id);
            const price = Number(item?.price);
            const quantity = Number(item?.quantity);
            if (!Number.isSafeInteger(id) || id <= 0 ||
                !Number.isFinite(price) || price < 0 ||
                !Number.isInteger(quantity) || quantity < 1 || quantity > 99) {
                return [];
            }
            return [{
                ...item,
                id,
                price,
                quantity,
                note: typeof item?.note === 'string' ? item.note.slice(0, 255) : ''
            }];
        });
    }

    function setStatus(message = '', isError = false) {
        statusElement.textContent = message;
        statusElement.classList.toggle('text-error', isError);
        statusElement.classList.toggle('text-on-surface-variant', !isError);
        statusElement.classList.toggle('text-tertiary', !isError && message.toLowerCase().includes('thành công'));
    }

    try {
        const serializedRequest = sessionStorage.getItem(PENDING_REQUEST_KEY);
        savedRequest = serializedRequest ? JSON.parse(serializedRequest) : null;
        uncertainSubmission = Boolean(savedRequest);
    } catch {
        uncertainSubmission = true;
        setStatus('Không thể đọc mã yêu cầu cũ. Giữ nguyên dữ liệu trình duyệt và liên hệ nhân viên để đối soát trước khi gửi đơn khác.', true);
    }
    if (uncertainSubmission && isValidRequestId(savedRequest?.requestId)) {
        renderRecoveryPrompt(
            'Có yêu cầu đặt món trước đó chưa được đối soát. Kiểm tra trạng thái bằng mã cũ trước; giỏ hàng hiện tại sẽ được giữ nguyên.',
            [{ label: 'Kiểm tra yêu cầu cũ', onClick: checkSavedRequest }]
        );
    } else if (uncertainSubmission) {
        renderRecoveryPrompt('Không thể xác minh dữ liệu yêu cầu đang chờ. Không gửi đơn mới; giữ nguyên dữ liệu trình duyệt để nhân viên đối soát.', []);
    }

    function canSubmit() {
        return (
            !sessionExpired &&
            !uncertainSubmission &&
            hasCustomerSession() &&
            hasCustomerRole() &&
            !isTokenExpired() &&
            customerLoaded &&
            tablesLoaded &&
            Boolean(tableSelect.value) &&
            cart.length > 0 &&
            !pending
        );
    }

    function updateSubmit() {
        const disabled = !canSubmit();
        submitButton.disabled = disabled;
        if (customSubmitButton) customSubmitButton.disabled = disabled;
    }

    function setSubmitButton(text, icon = 'send') {
        if (!customSubmitButton) return;
        customSubmitButton.replaceChildren();
        const iconElement = document.createElement('span');
        iconElement.className = 'material-symbols-outlined text-[22px]';
        iconElement.textContent = icon;
        customSubmitButton.append(iconElement, document.createTextNode(text));
    }

    function persistCart() {
        if (writeCart(cart)) return true;
        setStatus('Không thể lưu thay đổi giỏ hàng trong trình duyệt. Vui lòng kiểm tra dung lượng lưu trữ rồi thử lại.', true);
        return false;
    }

    function updateCart() {
        cart = normalizeCart(cart);
        persistCart();

        if (cart.length === 0) {
            catalogErrors.clear();
            renderCheckoutMessage(cartElement, 'Giỏ hàng đang trống. Hãy quay lại thực đơn để chọn món.');
            updateSubmit();
            return;
        }

        const displayItems = cart.map((item) => ({
            ...item,
            catalogError: catalogErrors.get(String(item.id)) ?? ''
        }));

        renderCheckoutCart(
            cartElement,
            displayItems,
            (index, quantity) => {
                if (index < 0 || index >= cart.length) return;
                const nextQuantity = Number(quantity);
                if (nextQuantity === 0) {
                    catalogErrors.delete(String(cart[index].id));
                    cart.splice(index, 1);
                } else if (Number.isInteger(nextQuantity) && nextQuantity >= 1 && nextQuantity <= 99) {
                    cart[index].quantity = nextQuantity;
                }
                updateCart();
            },
            (index, note) => {
                if (index < 0 || index >= cart.length) return;
                cart[index].note = String(note ?? '').slice(0, 255);
                persistCart();
            }
        );

        updateSubmit();
    }

    async function refreshCatalogPrices() {
        if (cart.length === 0) {
            catalogErrors.clear();
            updateCart();
            return true;
        }

        try {
            const dishes = await menuApi.getDishes();
            const dishesById = new Map(dishes.map((dish) => [String(dish.id), dish]));
            catalogErrors.clear();

            cart = cart.map((item) => {
                const dish = dishesById.get(String(item.id));
                if (!dish) {
                    catalogErrors.set(String(item.id), 'Món này không còn trong thực đơn.');
                    return item;
                }

                item.name = dish.name;
                item.price = dish.price;
                if (!dish.isAvailable) {
                    catalogErrors.set(String(item.id), 'Món hiện không còn bán.');
                } else if (!Number.isFinite(dish.price) || dish.price <= 0) {
                    catalogErrors.set(String(item.id), 'Giá món chưa được cấu hình hợp lệ.');
                }
                return item;
            });

            updateCart();
            return catalogErrors.size === 0;
        } catch (error) {
            cart.forEach((item) => catalogErrors.set(String(item.id), 'Không thể xác minh món và đơn giá từ thực đơn.'));
            updateCart();
            setStatus(error?.message || 'Không thể tải thực đơn để xác minh giá. Giỏ hàng vẫn được giữ lại.', true);
            return false;
        }
    }

    function renderCustomer(customer) {
        const name = customer?.FullName ?? customer?.fullName ?? customer?.Name ?? customer?.name ?? 'Khách hàng';
        const phone = customer?.Phone ?? customer?.phone ?? '—';
        const email = customer?.Email ?? customer?.email ?? '—';
        const wrapper = document.createElement('div');
        wrapper.className = 'grid gap-3 sm:grid-cols-3';

        [
            ['Khách hàng', name, 'person'],
            ['Số điện thoại', phone, 'phone'],
            ['Email', email, 'mail']
        ].forEach(([label, value, icon]) => {
            const item = document.createElement('div');
            item.className = 'rounded-xl bg-surface-container-low p-3';
            const labelRow = document.createElement('div');
            labelRow.className = 'flex items-center gap-2 text-xs text-on-surface-variant';
            const iconElement = document.createElement('span');
            iconElement.className = 'material-symbols-outlined text-[17px]';
            iconElement.textContent = icon;
            const labelElement = document.createElement('span');
            labelElement.textContent = label;
            labelRow.append(iconElement, labelElement);
            const valueElement = document.createElement('p');
            valueElement.className = 'mt-1 truncate text-sm font-semibold text-on-surface';
            valueElement.textContent = value;
            item.append(labelRow, valueElement);
            wrapper.append(item);
        });

        customerElement.replaceChildren(wrapper);
    }

    async function loadCustomer() {
        try {
            const customer = await profileApi.getMyProfile();
            if (sessionExpired) return;
            renderCustomer(customer);
            customerLoaded = true;
        } catch (error) {
            customerLoaded = false;
            renderCheckoutMessage(
                customerElement,
                error?.message || 'Không thể tải thông tin khách hàng.',
                { type: 'error' }
            );
        }
        updateSubmit();
    }

    function handleTableSelection(id) {
        if (!id) return;
        selectedTableId = String(id);
        tableSelect.value = String(id);
        renderCheckoutTableAlert(tableAlertContainer, null);
        setStatus('');
        tableSelect.dispatchEvent(new Event('change', { bubbles: true }));
        updateSubmit();

        renderCheckoutTables(tableSelect, cachedAvailableTables, {
            gridContainer: tableGridContainer,
            countBadge: tableCountBadge,
            selectedTableId,
            activeAreaFilter,
            onSelectTable: handleTableSelection,
            onSelectArea: handleAreaFilter
        });
    }

    function handleAreaFilter(area) {
        activeAreaFilter = area;
        renderCheckoutTables(tableSelect, cachedAvailableTables, {
            gridContainer: tableGridContainer,
            countBadge: tableCountBadge,
            selectedTableId,
            activeAreaFilter,
            onSelectTable: handleTableSelection,
            onSelectArea: handleAreaFilter
        });
    }

    async function loadTables(realtimeData = null) {
        const previousTable = selectedTableId || tableSelect.value;
        try {
            // Lấy danh sách bàn trống thực tế từ Backend
            const tables = await tableApi.getAvailableTables();
            if (sessionExpired) return;

            let effectiveTables = tables;

            // Xử lý cập nhật tức thời từ sự kiện realtime nếu có
            if (realtimeData && (realtimeData.tableId !== undefined || realtimeData.TableId !== undefined)) {
                const rtId = String(realtimeData.tableId ?? realtimeData.TableId);
                const rtStatus = String(realtimeData.status ?? realtimeData.Status ?? '').toLowerCase();
                const isRtFree = rtStatus === 'trong' || rtStatus === '0' || realtimeData.status === 0 || realtimeData.Status === 0;

                if (!isRtFree) {
                    // Bàn chuyển sang bận -> lập tức loại khỏi danh sách bàn trống
                    effectiveTables = effectiveTables.filter(t => String(t.id) !== rtId);
                } else if (!effectiveTables.some(t => String(t.id) === rtId)) {
                    // Bàn chuyển sang trống -> bổ sung vào danh sách nếu chưa có
                    effectiveTables = [
                        ...effectiveTables,
                        {
                            id: Number(rtId),
                            name: realtimeData.tableName ?? realtimeData.TableName ?? `Bàn ${rtId}`,
                            capacity: 4,
                            status: 'Trong',
                            areaName: realtimeData.areaName ?? realtimeData.AreaName ?? 'Tầng 1 - Khu A'
                        }
                    ];
                }
            }

            cachedAvailableTables = effectiveTables;
            tablesLoaded = true;

            // Kiểm tra xem bàn đã chọn trước đó có còn trống không
            const isPreviousStillAvailable = previousTable && effectiveTables.some(t => String(t.id) === String(previousTable));

            if (previousTable && !isPreviousStillAvailable) {
                // Bàn đang chọn vừa chuyển sang trạng thái bận!
                selectedTableId = '';
                tableSelect.value = '';

                let busyName = `Bàn #${previousTable}`;
                if (realtimeData && (String(realtimeData.tableId) === String(previousTable) || String(realtimeData.TableId) === String(previousTable))) {
                    busyName = realtimeData.tableName || realtimeData.TableName || busyName;
                }

                renderCheckoutTableAlert(
                    tableAlertContainer,
                    `${busyName} vừa được sử dụng hoặc chuyển sang trạng thái bận. Vui lòng chọn bàn trống khác.`
                );
                setStatus(`${busyName} vừa chuyển sang trạng thái bận. Vui lòng chọn bàn khác.`, true);
            } else if (isPreviousStillAvailable) {
                selectedTableId = String(previousTable);
                tableSelect.value = String(previousTable);
            } else {
                selectedTableId = '';
                tableSelect.value = '';
            }

            renderCheckoutTables(tableSelect, effectiveTables, {
                gridContainer: tableGridContainer,
                countBadge: tableCountBadge,
                selectedTableId,
                activeAreaFilter,
                onSelectTable: handleTableSelection,
                onSelectArea: handleAreaFilter
            });

            if (effectiveTables.length === 0) {
                renderCheckoutMessage(authState, 'Hiện chưa có bàn trống khả dụng. Vui lòng thử lại sau.', { type: 'warning' });
            }
        } catch (error) {
            tablesLoaded = false;
            selectedTableId = '';
            tableSelect.replaceChildren(new Option('Không thể tải danh sách bàn', ''));
            tableSelect.disabled = true;
            if (tableGridContainer) {
                tableGridContainer.innerHTML = `
                    <div class="rounded-xl border border-red-200 bg-red-50 p-4 text-xs md:text-sm text-red-800 text-center">
                        <p class="font-bold">Không thể tải danh sách bàn trống.</p>
                        <p class="mt-1">${error?.message || 'Lỗi kết nối đến máy chủ.'}</p>
                    </div>
                `;
            }
            setStatus(error?.message || 'Không thể tải danh sách bàn.', true);
        }
        updateSubmit();
    }

    function getSubmissionRequestId(fingerprint) {
        try {
            const serialized = sessionStorage.getItem(PENDING_REQUEST_KEY);
            if (serialized) {
                try {
                    savedRequest = JSON.parse(serialized);
                } catch {
                    throw new Error('Không thể xác minh yêu cầu checkout đang chờ. Hãy giữ nguyên giỏ hàng và liên hệ nhân viên để kiểm tra đơn trước khi gửi lại.');
                }
            }
        } catch (error) {
            if (error instanceof Error && error.message.startsWith('Không thể xác minh')) throw error;
            throw new Error('Trình duyệt không thể lưu mã chống gửi đơn trùng. Hãy bật lưu trữ phiên rồi thử lại.');
        }

        if (savedRequest) {
            if (savedRequest.fingerprint !== fingerprint || typeof savedRequest.requestId !== 'string') {
                throw new Error('Yêu cầu cũ cần được đối soát trước khi gửi giỏ hàng đã thay đổi.');
            }
            return savedRequest.requestId;
        }

        const cryptoApi = globalThis.crypto;
        if (!cryptoApi?.randomUUID) {
            throw new Error('Trình duyệt này không hỗ trợ tạo mã yêu cầu an toàn nên chưa thể gửi đơn.');
        }

        const requestId = cryptoApi.randomUUID();
        try {
            savedRequest = { fingerprint, requestId };
            sessionStorage.setItem(PENDING_REQUEST_KEY, JSON.stringify(savedRequest));
        } catch {
            savedRequest = null;
            throw new Error('Trình duyệt không thể lưu mã chống gửi đơn trùng. Hãy bật lưu trữ phiên rồi thử lại.');
        }
        uncertainSubmission = true;
        return requestId;
    }

    function clearPendingRequest() {
        try {
            sessionStorage.removeItem(PENDING_REQUEST_KEY);
            return true;
        } catch {
            return false;
        }
    }

    function renderRecoveryPrompt(message, actions = []) {
        renderCheckoutRecovery(recoveryElement, message, actions.map((action) => ({
            ...action,
            disabled: pending || action.disabled === true
        })));
    }

    function isValidRequestId(requestId) {
        return typeof requestId === 'string' &&
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(requestId);
    }

    function getSavedRequestBody(request) {
        if (!request || !isValidRequestId(request.requestId) ||
            typeof request.fingerprint !== 'string') {
            throw new Error('Dữ liệu yêu cầu cũ không đầy đủ; không thể tạo yêu cầu thay thế một cách an toàn.');
        }

        let body;
        try {
            body = JSON.parse(request.fingerprint);
        } catch {
            throw new Error('Không thể khôi phục chính xác nội dung yêu cầu cũ; không gửi đơn mới.');
        }

        if (!Number.isSafeInteger(body?.tableId) || body.tableId <= 0 ||
            !Array.isArray(body.items) || body.items.length === 0 ||
            body.items.some((item) =>
                !Number.isSafeInteger(item?.menuItemId) || item.menuItemId <= 0 ||
                !Number.isInteger(item.quantity) || item.quantity < 1 || item.quantity > 99 ||
                (item.note !== null && typeof item.note !== 'string'))) {
            throw new Error('Nội dung yêu cầu cũ không hợp lệ; không gửi đơn mới.');
        }

        return body;
    }

    function validateOrderResponse(result, fallbackBody = null) {
        const orderId = Number(result?.orderId ?? result?.OrderId);
        const orderStatus = result?.status ?? result?.Status;
        const totalAmount = Number(result?.totalAmount ?? result?.TotalAmount);
        const fallbackItems = Array.isArray(fallbackBody?.items) ? fallbackBody.items : [];
        const items = Array.isArray(result?.items)
            ? result.items.map((item) => ({
                menuItemId: Number(item?.menuItemId ?? item?.MenuItemId),
                menuItemName: item?.menuItemName ?? item?.MenuItemName ?? 'Món ăn',
                quantity: Number(item?.quantity ?? item?.Quantity),
                unitPrice: Number(item?.unitPrice ?? item?.UnitPrice),
                note: item?.note ?? item?.Note ?? null
            }))
            : fallbackItems.map((item) => ({
                menuItemId: item.menuItemId,
                menuItemName: `Món #${item.menuItemId}`,
                quantity: item.quantity,
                unitPrice: null,
                note: item.note
            }));
        const tableId = Number(result?.tableId ?? result?.TableId ?? fallbackBody?.tableId);
        const tableName = result?.tableName ?? result?.TableName ?? `Bàn #${tableId}`;
        if (!Number.isSafeInteger(orderId) || orderId <= 0 ||
            !['DangMo', 'DaThanhToan', 'DaHuy'].includes(orderStatus) ||
            !Number.isSafeInteger(tableId) || tableId <= 0 ||
            items.length === 0 ||
            items.some((item) => !Number.isSafeInteger(item.menuItemId) || item.menuItemId <= 0 ||
                !Number.isInteger(item.quantity) || item.quantity < 1 ||
                (item.unitPrice !== null && (!Number.isFinite(item.unitPrice) || item.unitPrice < 0))) ||
            !Number.isFinite(totalAmount) || totalAmount < 0) {
            throw new Error('Máy chủ chưa trả về đủ thông tin xác nhận đơn; yêu cầu vẫn được giữ để đối soát.');
        }
        return { orderId, orderStatus, totalAmount, tableId, tableName, items };
    }

    function archiveResolvedRequest(request, resolution) {
        try {
            const stored = localStorage.getItem('gusto_checkout_request_history');
            const history = stored ? JSON.parse(stored) : [];
            if (!Array.isArray(history)) {
                throw new Error('Kho lưu yêu cầu đã xử lý không hợp lệ.');
            }
            const entry = {
                requestId: request.requestId,
                fingerprint: request.fingerprint,
                resolution,
                resolvedAt: new Date().toISOString(),
                ...(resolution.order ? { order: resolution.order } : {})
            };
            const existingIndex = history.findIndex((item) => item?.requestId === request.requestId);
            if (existingIndex >= 0) history[existingIndex] = entry;
            else history.push(entry);
            localStorage.setItem('gusto_checkout_request_history', JSON.stringify(history));
        } catch (error) {
            setStatus(error?.message || 'Không thể lưu lại mã yêu cầu cũ để đối soát.', true);
            return false;
        }

        if (!clearPendingRequest()) {
            setStatus('Đã lưu mã yêu cầu cũ nhưng không thể cập nhật trạng thái phiên. Không gửi đơn khác.', true);
            return false;
        }
        savedRequest = null;
        uncertainSubmission = false;
        recoveryElement?.replaceChildren();
        updateSubmit();
        return true;
    }

    function showRecoveredOrder(order) {
        const itemSummary = order.items.map((item) => {
            const note = item.note ? ` (ghi chú: ${item.note})` : '';
            const price = item.unitPrice === null ? '' : ` · ${new Intl.NumberFormat('vi-VN').format(item.unitPrice)}đ/phần`;
            return `${item.menuItemName} × ${item.quantity}${price}${note}`;
        }).join('; ');
        const orderMessage = `Đơn #${order.orderId} · ${order.orderStatus} · ${order.tableName} · ${new Intl.NumberFormat('vi-VN').format(order.totalAmount)}đ. Món: ${itemSummary}`;
        setStatus(orderMessage);
        renderRecoveryPrompt(
            `${orderMessage} Giỏ hàng và bàn hiện tại chưa bị thay đổi. Chỉ tiếp tục nếu thực sự muốn gửi yêu cầu khác.`,
            [{
                label: 'Đã đối chiếu, tiếp tục với giỏ hàng hiện tại',
                onClick: () => {
                    if (archiveResolvedRequest(savedRequest, { type: 'created', order })) {
                        setStatus('Đơn cũ đã được lưu để tra cứu. Giỏ hàng hiện tại vẫn được giữ nguyên.');
                    }
                }
            }]
        );
    }

    async function checkSavedRequest() {
        if (pending || !savedRequest) return;
        pending = true;
        updateSubmit();
        setStatus('Đang kiểm tra trạng thái bằng mã yêu cầu cũ...');
        renderRecoveryPrompt('Đang tra cứu đơn hàng đã ghi nhận...', []);

        try {
            const result = await orderApi.getCustomerOrderByRequestId(savedRequest.requestId);
            showRecoveredOrder(validateOrderResponse(result));
        } catch (error) {
            if (error?.status === 404 && error?.code === 'ORDER_NOT_FOUND') {
                setStatus('Chưa thấy đơn đã ghi nhận. Kết quả này không loại trừ yêu cầu cũ vẫn đang tới máy chủ; chỉ thử lại đúng nội dung và mã cũ.', true);
                renderRecoveryPrompt(
                    'Không tạo mã mới. Có thể gửi lại an toàn cùng mã yêu cầu và nội dung cũ; backend sẽ đối chiếu idempotency trước khi tạo đơn.',
                    [
                        { label: 'Kiểm tra lại', onClick: checkSavedRequest },
                        { label: 'Thử lại cùng yêu cầu cũ', onClick: retrySavedRequest }
                    ]
                );
            } else {
                setStatus(error?.message || 'Không thể tra cứu yêu cầu cũ. Không gửi đơn khác.', true);
                renderRecoveryPrompt('Trạng thái vẫn chưa xác định. Giữ nguyên giỏ hàng và mã yêu cầu; có thể thử tra cứu lại.', [
                    { label: 'Thử tra cứu lại', onClick: checkSavedRequest }
                ]);
            }
        } finally {
            pending = false;
            updateSubmit();
            refreshRecoveryButtons();
        }
    }

    function refreshRecoveryButtons() {
        const buttons = recoveryElement?.querySelectorAll('button') ?? [];
        buttons.forEach((button) => { button.disabled = pending; });
    }

    async function retrySavedRequest() {
        if (pending || !savedRequest) return;
        let body;
        try {
            body = getSavedRequestBody(savedRequest);
        } catch (error) {
            setStatus(error.message, true);
            return;
        }

        pending = true;
        updateSubmit();
        setStatus('Đang gửi lại đúng yêu cầu cũ với cùng mã chống trùng...');
        renderRecoveryPrompt('Đang đối soát yêu cầu cũ; không gửi giỏ hàng hiện tại.', []);
        try {
            const result = await orderApi.createCustomerOrder({
                ...body,
                clientRequestId: savedRequest.requestId
            });
            showRecoveredOrder(validateOrderResponse(result, body));
        } catch (error) {
            if (error?.status === 409) {
                // Nếu bàn vừa bị chiếm ngay lúc bấm tạo đơn, tải lại danh sách bàn ngay
                void loadTables();
            }
            const definitivelyRejected =
                error?.status === 400 ||
                error?.status === 404 ||
                (error?.status === 409 && error?.code !== 'IDEMPOTENCY_KEY_REUSED');
            if (definitivelyRejected && archiveResolvedRequest(savedRequest, { type: 'rejected' })) {
                setStatus(`Yêu cầu cũ bị từ chối trước khi tạo đơn. Giỏ hàng hiện tại được giữ nguyên; bạn có thể gửi lại sau khi kiểm tra. ${error.message}`, true);
            } else {
                setStatus(error?.message || 'Chưa xác định được kết quả yêu cầu cũ. Không gửi đơn khác.', true);
                renderRecoveryPrompt('Kết quả vẫn chưa xác định. Tra cứu lại hoặc thử lại cùng mã và nội dung cũ; không tạo mã mới.', [
                    { label: 'Kiểm tra trạng thái', onClick: checkSavedRequest },
                    { label: 'Thử lại cùng yêu cầu cũ', onClick: retrySavedRequest }
                ]);
            }
        } finally {
            pending = false;
            updateSubmit();
            refreshRecoveryButtons();
        }
    }

    tableSelect.addEventListener('change', () => {
        setStatus('');
        updateSubmit();
    });

    updateCart();

    // Phân biệt rõ các trạng thái xác thực và phân quyền
    if (!hasCustomerSession()) {
        checkoutForm.classList.add('hidden');
        authState.classList.remove('hidden');
        renderCheckoutMessage(
            authState,
            'Bạn cần đăng nhập tài khoản khách hàng để tiếp tục đặt món.',
            {
                loginHref: '/pages/auth/login_register.html?returnUrl=%2Fpages%2Fcheckout%2Findex.html',
                type: 'info'
            }
        );
        return;
    }

    if (isTokenExpired()) {
        sessionExpired = true;
        checkoutForm.classList.add('hidden');
        authState.classList.remove('hidden');
        renderCheckoutMessage(
            authState,
            'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.',
            {
                loginHref: '/pages/auth/login_register.html?returnUrl=%2Fpages%2Fcheckout%2Findex.html',
                type: 'warning'
            }
        );
        return;
    }

    if (!hasCustomerRole()) {
        checkoutForm.classList.add('hidden');
        authState.classList.remove('hidden');
        renderCheckoutMessage(
            authState,
            'Tài khoản hiện tại không có quyền đặt món với vai trò khách hàng. Vui lòng đăng nhập bằng tài khoản khách hàng.',
            {
                loginHref: '/pages/auth/login_register.html?returnUrl=%2Fpages%2Fcheckout%2Findex.html',
                type: 'error'
            }
        );
        return;
    }

    authState.classList.add('hidden');
    checkoutForm.classList.remove('hidden');
    renderCheckoutMessage(customerElement, 'Đang tải thông tin khách hàng...');
    await Promise.allSettled([loadCustomer(), loadTables(), refreshCatalogPrices()]);

    customSubmitButton?.addEventListener('click', () => {
        if (!canSubmit()) return;
        submitButton.click();
    });

    // -----------------------------------------------------
    // ĐOẠN ĐƯỢC CẬP NHẬT ĐỂ ĐỒNG BỘ VỚI BACKEND
    // -----------------------------------------------------
    checkoutForm.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (pending) return;
        if (uncertainSubmission) {
            setStatus('Yêu cầu trước chưa được đối soát. Hãy kiểm tra hoặc xử lý an toàn yêu cầu cũ trước khi gửi giỏ hàng hiện tại.', true);
            return;
        }
        if (!hasCustomerSession() || isTokenExpired()) {
            handleUnauthorized();
            return;
        }
        if (!hasCustomerRole()) {
            setStatus('Tài khoản hiện tại không có quyền đặt món với vai trò khách hàng.', true);
            return;
        }
        if (cart.length === 0) {
            setStatus('Giỏ hàng đang trống.', true);
            updateCart();
            return;
        }
        if (!tableSelect.value) {
            setStatus('Vui lòng chọn bàn trước khi đặt món.', true);
            tableSelect.focus();
            return;
        }
        if (!checkoutForm.reportValidity()) return;

        pending = true;
        updateSubmit();
        setSubmitButton('Đang gửi đơn...', 'progress_activity');
        customSubmitButton?.querySelector('.material-symbols-outlined')?.classList.add('animate-spin');
        setStatus('Đang xác minh thực đơn và gửi yêu cầu đặt món...');

        try {
            if (!await refreshCatalogPrices()) {
                setStatus('Có món không còn bán hoặc chưa xác minh được giá. Hãy kiểm tra giỏ hàng trước khi đặt.', true);
                return;
            }

            // CHỈ GỬI LÊN TABLE ID VÀ DANH SÁCH MÓN
            const requestBody = {
                tableId: Number(tableSelect.value),
                items: cart.map((item) => ({
                    menuItemId: item.id,
                    quantity: item.quantity,
                    note: item.note.trim() || null
                }))
            };
            
            // TẠO MÃ CHỐNG TRÙNG ĐƠN HÀNG (Idempotency Key)
            const fingerprint = JSON.stringify(requestBody);
            requestBody.clientRequestId = getSubmissionRequestId(fingerprint);

            // GỌI API ĐẶT MÓN
            const result = await orderApi.createCustomerOrder(requestBody);
            
            // THÀNH CÔNG: Hiển thị thông báo và điều hướng
            setStatus(`Đơn hàng #${result.orderId || result.OrderId} đã được tạo và gửi xuống bếp thành công!`);
            setSubmitButton('Hoàn tất đặt món', 'check_circle');
            
            renderCheckoutMessage(
                cartElement,
                `Đơn hàng #${result.orderId || result.OrderId} đã được chuyển đến bộ phận bếp. Vui lòng thanh toán tại quầy khi dùng bữa xong.`,
                { type: 'success' }
            );

            // Xóa giỏ hàng sau khi đặt thành công
            writeCart([]);
            cart = [];
            clearPendingRequest();

            // Tự động chuyển hướng về trang lịch sử đơn hàng sau 2 giây
            setTimeout(() => {
                window.location.href = '../profile/index.html#orders';
            }, 2000);

        } catch (error) {
            if (error?.status === 409) {
                // Nếu bàn vừa bị chiếm ngay lúc bấm tạo đơn, tải lại danh sách bàn ngay
                void loadTables();
            }
            const definitivelyRejected =
                error?.status === 400 ||
                error?.status === 404 ||
                (error?.status === 409 && error?.code !== 'IDEMPOTENCY_KEY_REUSED');
            if (savedRequest && definitivelyRejected) {
                if (archiveResolvedRequest(savedRequest, { type: 'rejected' })) {
                    setStatus(`Yêu cầu bị từ chối trước khi tạo đơn. Giỏ hàng vẫn được giữ lại. ${error.message}`, true);
                }
            } else {
                setStatus(normalizeOrderError(error), true);
                if (savedRequest) {
                    renderRecoveryPrompt('Kết quả yêu cầu cũ chưa xác định. Tra cứu trước hoặc thử lại đúng mã và nội dung cũ.', [
                        { label: 'Kiểm tra trạng thái', onClick: checkSavedRequest },
                        { label: 'Thử lại cùng yêu cầu cũ', onClick: retrySavedRequest }
                    ]);
                }
            }
            setSubmitButton('Thử lại cùng yêu cầu', 'send');
        } finally {
            pending = false;
            customSubmitButton?.querySelector('.material-symbols-outlined')?.classList.remove('animate-spin');
            updateSubmit();
        }
    });

    function normalizeOrderError(error) {
        const message = error?.message || '';
        if (/network|failed to fetch|fetch/i.test(message)) {
            return 'Không thể kết nối đến hệ thống nhà hàng. Giỏ hàng được giữ lại; nếu thử lại, hệ thống sẽ dùng cùng mã yêu cầu để tránh tạo đơn trùng.';
        }
        if (/401|unauthorized|unauthenticated/i.test(message)) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
        if (/404|not found|không tìm thấy/i.test(message)) return message || 'Không tìm thấy món hoặc bàn. Vui lòng kiểm tra lại.';
        if (/409|conflict|không còn khả dụng|đã được sử dụng/i.test(message)) return message;
        return message || 'Không thể tạo đơn. Giỏ hàng vẫn được giữ lại.';
    }

    realtimeClient.on('TableStatusUpdated', (data) => {
        if (!pending && !sessionExpired) loadTables(data);
    });
    realtimeClient.on('MenuItemUpdated', () => {
        if (!pending && !sessionExpired) refreshCatalogPrices();
    });
    realtimeClient.on('DishStatusChanged', () => {
        if (!pending && !sessionExpired) refreshCatalogPrices();
    });
    realtimeClient.on('CustomerProfileUpdated', () => {
        if (!pending && !sessionExpired) loadCustomer();
    });
    realtimeClient.on('sync', () => {
        if (!pending && !sessionExpired) {
            loadTables();
            refreshCatalogPrices();
            loadCustomer();
        }
    });
    realtimeClient.start().catch(() => {});
}

initializeCheckout();