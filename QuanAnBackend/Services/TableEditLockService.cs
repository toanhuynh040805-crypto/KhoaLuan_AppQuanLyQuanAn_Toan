namespace QuanAnBackend.Services;

public sealed record TableEditLockSnapshot(
    int TableId,
    string EmployeeCode,
    string EmployeeName,
    string ClientToken,
    DateTimeOffset ExpiresAtUtc
);

public sealed class TableEditLockService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(45);
    private readonly object _gate = new();
    private readonly Dictionary<int, TableEditLockSnapshot> _locks = new();

    public bool TryAcquire(
        int tableId,
        string employeeCode,
        string employeeName,
        string clientToken,
        out TableEditLockSnapshot lease,
        out TableEditLockSnapshot? conflictingLease)
    {
        lock (_gate)
        {
            CleanupExpiredUnsafe();

            // Phiên vào sau luôn được quyền tiếp quản bàn ngay lập tức.
            // Giữ lại lease cũ để controller phát realtime cho thiết bị/tài
            // khoản đang giữ bàn biết rằng phiên của họ đã bị thay thế.
            conflictingLease =
                _locks.TryGetValue(tableId, out var current) &&
                !string.Equals(current.ClientToken, clientToken, StringComparison.Ordinal)
                    ? current
                    : null;

            lease = new TableEditLockSnapshot(
                tableId,
                employeeCode,
                employeeName,
                clientToken,
                DateTimeOffset.UtcNow.Add(LeaseDuration)
            );

            _locks[tableId] = lease;
            return true;
        }
    }

    public bool TryHeartbeat(
        int tableId,
        string clientToken,
        out TableEditLockSnapshot? lease,
        out TableEditLockSnapshot? conflictingLease)
    {
        lock (_gate)
        {
            CleanupExpiredUnsafe();

            if (!_locks.TryGetValue(tableId, out var current))
            {
                lease = null;
                conflictingLease = null;
                return false;
            }

            if (!string.Equals(current.ClientToken, clientToken, StringComparison.Ordinal))
            {
                lease = null;
                conflictingLease = current;
                return false;
            }

            lease = current with
            {
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(LeaseDuration)
            };
            _locks[tableId] = lease;
            conflictingLease = null;
            return true;
        }
    }

    public bool Release(int tableId, string clientToken)
    {
        lock (_gate)
        {
            CleanupExpiredUnsafe();

            if (!_locks.TryGetValue(tableId, out var current))
                return true;

            if (!string.Equals(current.ClientToken, clientToken, StringComparison.Ordinal))
                return false;

            _locks.Remove(tableId);
            return true;
        }
    }

    public TableEditLockSnapshot? GetActive(int tableId)
    {
        lock (_gate)
        {
            CleanupExpiredUnsafe();
            return _locks.TryGetValue(tableId, out var current) ? current : null;
        }
    }

    private void CleanupExpiredUnsafe()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _locks
            .Where(pair => pair.Value.ExpiresAtUtc <= now)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var tableId in expired)
            _locks.Remove(tableId);
    }
}
