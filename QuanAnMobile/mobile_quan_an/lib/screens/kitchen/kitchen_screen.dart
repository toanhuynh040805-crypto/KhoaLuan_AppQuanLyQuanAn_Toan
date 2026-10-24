import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/employee_profile.dart';
import '../../models/order_item.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/mobile_access_policy.dart';
import '../../widgets/top_message.dart';
import '../login/login_screen.dart';

class KitchenScreen extends StatefulWidget {
  const KitchenScreen({super.key});

  @override
  State<KitchenScreen> createState() => _KitchenScreenState();
}

class _KitchenScreenState extends State<KitchenScreen>
    with WidgetsBindingObserver {
  final List<_KitchenItem> _items = <_KitchenItem>[];
  final Set<int> _priorityItemIds = <int>{};
  final Set<int> _priorityTableIds = <int>{};
  final Set<int> _savingItemIds = <int>{};

  Timer? _pollTimer;
  StreamSubscription? _dishStatusSubscription;
  StreamSubscription? _kitchenQueueSubscription;
  bool _loading = true;
  String? _error;
  String? _savingBatchKey;

  String get _stationDisplayName {
    final String station = appEmployee.stationName.trim().toLowerCase();
    return station == 'quaynuoc' || station == 'quầy nước'
        ? 'Quầy nước'
        : 'Bếp';
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    _startRealtime();
    _pollTimer = Timer.periodic(
      const Duration(seconds: 6),
      (_) => _load(silent: true),
    );
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _load(silent: true);
    }
  }

  Future<void> _startRealtime() async {
    _dishStatusSubscription = AppServices
        .realtimeService.dishStatusChanges
        .listen((_) => _load(silent: true));
    _kitchenQueueSubscription = AppServices
        .realtimeService.kitchenQueueChanges
        .listen((_) => _load(silent: true));

    try {
      await AppServices.realtimeService.start(
        employeeCode: appEmployee.employeeCode,
        roleKey: appEmployee.stationName.trim().isNotEmpty
            ? appEmployee.stationName.trim()
            : MobileAccessPolicy.backendRoleKey(appEmployee.role),
      );
    } catch (_) {
      // Polling 6 giây vẫn giữ màn bếp hoạt động nếu SignalR tạm mất kết nối.
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _pollTimer?.cancel();
    _dishStatusSubscription?.cancel();
    _kitchenQueueSubscription?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent && mounted) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }

    try {
      final int? stationId = appEmployee.stationId;
      if (stationId == null) {
        throw const ApiException(
          message: 'Tài khoản chế biến chưa được phân công Bếp hoặc Quầy nước',
        );
      }

      final queueItems = await AppServices.orderService.getKitchenItems(
        stationId: stationId,
      );

      Set<int> priorityItemIds = <int>{};
      Set<int> priorityTableIds = <int>{};
      try {
        final Map<String, Set<int>> priorities =
            await AppServices.referenceOpsService.getKitchenPriorities();
        priorityItemIds = priorities['itemIds'] ?? <int>{};
        priorityTableIds = priorities['tableIds'] ?? <int>{};
      } catch (_) {
        // Ưu tiên là lớp bổ sung; bếp vẫn hoạt động nếu endpoint tạm mất.
      }

      final List<_KitchenItem> loaded = queueItems
          .map(
            (row) => _KitchenItem(
              orderId: row.orderId,
              tableId: row.tableId,
              tableName: row.tableName.isEmpty
                  ? (row.tableId > 0 ? 'Bàn #${row.tableId}' : 'Đơn mang về')
                  : row.tableName,
              createdAt: row.createdAt,
              item: row.item,
            ),
          )
          .toList()
        ..sort((a, b) {
          final int timeCompare = a.createdAt.compareTo(b.createdAt);
          if (timeCompare != 0) return timeCompare;
          return (a.item.id ?? 0).compareTo(b.item.id ?? 0);
        });

      if (!mounted) return;
      setState(() {
        _items
          ..clear()
          ..addAll(loaded);
        _priorityItemIds
          ..clear()
          ..addAll(priorityItemIds);
        _priorityTableIds
          ..clear()
          ..addAll(priorityTableIds);
        _loading = false;
        _error = null;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'Không thể tải danh sách món bếp';
      });
    }
  }

  List<_KitchenBatch> _buildBatches() {
    final Map<String, _KitchenBatch> grouped = <String, _KitchenBatch>{};

    for (final _KitchenItem row in _items) {
      // Từ bản mới, toàn bộ món trong cùng một lần bấm "Gọi món" có CreatedAt
      // giống hệt nhau. Vì vậy cùng bàn gọi thêm lần sau sẽ luôn thành batch mới.
      final String key =
          '${row.orderId}:${row.tableId}:${row.createdAt.toIso8601String()}';

      grouped.putIfAbsent(
        key,
        () => _KitchenBatch(
          keyValue: key,
          orderId: row.orderId,
          tableId: row.tableId,
          tableName: row.tableName,
          createdAt: row.createdAt,
          items: <_KitchenItem>[],
        ),
      );
      grouped[key]!.items.add(row);
    }

    final List<_KitchenBatch> batches = grouped.values.toList();

    for (final _KitchenBatch batch in batches) {
      batch.items.sort(
        (a, b) => (a.item.id ?? 0).compareTo(b.item.id ?? 0),
      );
    }

    batches.sort((a, b) {
      // Cùng một bàn: LUÔN giữ đúng thứ tự gọi món cũ trước, mới sau.
      if (a.tableId == b.tableId) {
        final int timeCompare = a.createdAt.compareTo(b.createdAt);
        if (timeCompare != 0) return timeCompare;
        return a.keyValue.compareTo(b.keyValue);
      }

      // Khác bàn: vẫn giữ nghiệp vụ ưu tiên đã có.
      final bool aPriority = _batchIsPriority(a);
      final bool bPriority = _batchIsPriority(b);
      if (aPriority != bPriority) return aPriority ? -1 : 1;

      final int timeCompare = a.createdAt.compareTo(b.createdAt);
      if (timeCompare != 0) return timeCompare;
      return a.tableName.compareTo(b.tableName);
    });

    return batches;
  }

  bool _batchIsPriority(_KitchenBatch batch) {
    if (_priorityTableIds.contains(batch.tableId)) return true;
    return batch.items.any(
      (row) => row.item.id != null && _priorityItemIds.contains(row.item.id),
    );
  }

  String _batchStatus(_KitchenBatch batch) {
    final bool hasWaiting =
        batch.items.any((row) => row.item.status == 'Chờ chế biến');
    final bool hasCooking =
        batch.items.any((row) => row.item.status == 'Đang chế biến');

    if (hasWaiting && hasCooking) return 'Đang xử lý';
    if (hasWaiting) return 'Chờ chế biến';
    if (hasCooking) return 'Đang chế biến';
    return 'Đã xong';
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'Chờ chế biến':
        return Colors.grey.shade700;
      case 'Đang xử lý':
      case 'Đang chế biến':
        return AppTheme.orangeColor;
      case 'Đã xong':
        return Colors.green;
      default:
        return Colors.blueGrey;
    }
  }

  Future<void> _advance(_KitchenItem row) async {
    if (!MobileAccessPolicy.canCook(appEmployee.role) || row.item.id == null) {
      return;
    }

    final String? nextStatus = switch (row.item.status) {
      'Chờ chế biến' => 'Đang chế biến',
      'Đang chế biến' => 'Đã xong',
      _ => null,
    };

    if (nextStatus == null) return;

    final int itemId = row.item.id!;
    setState(() => _savingItemIds.add(itemId));

    try {
      await AppServices.orderService.updateItemStatus(
        orderItemId: itemId,
        status: nextStatus,
      );

      if (nextStatus == 'Đã xong') {
        await _clearItemPriority(itemId);
      }

      await _load(silent: true);

      if (!mounted) return;
      TopMessage.show(
        context,
        message: nextStatus == 'Đã xong'
            ? '${row.item.name} đã hoàn thành'
            : 'Đã bắt đầu chế biến ${row.item.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() => _savingItemIds.remove(itemId));
      }
    }
  }

  Future<void> _advanceBatch(_KitchenBatch batch) async {
    if (!MobileAccessPolicy.canCook(appEmployee.role) ||
        _savingBatchKey != null) {
      return;
    }

    final List<_KitchenItem> waiting = batch.items
        .where((row) => row.item.status == 'Chờ chế biến' && row.item.id != null)
        .toList();
    final List<_KitchenItem> cooking = batch.items
        .where((row) => row.item.status == 'Đang chế biến' && row.item.id != null)
        .toList();

    // Nếu còn món chờ, nút "tất cả" chỉ bắt đầu các món chờ.
    // Chỉ khi không còn món chờ mới cho hoàn thành toàn bộ món đang chế biến.
    final bool starting = waiting.isNotEmpty;
    final List<_KitchenItem> targets = starting ? waiting : cooking;
    if (targets.isEmpty) return;

    final Set<int> targetIds =
        targets.map((row) => row.item.id!).toSet();

    setState(() {
      _savingBatchKey = batch.keyValue;
      _savingItemIds.addAll(targetIds);
    });

    try {
      for (final _KitchenItem row in targets) {
        await AppServices.orderService.updateItemStatus(
          orderItemId: row.item.id!,
          status: starting ? 'Đang chế biến' : 'Đã xong',
        );

        if (!starting) {
          await _clearItemPriority(row.item.id!);
        }
      }

      await _load(silent: true);

      if (!mounted) return;
      TopMessage.show(
        context,
        message: starting
            ? 'Đã bắt đầu toàn bộ món của ${batch.tableName}'
            : 'Đã hoàn thành toàn bộ món của ${batch.tableName}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      // Có thể một phần món đã cập nhật trước khi món khác gặp concurrency.
      // Tải lại ngay để giao diện phản ánh đúng trạng thái thật từ Backend.
      await _load(silent: true);
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() {
          _savingBatchKey = null;
          _savingItemIds.removeAll(targetIds);
        });
      }
    }
  }

  Future<void> _clearItemPriority(int itemId) async {
    try {
      await AppServices.referenceOpsService.setItemPriority(
        orderItemId: itemId,
        employeeCode: appEmployee.employeeCode,
        active: false,
      );
    } catch (_) {
      // Không để việc dọn cờ ưu tiên chặn thao tác hoàn thành món.
    }
  }

  Future<void> _logout() async {
    _pollTimer?.cancel();
    await AppServices.realtimeService.stop();
    if (!mounted) return;

    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  String _timeText(DateTime value) {
    if (value.millisecondsSinceEpoch <= 0) return '--:--';
    final String hour = value.hour.toString().padLeft(2, '0');
    final String minute = value.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }

  @override
  Widget build(BuildContext context) {
    final List<_KitchenBatch> batches = _buildBatches();

    return Scaffold(
      backgroundColor: const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: Text(
          '$_stationDisplayName - phiếu cần xử lý',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            color: AppTheme.textColor,
            fontWeight: FontWeight.bold,
          ),
        ),
        actions: [
          IconButton(
            tooltip: 'Tải lại',
            onPressed: () => _load(),
            icon: const Icon(Icons.refresh),
          ),
          PopupMenuButton<String>(
            onSelected: (value) {
              if (value == 'logout') _logout();
            },
            itemBuilder: (_) => const [
              PopupMenuItem(
                value: 'logout',
                child: Text('Đăng xuất'),
              ),
            ],
          ),
        ],
      ),
      body: _loading
          ? const Center(
              child: CircularProgressIndicator(
                color: AppTheme.orangeColor,
              ),
            )
          : _error != null
              ? _buildError()
              : RefreshIndicator(
                  onRefresh: _load,
                  child: batches.isEmpty
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          children: [
                            const SizedBox(height: 180),
                            const Icon(
                              Icons.soup_kitchen_outlined,
                              size: 58,
                              color: Colors.grey,
                            ),
                            const SizedBox(height: 12),
                            Center(
                              child: Text(
                                'Hiện không có phiếu chờ ${_stationDisplayName.toLowerCase()}',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ),
                          ],
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.all(14),
                          itemCount: batches.length,
                          separatorBuilder: (_, _) =>
                              const SizedBox(height: 12),
                          itemBuilder: (context, index) =>
                              _buildBatchCard(batches[index]),
                        ),
                ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 44),
            const SizedBox(height: 10),
            Text(
              _error!,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: () => _load(),
              child: const Text('Thử lại'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBatchCard(_KitchenBatch batch) {
    final String batchStatus = _batchStatus(batch);
    final Color statusColor = _statusColor(batchStatus);
    final bool batchPriority = _batchIsPriority(batch);
    final bool savingBatch = _savingBatchKey == batch.keyValue;

    final bool hasWaiting =
        batch.items.any((row) => row.item.status == 'Chờ chế biến');
    final bool hasCooking =
        batch.items.any((row) => row.item.status == 'Đang chế biến');

    final String? bulkLabel = hasWaiting
        ? 'Bắt đầu chế biến tất cả'
        : hasCooking
            ? 'Xác nhận hoàn thành tất cả'
            : null;

    return Container(
      padding: const EdgeInsets.all(15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: batchPriority
              ? Colors.redAccent.withValues(alpha: 0.45)
              : Colors.grey.shade200,
          width: batchPriority ? 1.4 : 1,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      batch.tableName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      'Đợt gọi lúc ${_timeText(batch.createdAt)} • ${batch.items.length} món',
                      style: TextStyle(
                        color: Colors.grey.shade600,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 9,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Text(
                  batchStatus,
                  style: TextStyle(
                    color: statusColor,
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          if (batchPriority) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(
                horizontal: 8,
                vertical: 5,
              ),
              decoration: BoxDecoration(
                color: Colors.redAccent.withValues(alpha: 0.10),
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Text(
                'Ưu tiên',
                style: TextStyle(
                  color: Colors.redAccent,
                  fontSize: 10,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
          const SizedBox(height: 10),
          const Divider(height: 1),
          ...batch.items.map(
            (row) => _buildBatchItem(row, batchBusy: savingBatch),
          ),
          if (bulkLabel != null) ...[
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              height: 46,
              child: FilledButton.icon(
                onPressed: savingBatch
                    ? null
                    : () => _advanceBatch(batch),
                style: FilledButton.styleFrom(
                  backgroundColor: hasWaiting
                      ? AppTheme.orangeColor
                      : const Color(0xFF2563EB),
                  foregroundColor: Colors.white,
                ),
                icon: savingBatch
                    ? const SizedBox(
                        width: 17,
                        height: 17,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Icon(
                        hasWaiting
                            ? Icons.play_arrow_rounded
                            : Icons.outlined_flag,
                      ),
                label: Text(bulkLabel),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildBatchItem(
    _KitchenItem row, {
    required bool batchBusy,
  }) {
    final int? itemId = row.item.id;
    final bool saving =
        itemId != null && _savingItemIds.contains(itemId);
    final bool canAdvance = row.item.status == 'Chờ chế biến' ||
        row.item.status == 'Đang chế biến';
    final bool itemPriority =
        itemId != null && _priorityItemIds.contains(itemId);
    final Color statusColor = _statusColor(row.item.status);

    return Padding(
      padding: const EdgeInsets.only(top: 11),
      child: Container(
        padding: const EdgeInsets.all(11),
        decoration: BoxDecoration(
          color: const Color(0xFFF8F9FA),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: Colors.grey.shade200),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Text(
                    row.item.name,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  'x${row.item.quantity}',
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Text(
                    row.item.status,
                    style: TextStyle(
                      color: statusColor,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                if (itemPriority)
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.redAccent.withValues(alpha: 0.10),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Text(
                      'Ưu tiên món',
                      style: TextStyle(
                        color: Colors.redAccent,
                        fontSize: 10,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                if (row.item.isTakeaway)
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppTheme.orangeColor.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Text(
                      'MANG VỀ',
                      style: TextStyle(
                        color: AppTheme.orangeColor,
                        fontSize: 10,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
              ],
            ),
            if ((row.item.note ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                'Ghi chú: ${row.item.note}',
                style: TextStyle(
                  color: Colors.grey.shade700,
                  fontStyle: FontStyle.italic,
                  fontSize: 12,
                ),
              ),
            ],
            if (canAdvance) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: (saving || batchBusy)
                      ? null
                      : () => _advance(row),
                  icon: saving
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Icon(
                          row.item.status == 'Đang chế biến'
                              ? Icons.outlined_flag
                              : Icons.play_arrow_rounded,
                          size: 18,
                        ),
                  label: Text(
                    row.item.status == 'Đang chế biến'
                        ? 'Xác nhận món'
                        : 'Bắt đầu món',
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _KitchenItem {
  final int orderId;
  final int tableId;
  final String tableName;
  final DateTime createdAt;
  final OrderItem item;

  const _KitchenItem({
    required this.orderId,
    required this.tableId,
    required this.tableName,
    required this.createdAt,
    required this.item,
  });
}

class _KitchenBatch {
  final String keyValue;
  final int orderId;
  final int tableId;
  final String tableName;
  final DateTime createdAt;
  final List<_KitchenItem> items;

  const _KitchenBatch({
    required this.keyValue,
    required this.orderId,
    required this.tableId,
    required this.tableName,
    required this.createdAt,
    required this.items,
  });
}
