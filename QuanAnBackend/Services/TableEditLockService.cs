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
        bool forceTakeover,
        out TableEditLockSnapshot lease,
        out TableEditLockSnapshot? conflictingLease)
    {
        lock (_gate)
        {
            CleanupExpiredUnsafe();

            if (_locks.TryGetValue(tableId, out var current))
            {
                // Cùng một client gọi acquire lại thì chỉ gia hạn lease, không
                // coi là xung đột và cũng không phát sự kiện takeover.
                if (string.Equals(
                        current.ClientToken,
                        clientToken,
                        StringComparison.Ordinal))
                {
                    lease = current with
                    {
                        EmployeeCode = employeeCode,
                        EmployeeName = employeeName,
                        ExpiresAtUtc = DateTimeOffset.UtcNow.Add(LeaseDuration)
                    };
                    _locks[tableId] = lease;
                    conflictingLease = null;
                    return true;
                }

                // Có người khác đang giữ bàn: lần gọi đầu chỉ trả conflict để
                // client hiển thị xác nhận. Chỉ khi người dùng đồng ý và gọi
                // lại với forceTakeover=true mới thay lease cũ.
                if (!forceTakeover)
                {
                    lease = current;
                    conflictingLease = current;
                    return false;
                }

                conflictingLease = current;
            }
            else
            {
                conflictingLease = null;
            }

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
