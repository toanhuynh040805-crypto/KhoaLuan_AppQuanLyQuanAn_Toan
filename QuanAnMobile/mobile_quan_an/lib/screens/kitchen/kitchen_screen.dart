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

  Timer? _pollTimer;
  StreamSubscription? _dishStatusSubscription;
  StreamSubscription? _kitchenQueueSubscription;
  bool _loading = true;
  String? _error;
  int? _savingItemId;

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

      // Cùng nguồn dữ liệu với WinForms: /api/orderitems?departmentId=StationId.
      // Backend tiếp tục kiểm tra StationId của tài khoản, nên Bếp món không thể
      // đọc/xử lý hàng đợi Quầy nước và ngược lại.
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
              tableId: row.tableId,
              tableName: row.tableName.isEmpty
                  ? 'Bàn #${row.tableId}'
                  : row.tableName,
              item: row.item,
            ),
          )
          .toList();

      loaded.sort((a, b) {
        final bool aPriority = priorityItemIds.contains(a.item.id) ||
            priorityTableIds.contains(a.tableId);
        final bool bPriority = priorityItemIds.contains(b.item.id) ||
            priorityTableIds.contains(b.tableId);
        if (aPriority != bPriority) return aPriority ? -1 : 1;

        final int statusCompare =
            _statusPriority(a.item.status).compareTo(
          _statusPriority(b.item.status),
        );
        if (statusCompare != 0) return statusCompare;
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

  int _statusPriority(String status) {
    switch (status) {
      case 'Chờ chế biến':
        return 0;
      case 'Đang chế biến':
        return 1;
      case 'Đã xong':
        return 2;
      default:
        return 3;
    }
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'Chờ chế biến':
        return Colors.grey.shade700;
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

    setState(() {
      _savingItemId = row.item.id;
    });

    try {
      await AppServices.orderService.updateItemStatus(
        orderItemId: row.item.id!,
        status: nextStatus,
      );
      if (nextStatus == 'Đã xong') {
        try {
          await AppServices.referenceOpsService.setItemPriority(
            orderItemId: row.item.id!,
            employeeCode: appEmployee.employeeCode,
            active: false,
          );
        } catch (_) {
          // Không để việc dọn cờ ưu tiên chặn thao tác hoàn thành món.
        }
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
        setState(() {
          _savingItemId = null;
        });
      }
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: Text(
          '$_stationDisplayName - món cần xử lý',
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
                  child: _items.isEmpty
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
                                'Hiện không có món chờ ${_stationDisplayName.toLowerCase()}',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ),
                          ],
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.all(14),
                          itemCount: _items.length,
                          separatorBuilder: (_, _) =>
                              const SizedBox(height: 10),
                          itemBuilder: (context, index) =>
                              _buildItem(_items[index]),
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

  Widget _buildItem(_KitchenItem row) {
    final Color color = _statusColor(row.item.status);
    final bool saving = _savingItemId == row.item.id;
    final bool canAdvance = row.item.status == 'Chờ chế biến' ||
        row.item.status == 'Đang chế biến';
    final bool itemPriority =
        row.item.id != null && _priorityItemIds.contains(row.item.id);
    final bool tablePriority = _priorityTableIds.contains(row.tableId);

    return Container(
      padding: const EdgeInsets.all(15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  row.tableName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 17,
                  ),
                ),
              ),
              if (itemPriority || tablePriority) ...[
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
                  decoration: BoxDecoration(
                    color: Colors.redAccent.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Text(
                    itemPriority ? 'Ưu tiên món' : 'Ưu tiên bàn',
                    style: const TextStyle(
                      color: Colors.redAccent,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: 6),
              ],
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 9,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Text(
                  row.item.status,
                  style: TextStyle(
                    color: color,
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            row.item.name,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Số lượng: ${row.item.quantity}',
            style: TextStyle(color: Colors.grey.shade700),
          ),
          // Món khách yêu cầu mang về: bếp/quầy nước đóng hộp
          if (row.item.isTakeaway) ...[
            const SizedBox(height: 6),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: AppTheme.orangeColor.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Text(
                'MANG VỀ',
                style: TextStyle(
                  color: AppTheme.orangeColor,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ],
          if ((row.item.note ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 5),
            Text(
              'Ghi chú: ${row.item.note}',
              style: TextStyle(
                color: Colors.grey.shade700,
                fontStyle: FontStyle.italic,
              ),
            ),
          ],
          if (canAdvance) ...[
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 44,
              child: FilledButton.icon(
                onPressed: saving ? null : () => _advance(row),
                style: FilledButton.styleFrom(
                  backgroundColor: row.item.status == 'Đang chế biến'
                      ? Colors.green
                      : AppTheme.orangeColor,
                ),
                icon: saving
                    ? const SizedBox(
                        width: 17,
                        height: 17,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Icon(
                        row.item.status == 'Đang chế biến'
                            ? Icons.check
                            : Icons.play_arrow,
                      ),
                label: Text(
                  row.item.status == 'Đang chế biến'
                      ? 'Hoàn thành món'
                      : 'Bắt đầu chế biến',
                ),
              ),
            ),
          ] else ...[
            const SizedBox(height: 10),
            const Text(
              'Đã xong - chờ nhân viên phục vụ mang món ra bàn.',
              style: TextStyle(
                color: Colors.green,
                fontWeight: FontWeight.w600,
                fontSize: 12,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _KitchenItem {
  final int tableId;
  final String tableName;
  final OrderItem item;

  const _KitchenItem({
    required this.tableId,
    required this.tableName,
    required this.item,
  });
}