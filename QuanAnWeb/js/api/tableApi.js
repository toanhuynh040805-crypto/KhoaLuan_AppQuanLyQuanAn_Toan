import { apiClient } from './client.js?auth-session-2';

const tableStatuses = ['Trong', 'DangPhucVu', 'DaDat', 'DangDonDep'];

function normalizeStatus(rawStatus) {
    if (typeof rawStatus === 'number') {
        return tableStatuses[rawStatus] ?? 'Unknown';
    }

    if (typeof rawStatus === 'string') {
        const value = rawStatus.trim();
        if (/^\d+$/.test(value)) {
            return tableStatuses[Number(value)] ?? 'Unknown';
        }
        return value || 'Unknown';
    }

    return 'Unknown';
}

function normalizeTable(table) {
    const id = table.Id ?? table.id ?? table.TableId ?? table.tableId;
    const name = table.TableName ?? table.tableName ?? table.Name ?? table.name;

    if ((typeof id !== 'number' && typeof id !== 'string') || typeof name !== 'string') {
        throw new TypeError('Table response contains invalid fields.');
    }

    return {
        id: Number(id),
        name: name.trim(),
        capacity: Number(table.Capacity ?? table.capacity ?? 0),
        status: normalizeStatus(table.Status ?? table.status ?? table.CurrentStatus ?? table.currentStatus),
        areaId: Number(table.AreaId ?? table.areaId ?? 0),
        areaName: String(table.AreaName ?? table.areaName ?? ''),
        tableType: String(table.TableType ?? table.tableType ?? 'Thuong'),
        amount: table.Amount ?? table.amount ?? null,
        customerName: table.CustomerName ?? table.customerName ?? null,
        linkedOrderId: table.LinkedOrderId ?? table.linkedOrderId ?? null,
        isLinkPrimary: Boolean(table.IsLinkPrimary ?? table.isLinkPrimary ?? false),
        linkedWith: table.LinkedWith ?? table.linkedWith ?? null
    };
}

function statusPriority(status) {
    switch (normalizeStatus(status)) {
        case 'DangPhucVu': return 400;
        case 'DaDat': return 300;
        case 'DangDonDep': return 200;
        case 'Trong': return 100;
        default: return 0;
    }
}

function normalizeKey(table) {
    return `${Number(table.areaId ?? 0)}::${String(table.name ?? '').trim().toUpperCase()}`;
}

export function normalizeTableList(rawTables) {
    const normalized = (Array.isArray(rawTables) ? rawTables : [])
        .map(normalizeTable)
        .filter((table) => Number.isFinite(table.id) && table.id > 0 && table.name);

    const byPhysicalTable = new Map();

    normalized.forEach((table) => {
        const key = normalizeKey(table);
        const current = byPhysicalTable.get(key);

        if (!current ||
            statusPriority(table.status) > statusPriority(current.status) ||
            (statusPriority(table.status) === statusPriority(current.status) && table.id > current.id)) {
            byPhysicalTable.set(key, table);
        }
    });

    return Array.from(byPhysicalTable.values())
        .sort((a, b) =>
            a.areaName.localeCompare(b.areaName, 'vi') ||
            a.name.localeCompare(b.name, 'vi'));
}

export const tableApi = {
    async getTables({ status, areaId } = {}) {
        const query = new URLSearchParams();
        if (status) query.set('status', status);
        if (areaId != null && areaId !== '') query.set('areaId', String(areaId));

        const endpoint = query.toString() ? `/tables?${query}` : '/tables';
        const response = await apiClient.get(endpoint);
        const tables = normalizeTableList(response);

        return tables.filter((table) => {
            if (status && table.status !== normalizeStatus(status)) return false;
            if (areaId != null && areaId !== '' && table.areaId !== Number(areaId)) return false;
            return true;
        });
    },

    async getAvailableTables() {
        const tables = await this.getTables({ status: 'Trong' });
        return tables.filter((table) => table.status === 'Trong');
    }
};
