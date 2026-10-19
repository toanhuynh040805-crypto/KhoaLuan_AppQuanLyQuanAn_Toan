import { apiClient } from './client.js?auth-session-2';

const tableStatuses = ['Trong', 'DangPhucVu', 'DaDat', 'DangDonDep'];

function normalizeTable(table) {
    const id = table.Id ?? table.id ?? table.TableId ?? table.tableId;
    const name = table.TableName ?? table.tableName ?? table.Name ?? table.name;
    if ((typeof id !== 'number' && typeof id !== 'string') || typeof name !== 'string') {
        throw new TypeError('Table response contains invalid fields.');
    }
    const rawStatus = table.Status ?? table.status;
    const status = typeof rawStatus === 'number'
        ? (tableStatuses[rawStatus] ?? 'Unknown')
        : (typeof rawStatus === 'string' ? rawStatus : 'Unknown');
    return {
        id,
        name,
        capacity: Number(table.Capacity ?? table.capacity ?? 0),
        status,
        areaName: typeof (table.AreaName ?? table.areaName) === 'string' ? (table.AreaName ?? table.areaName) : ''
    };
}

export const tableApi = {
    async getTables() {
        // Khách dùng endpoint riêng (API /tables là của nhân viên, bị chặn với token khách)
        const response = await apiClient.get('/orders/customer-order/tables');
        if (!Array.isArray(response)) throw new TypeError('Table response must be an array.');
        return response.map(normalizeTable).filter((table) => table.id != null);
    }
};