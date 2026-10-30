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
    async getTables({ status, areaId } = {}) {
        const query = new URLSearchParams();
        if (status) query.set('status', status);
        if (areaId != null && areaId !== '') query.set('areaId', String(areaId));
        const endpoint = query.toString() ? `/tables?${query}` : '/tables';
        const response = await apiClient.get(endpoint);
        if (!Array.isArray(response)) throw new TypeError('Table response must be an array.');
        return response.map(normalizeTable).filter((table) => table.id != null);
    },
    async getAvailableTables() {
        const tables = await this.getTables({ status: 'Trong' });
        // Lọc bảo đảm kép ở phía Client
        return tables.filter(t => t.status === 'Trong');
    }
};
