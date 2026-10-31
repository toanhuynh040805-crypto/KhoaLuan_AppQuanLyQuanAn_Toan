import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/app_notification.dart';
import '../../models/cart_item.dart';
import '../../models/employee_profile.dart';
import '../../models/notification_settings_state.dart';
import '../../models/order_item.dart';
import '../../models/restaurant_table.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/mobile_access_policy.dart';
import '../../services/notification_service.dart';
import '../../services/realtime_service.dart';
import '../../services/table_service.dart';
import '../../services/table_lock_service.dart';
import '../../utils/order_time_format.dart';
import '../../widgets/top_message.dart';
import '../menu/menu_screen.dart';
import '../notifications/notification_screen.dart';
import '../profile/profile_screen.dart';
import 'table_detail_v2.dart';

class TableScreen extends StatefulWidget {
  const TableScreen({super.key});

  @override
  State<TableScreen> createState() => _TableScreenState();
}

class _TableScreenState extends State<TableScreen> with WidgetsBindingObserver {
  final TableService _tableService = AppServices.tableService;
  final NotificationService _notificationService =
      AppServices.notificationService;
  final RealtimeService _realtimeService = AppServices.realtimeService;

  final List<RestaurantTable> tables = [];
  final Map<int, List<OrderItem>> tableOrders = {};
  final List<AppNotification> notifications = [];
  final Set<int> paymentRequestedTableIds = {};

  StreamSubscription<AppNotification>? _notificationSubscription;
  StreamSubscription<DishStatusRealtimeEvent>? _dishStatusSubscription;
  StreamSubscription<PaymentCompletedRealtimeEvent>? _paymentSubscription;
  StreamSubscription<TableStatusRealtimeEvent>? _tableStatusSubscription;
  Timer? _relativeTimeTimer;

  bool _isLoading = true;
  String? _loadError;
  bool _detailRouteActive = false;
  bool _realtimeRefreshPending = false;

  String _section = 'invoices';
  String? _selectedFloorFilter;
  TableStatus? _statusFilter;

  bool get _canManageTables =>
      MobileAccessPolicy.canManageTables(appEmployee.role);

  bool get _canOrder => MobileAccessPolicy.canOrder(appEmployee.role);

  bool get _canCompletePayment =>
      MobileAccessPolicy.canCompletePayment(appEmployee.role);

  Future<void> _refreshPendingPayments() async {
    if (!_canCompletePayment) return;

    try {
      final Set<int> pending = await AppServices.paymentService
          .getPendingTableIds();

      if (!mounted) return;

      if (_detailRouteActive) {
        paymentRequestedTableIds
          ..clear()
          ..addAll(pending);
        _realtimeRefreshPending = true;
        return;
      }

      setState(() {
        paymentRequestedTableIds
          ..clear()
          ..addAll(pending);
      });
    } catch (_) {
      // Không chặn sơ đồ bàn nếu endpoint trạng thái thanh toán tạm mất kết nối.
    }
  }

  List<String> get _areaNames {
    final Set<String> names = tables
        .where((table) => !table.isTakeaway)
        .map((table) => table.areaName?.trim() ?? '')
        .where((name) => name.isNotEmpty)
        .toSet();

    final List<String> result = names.toList()..sort();
    return result;
  }

  Map<String, List<String>> get _areasByFloor {
    final Map<String, List<String>> result = <String, List<String>>{};

    for (final String area in _areaNames) {
      final String floor = _floorNameForArea(area);
      result.putIfAbsent(floor, () => <String>[]).add(area);
    }

    for (final List<String> areas in result.values) {
      areas.sort(
        (a, b) => _zoneLabelForArea(a).compareTo(_zoneLabelForArea(b)),
      );
    }

    return result;
  }

  String _floorNameForArea(String areaName) {
    final RegExpMatch? match = RegExp(
      r'^\s*(.+?)\s*-\s*(.+)\s*$',
    ).firstMatch(areaName);
    if (match == null) return 'Khu vực';
    return match.group(1)?.trim() ?? 'Khu vực';
  }

  String _zoneLabelForArea(String areaName) {
    final RegExpMatch? match = RegExp(
      r'^\s*(.+?)\s*-\s*(.+)\s*$',
    ).firstMatch(areaName);
    if (match == null) return areaName.trim();
    return match.group(2)?.trim() ?? areaName.trim();
  }

  String? get _selectedAreaName {
    if (!_section.startsWith('area:')) return null;
    return _section.substring(5);
  }

  String? get _selectedFloorName {
    if (_selectedFloorFilter != null &&
        _selectedFloorFilter!.trim().isNotEmpty) {
      return _selectedFloorFilter;
    }

    final String? area = _selectedAreaName;
    if (area == null || area.isEmpty) return null;
    return _floorNameForArea(area);
  }

  void _selectArea(String areaName) {
    setState(() {
      _selectedFloorFilter = _floorNameForArea(areaName);
      _section = 'area:$areaName';
      _statusFilter = null;
    });

    if (!_canOperateArea(areaName)) {
      _showAreaNotice(areaName);
    }
  }

  bool _canOperateArea(String areaName) {
    return tables.any(
      (table) =>
          !table.isTakeaway && table.areaName == areaName && table.canOperate,
    );
  }

  List<RestaurantTable> get _activeInvoiceTables {
    final List<RestaurantTable> result = tables.where((table) {
      if (table.status != TableStatus.occupied) {
        return false;
      }

      final bool hasItems = (tableOrders[table.id]?.isNotEmpty ?? false);
      final bool waitingPayment = paymentRequestedTableIds.contains(table.id);

      return hasItems || waitingPayment;
    }).toList();

    result.sort((a, b) {
      final bool aPay = paymentRequestedTableIds.contains(a.id);
      final bool bPay = paymentRequestedTableIds.contains(b.id);
      if (aPay != bPay) return aPay ? -1 : 1;
      return a.name.compareTo(b.name);
    });

    return result;
  }

  List<RestaurantTable> get _takeawayTables {
    return tables
        .where(
          (table) => table.isTakeaway && table.status == TableStatus.occupied,
        )
        .toList()
      ..sort((a, b) => a.id.compareTo(b.id));
  }

  List<RestaurantTable> get _areaTables {
    if (!_section.startsWith('area:')) {
      return const <RestaurantTable>[];
    }

    final String areaName = _section.substring(5);

    return tables.where((table) {
      if (table.isTakeaway || table.areaName != areaName) {
        return false;
      }

      if (_statusFilter != null && table.status != _statusFilter) {
        return false;
      }

      return true;
    }).toList();
  }

  int get unreadNotificationCount =>
      notifications.where((item) => !item.isRead).length;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _loadData();
    _startRealtime();
    _relativeTimeTimer = Timer.periodic(
      const Duration(minutes: 1),
      (_) {
        if (mounted) setState(() {});
      },
    );
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _requestRealtimeRefresh();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _notificationSubscription?.cancel();
    _dishStatusSubscription?.cancel();
    _paymentSubscription?.cancel();
    _tableStatusSubscription?.cancel();
    _relativeTimeTimer?.cancel();
    _realtimeService.stop();
    super.dispose();
  }

  void _showRoleDenied(String action) {
    TopMessage.show(
      context,
      message:
          'Vai trò ${appEmployee.role} không có quyền $action trên Mobile.',
      type: TopMessageType.warning,
    );
  }

  void _showAreaNoticeForTable(RestaurantTable table) {
    _showAreaNotice(table.areaName ?? 'Khu vực này');
  }

  void _showAreaNotice(String areaName) {
    TopMessage.show(
      context,
      message:
          '$areaName không nằm trong phân công của bạn. Bạn vẫn có thể thao tác; hệ thống sẽ lưu lịch sử.',
      type: TopMessageType.warning,
      duration: const Duration(milliseconds: 3200),
    );
  }

  Future<void> _startRealtime() async {
    _notificationSubscription = _realtimeService.notifications.listen(
      _handleRealtimeNotification,
    );

    _dishStatusSubscription = _realtimeService.dishStatusChanges.listen(
      (_) => _requestRealtimeRefresh(),
    );

    _paymentSubscription = _realtimeService.paymentCompletions.listen((event) {
      paymentRequestedTableIds.remove(event.tableId);
      _requestRealtimeRefresh();
    });

    _tableStatusSubscription = _realtimeService.tableStatusChanges.listen(
      (_) => _requestRealtimeRefresh(),
    );

    try {
      await _realtimeService.start(
        employeeCode: appEmployee.employeeCode,
        roleKey: MobileAccessPolicy.backendRoleKey(appEmployee.role),
      );
    } catch (_) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Realtime chưa kết nối. Có thể kéo xuống để tải lại dữ liệu.',
        type: TopMessageType.warning,
      );
    }
  }

  void _requestRealtimeRefresh() {
    if (!mounted) return;

    if (_detailRouteActive) {
      _realtimeRefreshPending = true;
      return;
    }

    unawaited(_loadData());
  }

  bool _notificationEnabled(AppNotification notification) {
    final NotificationSettingsState settings = appNotificationSettingsState;
    final String title = notification.title.toLowerCase();

    if (title.contains('món đã xong')) {
      return settings.kitchenFinished;
    }

    if (title.contains('thanh toán')) {
      return settings.paymentRequest;
    }

    if (notification.notificationType == 'DatBan' ||
        title.contains('đặt bàn') ||
        title.contains('bàn')) {
      return settings.tableNotification;
    }

    return true;
  }

  // Quản lý vừa phân ca / đổi hoặc gỡ khu vực -> backend gửi thông báo loại CaLam.
  bool _isShiftAssignmentNotification(AppNotification n) =>
      n.relatedEntityType == 'ShiftAssignment' || n.notificationType == 'CaLam';

  void _handleRealtimeNotification(AppNotification notification) {
        if (!mounted) return;

    // Tải lại bàn để cập nhật khu vực được phân công, kể cả khi đã tắt âm báo.
    if (_isShiftAssignmentNotification(notification)) {
      _requestRealtimeRefresh();
    }

    if (!_notificationEnabled(notification)) {
      return;
    }

    final int existingIndex = notification.id == null
        ? -1
        : notifications.indexWhere((item) => item.id == notification.id);

    void applyNotification() {
      if (existingIndex >= 0) {
        notifications[existingIndex] = notification;
      } else {
        notifications.insert(0, notification);
      }
    }

    if (_detailRouteActive) {
      applyNotification();
      _realtimeRefreshPending = true;
    } else {
      setState(applyNotification);
    }

    if (existingIndex < 0) {
      AppServices.notificationFeedbackService.play(
        appNotificationSettingsState,
      );
    }

    final String normalizedTitle = notification.title.toLowerCase();
    if (_canCompletePayment && normalizedTitle.contains('yêu cầu thanh toán')) {
      unawaited(_refreshPendingPayments());
    }
  }

  Future<void> _loadData() async {
    try {
      final List<RestaurantTable> loadedTables = await _tableService
          .getTables();

      // Chi tiết món đang mở là dữ liệu bổ sung. Trước đây request này lỗi
      // sẽ làm mất luôn toàn bộ sơ đồ bàn dù API bàn vẫn hoạt động.
      Map<int, List<OrderItem>> loadedOrders = <int, List<OrderItem>>{};
      try {
        loadedOrders = await _tableService.getInitialOrders();
      } catch (_) {
        loadedOrders = <int, List<OrderItem>>{};
      }

      final List<RestaurantTable> normalizedTables = loadedTables.map((table) {
        final List<OrderItem> items =
            loadedOrders[table.id] ?? const <OrderItem>[];
        final int computedAmount = items.fold<int>(
          0,
          (sum, item) => sum + item.totalPrice,
        );

        return table.copyWith(
          amount: table.status == TableStatus.occupied
              ? (items.isNotEmpty ? computedAmount : (table.amount ?? 0))
              : table.amount,
        );
      }).toList();

      try {
        final NotificationSettingsState loadedSettings = await AppServices
            .notificationSettingsService
            .getSettings(employeeCode: appEmployee.employeeCode);
        appNotificationSettingsState.copyFrom(loadedSettings);
      } catch (_) {
        // Không để cài đặt thông báo chặn sơ đồ bàn.
      }

      List<AppNotification>? loadedNotifications;
      try {
        loadedNotifications = await _notificationService.getNotifications(
          employeeCode: appEmployee.employeeCode,
        );
      } catch (_) {
        loadedNotifications = null;
      }

      if (!mounted) return;

      setState(() {
        tables
          ..clear()
          ..addAll(normalizedTables);
        tableOrders
          ..clear()
          ..addAll(loadedOrders);

        if (loadedNotifications != null) {
          notifications
            ..clear()
            ..addAll(loadedNotifications.where(_notificationEnabled));
        }

        _isLoading = false;
        _loadError = null;
      });

      if (_canCompletePayment) {
        await _refreshPendingPayments();
      }
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = 'Không thể tải dữ liệu bàn';
      });
    }
  }

  String _formatMoney(int amount) {
    final String value = amount.toString();
    final StringBuffer buffer = StringBuffer();

    for (int i = 0; i < value.length; i++) {
      if (i > 0 && (value.length - i) % 3 == 0) {
        buffer.write('.');
      }
      buffer.write(value[i]);
    }

    return '${buffer.toString()}đ';
  }

  String _statusText(RestaurantTable table) {
    if (paymentRequestedTableIds.contains(table.id)) {
      return 'Chờ thanh toán';
    }

    switch (table.status) {
      case TableStatus.empty:
        return 'Trống';
      case TableStatus.occupied:
        return 'Đang dùng';
      case TableStatus.reserved:
        return 'Đã đặt';
    }
  }

  Color _statusColor(RestaurantTable table) {
    if (paymentRequestedTableIds.contains(table.id)) {
      return Colors.redAccent;
    }

    switch (table.status) {
      case TableStatus.empty:
        return const Color(0xFF2E9B65);
      case TableStatus.occupied:
        return AppTheme.orangeColor;
      case TableStatus.reserved:
        return const Color(0xFF2F80ED);
    }
  }

  Future<void> _openTable(RestaurantTable table) async {
    if (!table.canOperate) {
      _showAreaNoticeForTable(table);
    }

    RestaurantTable current = table;
    TableLockLease? acquiredLock;

    if (table.status == TableStatus.empty) {
      if (!_canManageTables) {
        _showRoleDenied('nhận bàn');
        return;
      }

      // Bàn trống chỉ được khóa để tránh hai nhân viên cùng thao tác.
      // Chạm vào bàn chỉ là xem chi tiết: KHÔNG đổi trạng thái trong DB.
      // Bàn chỉ chuyển sang Đang phục vụ khi nhập số khách, gọi món hoặc
      // người dùng bấm Mở bàn trong màn chi tiết.
      acquiredLock = await _tryAcquireTableLock(table);
      if (acquiredLock == null) return;
    } else if (table.status == TableStatus.reserved) {
      if (!_canManageTables) {
        _showRoleDenied('nhận bàn');
        return;
      }

      final bool? confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Nhận bàn đã đặt'),
          content: Text(
            '${table.customerName ?? 'Khách'} đã đến nhận ${table.name}?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Chưa'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('Nhận bàn'),
            ),
          ],
        ),
      );

      if (!mounted || confirmed != true) return;

      acquiredLock = await _tryAcquireTableLock(table);
      if (acquiredLock == null) return;

      try {
        await _tableService.receiveTable(tableId: table.id, customerCount: 0);
      } on ApiException catch (error) {
        await _releaseTableLock(acquiredLock);
        if (!mounted) return;
        TopMessage.show(
          context,
          message: error.message,
          type: TopMessageType.error,
        );
        return;
      }

      current = table.copyWith(
        status: TableStatus.occupied,
        amount: 0,
        clearTime: true,
      );
      _replaceTable(current);
    }

    if (!mounted) {
      await _releaseTableLock(acquiredLock);
      return;
    }

    await _openDetail(current, acquiredLock: acquiredLock);
  }

  void _replaceTable(RestaurantTable table) {
    final int index = tables.indexWhere((item) => item.id == table.id);
    if (index < 0) return;

    setState(() {
      tables[index] = table;
      tableOrders.putIfAbsent(table.id, () => <OrderItem>[]);
    });
  }

  Future<TableLockLease?> _tryAcquireTableLock(RestaurantTable table) async {
    try {
      return await AppServices.tableLockService.acquire(
        tableId: table.id,
        employeeCode: appEmployee.employeeCode,
      );
    } on ApiException catch (error) {
      if (!mounted) return null;
      TopMessage.show(
        context,
        message: error.message,
        type: error.statusCode == 409
            ? TopMessageType.warning
            : TopMessageType.error,
        duration: const Duration(milliseconds: 2600),
      );
      return null;
    }
  }

  Future<void> _releaseTableLock(TableLockLease? tableLock) async {
    if (tableLock == null) return;
    try {
      await AppServices.tableLockService.release(
        tableId: tableLock.tableId,
        employeeCode: appEmployee.employeeCode,
        clientToken: tableLock.clientToken,
      );
    } catch (_) {
      // Lease tự hết hạn sau 45 giây nếu thiết bị mất mạng đúng lúc thoát.
    }
  }

  Future<void> _openDetail(
    RestaurantTable table, {
    TableLockLease? acquiredLock,
    bool lockRequired = true,
  }) async {
    TableLockLease? tableLock = acquiredLock;

    if (lockRequired && tableLock == null) {
      tableLock = await _tryAcquireTableLock(table);
      if (tableLock == null) return;
    }

    final MaterialPageRoute<Map<String, dynamic>> detailRoute =
        MaterialPageRoute<Map<String, dynamic>>(
          builder: (context) => TableDetailV2(
            table: table,
            initialOrderItems: tableOrders[table.id] ?? const <OrderItem>[],
            notifications: notifications,
            initialPaymentRequested: paymentRequestedTableIds.contains(
              table.id,
            ),
            tableLockToken: tableLock?.clientToken,
            onTransferTable: (paymentRequested) => _transferTable(
              sourceTable: table,
              paymentRequested: paymentRequested,
            ),
            onMergeTable: () => _mergeTable(sourceTable: table),
            onLinkTables: () => _linkTables(sourceTable: table),
            onSplitTable: (sourceItems) =>
                _splitTable(sourceTable: table, sourceItems: sourceItems),
          ),
        );

    _detailRouteActive = true;
    _realtimeRefreshPending = false;

    Map<String, dynamic>? result;
    try {
      result = await Navigator.of(
        context,
      ).push<Map<String, dynamic>>(detailRoute);

      // Đợi animation pop và overlay entries gỡ xong hoàn toàn.
      await detailRoute.completed;
    } finally {
      await _releaseTableLock(tableLock);
      _detailRouteActive = false;
    }

    if (!mounted) return;

    final bool refreshPending = _realtimeRefreshPending;
    _realtimeRefreshPending = false;

    if (result == null) {
      if (refreshPending) {
        await _loadData();
      }
      return;
    }

    final dynamic rawItems = result['orderItems'];
    final List<OrderItem> newItems = rawItems is List
        ? rawItems.whereType<OrderItem>().toList()
        : <OrderItem>[];

    int? asNullableInt(dynamic value) {
      if (value is int) return value;
      if (value is num) return value.toInt();
      return int.tryParse(value?.toString() ?? '');
    }

    final int newAmount = asNullableInt(result['amount']) ?? 0;
    final int? guestCount = asNullableInt(result['guestCount']);
    final bool paymentRequested = result['paymentRequested'] == true;
    final bool paymentCompleted = result['paymentCompleted'] == true;
    final bool releaseTable = result['releaseTable'] == true;
    final bool tableCleared = result['tableCleared'] == true;
    final bool tableActivated = result.containsKey('tableActivated')
        ? result['tableActivated'] == true
        : table.status == TableStatus.occupied;

    if (result['transferTable'] == true) {
      await _transferTable(
        sourceTable: table,
        paymentRequested: paymentRequested,
      );
      return;
    }

    if (result['mergeTable'] == true) {
      await _mergeTable(sourceTable: table);
      return;
    }

    if (result['linkTables'] == true) {
      await _linkTables(sourceTable: table);
      return;
    }

    if (result['splitTable'] == true) {
      await _splitTable(sourceTable: table, sourceItems: newItems);
      return;
    }

    if (tableCleared) {
      paymentRequestedTableIds.remove(table.id);
      await _loadData();
      if (!mounted) return;

      TopMessage.show(
        context,
        message: table.isTakeaway
            ? 'Đã xóa đơn mang về'
            : 'Đã xóa toàn bộ món và trả ${table.name} về Trống',
        type: TopMessageType.success,
      );
      return;
    }

    if (releaseTable) {
      try {
        await _tableService.releaseTable(tableId: table.id);
      } on ApiException catch (error) {
        if (!mounted) return;
        TopMessage.show(
          context,
          message: error.message,
          type: TopMessageType.error,
        );
        return;
      }
      paymentRequestedTableIds.remove(table.id);
      await _loadData();
      return;
    }

    if (paymentCompleted) {
      paymentRequestedTableIds.remove(table.id);
      await _loadData();
      if (!mounted) return;

      TopMessage.show(
        context,
        message: 'Thanh toán ${table.name} đã hoàn tất',
        type: TopMessageType.success,
      );
      return;
    }

    final int index = tables.indexWhere((item) => item.id == table.id);

    if (index >= 0) {
      setState(() {
        tableOrders[table.id] = newItems;
        if (paymentRequested) {
          paymentRequestedTableIds.add(table.id);
        } else {
          paymentRequestedTableIds.remove(table.id);
        }

        tables[index] = tables[index].copyWith(
          status: tableActivated ? TableStatus.occupied : tables[index].status,
          amount: tableActivated ? newAmount : null,
          clearAmount: !tableActivated,
          guestCount: guestCount,
          clearGuestCount: guestCount == null,
        );
      });
    }

    if (refreshPending) {
      await _loadData();
    }
  }

  Future<RestaurantTable?> _pickTable({
    required String title,
    required List<RestaurantTable> options,
  }) {
    return showDialog<RestaurantTable>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: SizedBox(
          width: double.maxFinite,
          height: MediaQuery.sizeOf(dialogContext).height * 0.55,
          child: options.isEmpty
              ? const Center(child: Text('Không có bàn phù hợp'))
              : ListView.separated(
                  itemCount: options.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, index) {
                    final RestaurantTable table = options[index];
                    return ListTile(
                      leading: const Icon(Icons.table_restaurant_outlined),
                      title: Text(table.name),
                      subtitle: Text(table.areaName ?? ''),
                      onTap: () => Navigator.pop(dialogContext, table),
                    );
                  },
                ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Hủy'),
          ),
        ],
      ),
    );
  }

  Future<bool> _transferTable({
    required RestaurantTable sourceTable,
    required bool paymentRequested,
  }) async {
    final List<RestaurantTable> options = tables
        .where(
          (table) =>
              !table.isTakeaway &&
              table.id != sourceTable.id &&
              table.status == TableStatus.empty,
        )
        .toList();

    final RestaurantTable? target = await _pickTable(
      title: 'Chuyển ${sourceTable.name} sang',
      options: options,
    );

    if (!mounted || target == null) return false;

    try {
      await _tableService.transferTable(
        sourceTableId: sourceTable.id,
        targetTableId: target.id,
      );
      paymentRequestedTableIds.remove(sourceTable.id);
      if (paymentRequested) {
        paymentRequestedTableIds.add(target.id);
      }
      await _loadData();
      return true;
    } on ApiException catch (error) {
      if (!mounted) return false;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
      return false;
    }
  }

  Future<bool> _linkTables({required RestaurantTable sourceTable}) async {
    final List<RestaurantTable> options =
        tables
            .where(
              (table) =>
                  table.id != sourceTable.id &&
                  !table.isTakeaway &&
                  table.status == TableStatus.empty,
            )
            .toList()
          ..sort((a, b) => a.name.compareTo(b.name));

    if (options.isEmpty) {
      TopMessage.show(
        context,
        message: 'Không có bàn trống phù hợp để ghép',
        type: TopMessageType.warning,
      );
      return false;
    }

    final Set<int>? selectedIds = await showDialog<Set<int>>(
      context: context,
      builder: (dialogContext) {
        final Set<int> selected = <int>{};
        return StatefulBuilder(
          builder: (context, setDialogState) => AlertDialog(
            titlePadding: const EdgeInsets.fromLTRB(20, 14, 8, 8),
            title: Row(
              children: [
                Expanded(child: Text('Ghép bàn với ${sourceTable.name}')),
                FilledButton(
                  onPressed: selected.isEmpty
                      ? null
                      : () => Navigator.pop(
                          dialogContext,
                          Set<int>.from(selected),
                        ),
                  child: const Text('Lưu'),
                ),
              ],
            ),
            content: SizedBox(
              width: double.maxFinite,
              height: MediaQuery.sizeOf(dialogContext).height * 0.62,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Chọn một hoặc nhiều bàn để cùng dùng chung 1 đơn',
                    style: TextStyle(color: Colors.grey.shade700),
                  ),
                  const SizedBox(height: 12),
                  Expanded(
                    child: GridView.builder(
                      gridDelegate:
                          const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 3,
                            crossAxisSpacing: 10,
                            mainAxisSpacing: 10,
                            childAspectRatio: 1,
                          ),
                      itemCount: options.length,
                      itemBuilder: (_, index) {
                        final RestaurantTable table = options[index];
                        final bool checked = selected.contains(table.id);
                        return InkWell(
                          onTap: () {
                            setDialogState(() {
                              if (checked) {
                                selected.remove(table.id);
                              } else {
                                selected.add(table.id);
                              }
                            });
                          },
                          borderRadius: BorderRadius.circular(14),
                          child: AnimatedContainer(
                            duration: const Duration(milliseconds: 120),
                            decoration: BoxDecoration(
                              color: checked
                                  ? AppTheme.orangeColor.withValues(alpha: 0.12)
                                  : Colors.white,
                              borderRadius: BorderRadius.circular(14),
                              border: Border.all(
                                color: checked
                                    ? AppTheme.orangeColor
                                    : Colors.blueGrey.shade200,
                                width: checked ? 2 : 1,
                              ),
                            ),
                            child: Stack(
                              children: [
                                Center(
                                  child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Icon(
                                        Icons.table_restaurant_outlined,
                                        color: checked
                                            ? AppTheme.orangeColor
                                            : Colors.blueGrey,
                                      ),
                                      const SizedBox(height: 6),
                                      Text(
                                        table.name,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                if (checked)
                                  const Positioned(
                                    right: 5,
                                    top: 5,
                                    child: Icon(
                                      Icons.check_circle,
                                      color: AppTheme.orangeColor,
                                      size: 20,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: const Text('Hủy'),
              ),
            ],
          ),
        );
      },
    );

    if (!mounted || selectedIds == null || selectedIds.isEmpty) return false;

    try {
      await _tableService.linkTables(
        primaryTableId: sourceTable.id,
        tableIds: selectedIds.toList(),
        employeeCode: appEmployee.employeeCode,
      );
      await _loadData();
      if (!mounted) return false;
      TopMessage.show(
        context,
        message:
            'Đã ghép ${selectedIds.length} bàn vào đơn của ${sourceTable.name}',
        type: TopMessageType.success,
      );
      return true;
    } on ApiException catch (error) {
      if (!mounted) return false;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
      return false;
    }
  }

  Future<bool> _mergeTable({required RestaurantTable sourceTable}) async {
    final List<RestaurantTable> options = tables
        .where(
          (table) =>
              !table.isTakeaway &&
              table.id != sourceTable.id &&
              table.status == TableStatus.occupied,
        )
        .toList();

    final RestaurantTable? target = await _pickTable(
      title: 'Gộp đơn ${sourceTable.name} vào bàn',
      options: options,
    );

    if (!mounted || target == null) return false;

    try {
      await _tableService.mergeTable(
        sourceTableId: sourceTable.id,
        targetTableId: target.id,
      );
      paymentRequestedTableIds.remove(sourceTable.id);
      await _loadData();
      return true;
    } on ApiException catch (error) {
      if (!mounted) return false;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
      return false;
    }
  }

  Future<bool> _splitTable({
    required RestaurantTable sourceTable,
    required List<OrderItem> sourceItems,
  }) async {
    if (sourceItems.isEmpty) {
      TopMessage.show(
        context,
        message: 'Bàn này chưa có món để tách',
        type: TopMessageType.warning,
      );
      return false;
    }

    final Set<int> selected = {};
    final Map<int, int> quantities = {};

    final Map<int, int>? selection = await showDialog<Map<int, int>>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) {
          return AlertDialog(
            title: const Text('Chọn món cần tách'),
            content: SizedBox(
              width: double.maxFinite,
              height: MediaQuery.sizeOf(dialogContext).height * 0.58,
              child: ListView.builder(
                itemCount: sourceItems.length,
                itemBuilder: (context, index) {
                  final OrderItem item = sourceItems[index];
                  final bool checked = selected.contains(index);
                  final int qty = quantities[index] ?? 1;

                  return Card(
                    elevation: 0,
                    child: Column(
                      children: [
                        CheckboxListTile(
                          value: checked,
                          title: Text(item.name),
                          subtitle: Text(
                            'SL ${item.quantity} • ${_formatMoney(item.totalPrice)}',
                          ),
                          onChanged: item.id == null
                              ? null
                              : (value) {
                                  setDialogState(() {
                                    if (value == true) {
                                      selected.add(index);
                                      quantities[index] = 1;
                                    } else {
                                      selected.remove(index);
                                      quantities.remove(index);
                                    }
                                  });
                                },
                        ),
                        if (checked)
                          Row(
                            mainAxisAlignment: MainAxisAlignment.end,
                            children: [
                              IconButton(
                                onPressed: qty > 1
                                    ? () {
                                        setDialogState(() {
                                          quantities[index] = qty - 1;
                                        });
                                      }
                                    : null,
                                icon: const Icon(Icons.remove),
                              ),
                              Text('$qty / ${item.quantity}'),
                              IconButton(
                                onPressed: qty < item.quantity
                                    ? () {
                                        setDialogState(() {
                                          quantities[index] = qty + 1;
                                        });
                                      }
                                    : null,
                                icon: const Icon(Icons.add),
                              ),
                            ],
                          ),
                      ],
                    ),
                  );
                },
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: const Text('Hủy'),
              ),
              FilledButton(
                onPressed: selected.isEmpty
                    ? null
                    : () {
                        final Map<int, int> result = {};
                        for (final int index in selected) {
                          result[index] = quantities[index] ?? 1;
                        }
                        Navigator.pop(dialogContext, result);
                      },
                child: const Text('Tiếp tục'),
              ),
            ],
          );
        },
      ),
    );

    if (!mounted || selection == null) return false;

    int remainingQuantity = 0;
    for (int index = 0; index < sourceItems.length; index++) {
      remainingQuantity +=
          sourceItems[index].quantity - (selection[index] ?? 0);
    }

    if (remainingQuantity <= 0) {
      TopMessage.show(
        context,
        message: 'Không thể tách toàn bộ món. Hãy dùng Chuyển bàn.',
        type: TopMessageType.warning,
      );
      return false;
    }

    final List<RestaurantTable> targets = tables
        .where(
          (table) =>
              !table.isTakeaway &&
              table.id != sourceTable.id &&
              table.status == TableStatus.empty,
        )
        .toList();

    final RestaurantTable? target = await _pickTable(
      title: 'Chọn bàn nhận món',
      options: targets,
    );

    if (!mounted || target == null) return false;

    final List<Map<String, dynamic>> apiItems = [];
    for (final MapEntry<int, int> entry in selection.entries) {
      final OrderItem item = sourceItems[entry.key];
      if (item.id == null) continue;
      apiItems.add({'orderItemId': item.id, 'quantity': entry.value});
    }

    if (apiItems.isEmpty) return false;

    try {
      await _tableService.splitTable(
        sourceTableId: sourceTable.id,
        targetTableId: target.id,
        items: apiItems,
      );
      paymentRequestedTableIds.remove(sourceTable.id);
      paymentRequestedTableIds.remove(target.id);
      await _loadData();
      return true;
    } on ApiException catch (error) {
      if (!mounted) return false;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
      return false;
    }
  }

  // Bấm "Tạo đơn mang về": hỏi khách đang ăn tại bàn hay chỉ mua mang về.
  Future<void> _createTakeaway() async {
    if (!_canManageTables && !_canOrder) {
      _showRoleDenied('tạo đơn mang về');
      return;
    }

    final String? mode = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 4, 20, 8),
              child: Text(
                'Khách mua mang về',
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
              ),
            ),
            ListTile(
              leading: const Icon(Icons.table_restaurant_outlined),
              title: const Text(
                'Khách đang ăn tại bàn',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              subtitle: const Text(
                'Mua thêm mang về, tính chung hóa đơn của bàn',
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => Navigator.pop(sheetContext, 'dine'),
            ),
            const Divider(height: 1),
            ListTile(
              leading: const Icon(Icons.shopping_bag_outlined),
              title: const Text(
                'Khách chỉ mua mang về',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              subtitle: const Text('Tạo đơn mang về riêng, không gắn bàn'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => Navigator.pop(sheetContext, 'only'),
            ),
            const SizedBox(height: 12),
          ],
        ),
      ),
    );

    if (!mounted || mode == null) return;

    if (mode == 'dine') {
      await _orderTakeawayForDiningTable();
    } else {
      await _createStandaloneTakeaway();
    }
  }

  // Khách đang ăn tại bàn mua thêm mang về: món được lưu vào đơn của chính bàn
  // đó (IsTakeaway = true) nên tự động tính chung hóa đơn.
  Future<void> _orderTakeawayForDiningTable() async {
    final List<RestaurantTable> options =
        tables
            .where(
              (table) =>
                  !table.isTakeaway && table.status == TableStatus.occupied,
            )
            .toList()
          ..sort((a, b) {
            final int areaCompare = (a.areaName ?? '').compareTo(
              b.areaName ?? '',
            );
            if (areaCompare != 0) return areaCompare;
            return a.name.compareTo(b.name);
          });

    if (options.isEmpty) {
      TopMessage.show(
        context,
        message: 'Chưa có bàn nào đang phục vụ khách.',
        type: TopMessageType.warning,
      );
      return;
    }

    final RestaurantTable? target = await showModalBottomSheet<RestaurantTable>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: SizedBox(
          height: MediaQuery.sizeOf(sheetContext).height * 0.68,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 4, 20, 12),
                child: Text(
                  'Chọn bàn khách đang ngồi',
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                ),
              ),
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.fromLTRB(12, 0, 12, 18),
                  itemCount: options.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (_, index) {
                    final RestaurantTable table = options[index];
                    return ListTile(
                      leading: const Icon(Icons.table_restaurant_outlined),
                      title: Text(
                        table.name,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text(table.areaName ?? 'Tại quán'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => Navigator.pop(sheetContext, table),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );

    if (!mounted || target == null) return;

    final List<CartItem>? items = await Navigator.push<List<CartItem>>(
      context,
      MaterialPageRoute<List<CartItem>>(
        builder: (context) => MenuScreen(
          table: target,
          takeawayOnly: true,
          notifications: notifications,
        ),
      ),
    );

    if (!mounted || items == null || items.isEmpty) return;

    try {
      await AppServices.orderService.addItems(
        tableId: target.id,
        employeeCode: appEmployee.employeeCode,
        items: items,
      );

      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Đã thêm món mang về vào ${target.name}',
        type: TopMessageType.success,
      );
      await _loadData();
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } catch (_) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Không thể gọi món. Vui lòng thử lại.',
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _createStandaloneTakeaway() async {
    if (!_canManageTables && !_canOrder) {
      _showRoleDenied('tạo đơn mang về');
      return;
    }

    try {
      final RestaurantTable table = await _tableService.openTakeaway();

      if (!mounted) return;

      final int existingIndex = tables.indexWhere(
        (item) => item.id == table.id,
      );

      setState(() {
        if (existingIndex >= 0) {
          tables[existingIndex] = table;
        } else {
          tables.add(table);
        }
        tableOrders[table.id] = <OrderItem>[];
      });

      await _openDetail(table);
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _openMenuFromBottomNav() async {
    if (!_canOrder) {
      _showRoleDenied('gọi món');
      return;
    }

    // Tab Thực đơn mở ngay, không buộc chọn bàn trước. MenuScreen sẽ yêu cầu
    // chọn bàn/đơn chỉ khi nhân viên đã chọn món và xác nhận giỏ hàng.
    await Navigator.push<void>(
      context,
      _tabRoute<void>(
        MenuScreen(notifications: notifications),
      ),
    );

    if (mounted) {
      await _loadData();
    }
  }

  Route<T> _tabRoute<T>(Widget page) {
    return PageRouteBuilder<T>(
      pageBuilder: (_, _, _) => page,
      transitionDuration: Duration.zero,
      reverseTransitionDuration: Duration.zero,
    );
  }

  Future<void> _openUtilityTab(int startIndex) async {
    int tabIndex = startIndex;

    while (mounted && (tabIndex == 2 || tabIndex == 3)) {
      final int? targetIndex = await Navigator.push<int>(
        context,
        _tabRoute<int>(
          tabIndex == 2
              ? NotificationScreen(
                  notifications: notifications,
                  notificationService: _notificationService,
                  employeeCode: appEmployee.employeeCode,
                  onReadStateChanged: () {
                    if (mounted) setState(() {});
                  },
                )
              : ProfileScreen(notifications: notifications),
        ),
      );

      if (!mounted || targetIndex == null) return;

      if (targetIndex == 0) return;
      if (targetIndex == 1) {
        await _openMenuFromBottomNav();
        return;
      }
      tabIndex = targetIndex;
    }
  }

  Widget _notificationIcon({Color? color}) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Icon(Icons.notifications_none, color: color),
        if (unreadNotificationCount > 0)
          Positioned(
            right: -7,
            top: -6,
            child: Container(
              constraints: const BoxConstraints(minWidth: 17, minHeight: 17),
              padding: const EdgeInsets.symmetric(horizontal: 4),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: Colors.red,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                unreadNotificationCount > 99
                    ? '99+'
                    : '$unreadNotificationCount',
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 9,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Bàn & hóa đơn',
          style: TextStyle(
            color: AppTheme.textColor,
            fontWeight: FontWeight.bold,
            fontSize: 20,
          ),
        ),
        actions: [
          IconButton(
            onPressed: () => _openUtilityTab(2),
            icon: _notificationIcon(color: AppTheme.textColor),
          ),
        ],
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: AppTheme.orangeColor),
            )
          : _loadError != null && tables.isEmpty
          ? _buildLoadError()
          : Column(
              children: [
                _buildSectionTabs(),
                if (_section.startsWith('area:')) _buildStatusFilters(),
                Expanded(child: _buildSectionBody()),
              ],
            ),
      bottomNavigationBar: _buildBottomNavigation(),
    );
  }

  Widget _buildSectionTabs() {
    final Map<String, List<String>> grouped = _areasByFloor;
    final List<String> floors = grouped.keys.toList()..sort();
    final String? selectedArea = _selectedAreaName;
    final String? selectedFloor = _selectedFloorName;

    final List<String> floorAreas = selectedFloor == null
        ? <String>[]
        : (grouped[selectedFloor] ?? const <String>[]);

    final String? areaDropdownValue =
        selectedArea != null && floorAreas.contains(selectedArea)
            ? selectedArea
            : null;

    Widget navChip({
      required String label,
      required IconData icon,
      required bool selected,
      required VoidCallback onTap,
    }) {
      return ChoiceChip(
        selected: selected,
        showCheckmark: false,
        avatar: Icon(
          icon,
          size: 17,
          color: selected ? Colors.white : Colors.grey.shade700,
        ),
        label: Text(label),
        labelStyle: TextStyle(
          color: selected ? Colors.white : AppTheme.textColor,
          fontWeight: FontWeight.w600,
        ),
        selectedColor: AppTheme.orangeColor,
        backgroundColor: const Color(0xFFF1F2F4),
        side: BorderSide.none,
        onSelected: (_) => onTap(),
      );
    }

    Widget dropdownShell({required bool selected, required Widget child}) {
      return Container(
        height: 42,
        padding: const EdgeInsets.symmetric(horizontal: 11),
        decoration: BoxDecoration(
          color: selected
              ? AppTheme.orangeColor.withValues(alpha: 0.10)
              : const Color(0xFFF1F2F4),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: selected
                ? AppTheme.orangeColor.withValues(alpha: 0.45)
                : Colors.transparent,
          ),
        ),
        child: child,
      );
    }

    return Container(
      color: Colors.white,
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      child: Column(
        children: [
          Row(
            children: [
              navChip(
                label: 'Hóa đơn',
                icon: Icons.receipt_long_outlined,
                selected: _section == 'invoices',
                onTap: () {
                  setState(() {
                    _section = 'invoices';
                    _selectedFloorFilter = null;
                    _statusFilter = null;
                  });
                },
              ),
              const SizedBox(width: 8),
              Expanded(
                child: dropdownShell(
                  selected:
                      selectedFloor != null &&
                      (_section == 'areas' ||
                          _section.startsWith('area:')),
                  child: DropdownButtonHideUnderline(
                    child: DropdownButton<String>(
                      isExpanded: true,
                      value: selectedFloor,
                      hint: const Row(
                        children: [
                          Icon(Icons.layers_outlined, size: 17),
                          SizedBox(width: 7),
                          Text(
                            'Tầng',
                            style: TextStyle(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                      icon: const Icon(Icons.keyboard_arrow_down),
                      items: floors
                          .map(
                            (floor) => DropdownMenuItem<String>(
                              value: floor,
                              child: Row(
                                children: [
                                  const Icon(Icons.layers_outlined, size: 17),
                                  const SizedBox(width: 7),
                                  Expanded(
                                    child: Text(
                                      floor,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: (floor) {
                        if (floor == null) return;

                        setState(() {
                          _selectedFloorFilter = floor;
                          // Chọn tầng chỉ lọc danh sách khu, không tự chọn Khu A.
                          _section = 'areas';
                          _statusFilter = null;
                        });
                      },
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              navChip(
                label: 'Mang về',
                icon: Icons.shopping_bag_outlined,
                selected: _section == 'takeaway',
                onTap: () {
                  setState(() {
                    _section = 'takeaway';
                    _selectedFloorFilter = null;
                    _statusFilter = null;
                  });
                },
              ),
            ],
          ),
          if (_section == 'areas' || _section.startsWith('area:')) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: dropdownShell(
                    selected: selectedArea != null,
                    child: DropdownButtonHideUnderline(
                      child: DropdownButton<String>(
                        isExpanded: true,
                        value: areaDropdownValue,
                        hint: const Row(
                          children: [
                            Icon(
                              Icons.table_restaurant_outlined,
                              size: 17,
                            ),
                            SizedBox(width: 7),
                            Text(
                              'Khu',
                              style: TextStyle(
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ],
                        ),
                        icon: const Icon(Icons.keyboard_arrow_down),
                        items: floorAreas
                            .map(
                              (area) => DropdownMenuItem<String>(
                                value: area,
                                child: Row(
                                  children: [
                                    const Icon(
                                      Icons.table_restaurant_outlined,
                                      size: 17,
                                    ),
                                    const SizedBox(width: 7),
                                    Expanded(
                                      child: Text(
                                        _zoneLabelForArea(area),
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            )
                            .toList(),
                        onChanged: (area) {
                          if (area != null) _selectArea(area);
                        },
                      ),
                    ),
                  ),
                ),
                if (selectedArea != null && !_canOperateArea(selectedArea)) ...[
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 9,
                      vertical: 8,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFFF7ED),
                      borderRadius: BorderRadius.circular(11),
                      border: Border.all(
                        color: const Color(0xFFF59E0B).withValues(alpha: 0.45),
                      ),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.info_outline,
                          size: 15,
                          color: Color(0xFFD97706),
                        ),
                        SizedBox(width: 4),
                        Text(
                          'Ngoài phân công',
                          style: TextStyle(
                            color: Color(0xFFD97706),
                            fontSize: 10,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildStatusFilters() {
    final List<MapEntry<TableStatus?, String>> filters = [
      const MapEntry(null, 'Tất cả'),
      const MapEntry(TableStatus.empty, 'Trống'),
      const MapEntry(TableStatus.occupied, 'Đang dùng'),
      const MapEntry(TableStatus.reserved, 'Đã đặt'),
    ];

    return Container(
      width: double.infinity,
      color: Colors.white,
      padding: const EdgeInsets.fromLTRB(12, 0, 12, 10),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: filters.map((entry) {
            final bool selected = _statusFilter == entry.key;
            return Padding(
              padding: const EdgeInsets.only(right: 7),
              child: FilterChip(
                selected: selected,
                label: Text(entry.value),
                onSelected: (_) {
                  setState(() {
                    _statusFilter = entry.key;
                  });
                },
              ),
            );
          }).toList(),
        ),
      ),
    );
  }

  Widget _buildSectionBody() {
    if (_section == 'invoices') {
      return _buildInvoiceView();
    }

    if (_section == 'takeaway') {
      return _buildTakeawayView();
    }

    return _buildAreaView();
  }

  DateTime? _firstOrderTime(List<OrderItem> items) {
    DateTime? result;
    for (final OrderItem item in items) {
      final DateTime? value = item.createdAt;
      if (value == null) continue;
      if (result == null || value.isBefore(result)) {
        result = value;
      }
    }
    return result;
  }

  DateTime? _latestOrderTime(List<OrderItem> items) {
    DateTime? result;
    for (final OrderItem item in items) {
      final DateTime? value = item.createdAt;
      if (value == null) continue;
      if (result == null || value.isAfter(result)) {
        result = value;
      }
    }
    return result;
  }

  Widget _buildInvoiceView() {
    final List<RestaurantTable> items = _activeInvoiceTables;

    if (items.isEmpty) {
      return _emptyState(
        icon: Icons.receipt_long_outlined,
        title: 'Chưa có hóa đơn đang mở',
        subtitle:
            'Khi bàn gọi món hoặc có đơn mang về, hóa đơn sẽ xuất hiện ở đây.',
      );
    }

    return RefreshIndicator(
      onRefresh: _loadData,
      child: GridView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(14),
        itemCount: items.length,
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          crossAxisSpacing: 12,
          mainAxisSpacing: 12,
          childAspectRatio: 1.08,
        ),
        itemBuilder: (context, index) => _buildInvoiceCard(items[index]),
      ),
    );
  }

  Widget _buildInvoiceCard(RestaurantTable table) {
    final List<OrderItem> items = tableOrders[table.id] ?? const <OrderItem>[];
    final int quantity = items.fold<int>(0, (sum, item) => sum + item.quantity);
    final DateTime? firstOrderAt = _firstOrderTime(items);
    final bool waiting = paymentRequestedTableIds.contains(table.id);

    return InkWell(
      onTap: () => _openTable(table),
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: waiting
                ? Colors.redAccent.withValues(alpha: 0.5)
                : !table.canOperate
                ? const Color(0xFFF59E0B).withValues(alpha: 0.55)
                : Colors.grey.shade200,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    table.name,
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                ),
                Tooltip(
                  message: !table.canOperate
                      ? 'Ngoài khu vực phân công - vẫn thao tác được'
                      : table.isTakeaway
                      ? 'Đơn mang về'
                      : 'Hóa đơn',
                  child: Icon(
                    !table.canOperate
                        ? Icons.info_outline
                        : table.isTakeaway
                        ? Icons.shopping_bag_outlined
                        : Icons.receipt_long_outlined,
                    color: !table.canOperate
                        ? const Color(0xFFD97706)
                        : waiting
                        ? Colors.redAccent
                        : AppTheme.orangeColor,
                    size: 20,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              _formatMoney(table.amount ?? 0),
              style: const TextStyle(
                color: AppTheme.orangeColor,
                fontSize: 18,
                fontWeight: FontWeight.bold,
              ),
            ),
            const Spacer(),
            Text(
              '$quantity món',
              style: TextStyle(
                color: Colors.grey.shade700,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 3),
            Text(
              table.isTakeaway ? 'Mang về' : (table.areaName ?? 'Tại quán'),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
            ),
            if (firstOrderAt != null)
              Text(
                formatElapsedCompact(firstOrderAt),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: Colors.grey.shade700,
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                ),
              ),
            if (!table.isTakeaway && table.guestCount != null)
              Text(
                '${table.guestCount} khách',
                style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
              ),
            if (waiting) ...[
              const SizedBox(height: 5),
              const Text(
                'Chờ thanh toán',
                style: TextStyle(
                  color: Colors.redAccent,
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildAreaView() {
    if (_selectedAreaName == null) {
      return _emptyState(
        icon: Icons.table_restaurant_outlined,
        title: 'Chọn tầng và khu',
        subtitle: 'Chọn tầng trước, sau đó chọn khu để xem danh sách bàn.',
      );
    }

    final List<RestaurantTable> items = _areaTables;

    if (items.isEmpty) {
      return _emptyState(
        icon: Icons.table_restaurant_outlined,
        title: 'Không có bàn phù hợp',
        subtitle: 'Thử chọn trạng thái khác hoặc kéo xuống để tải lại.',
      );
    }

    return RefreshIndicator(
      onRefresh: _loadData,
      child: GridView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(14),
        itemCount: items.length,
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          crossAxisSpacing: 12,
          mainAxisSpacing: 12,
          // Safari/iPhone Web làm tròn device pixels khác Android.
          // Cho card cao hơn một chút ở màn hình hẹp để không overflow.
          childAspectRatio:
              MediaQuery.sizeOf(context).width <= 430
                  ? 0.94
                  : 1.02,
        ),
        itemBuilder: (context, index) => _buildTableCard(items[index]),
      ),
    );
  }

  Widget _buildTableCard(RestaurantTable table) {
    final Color color = _statusColor(table);
    final List<OrderItem> items = tableOrders[table.id] ?? const <OrderItem>[];
    final int quantity = items.fold<int>(0, (sum, item) => sum + item.quantity);
    final DateTime? latestOrderAt = _latestOrderTime(items);

    Color cardBackground = Colors.white;
    Color cardBorder = Colors.grey.shade200;
    double borderWidth = 1;

    if (paymentRequestedTableIds.contains(table.id)) {
      cardBackground = const Color(0xFFFFF1F2);
      cardBorder = const Color(0xFFFDA4AF);
      borderWidth = 1.5;
    } else if (table.status == TableStatus.occupied) {
      cardBackground = const Color(0xFFFFF4E8);
      cardBorder = const Color(0xFFFFB77A);
      borderWidth = 1.4;
    } else if (table.status == TableStatus.reserved) {
      cardBackground = const Color(0xFFEFF6FF);
      cardBorder = const Color(0xFF93C5FD);
      borderWidth = 1.3;
    }

    return InkWell(
      onTap: () => _openTable(table),
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: cardBackground,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: cardBorder, width: borderWidth),
          boxShadow: table.status == TableStatus.occupied
              ? [
                  BoxShadow(
                    color: AppTheme.orangeColor.withValues(alpha: 0.08),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              table.name,
              maxLines: 1,
              softWrap: false,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 17),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 6,
              runSpacing: 5,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (!table.canOperate)
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 7,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFFF7ED),
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.info_outline,
                          size: 12,
                          color: Color(0xFFD97706),
                        ),
                        SizedBox(width: 3),
                        Text(
                          'Ngoài phân công',
                          style: TextStyle(
                            color: Color(0xFFD97706),
                            fontSize: 9.5,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.11),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Text(
                    _statusText(table),
                    maxLines: 1,
                    softWrap: false,
                    style: TextStyle(
                      color: color,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            const Spacer(),
            if (table.status == TableStatus.occupied) ...[
              Text(
                table.guestCount == null
                    ? 'Chưa nhập số khách'
                    : '${table.guestCount} khách',
                style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
              ),
              if (quantity > 0)
                Text(
                  '$quantity món',
                  style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
                ),
              if (latestOrderAt != null)
                Text(
                  formatElapsedCompact(latestOrderAt),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: Colors.grey.shade600,
                    fontSize: 10.5,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              Text(
                _formatMoney(table.amount ?? 0),
                style: TextStyle(
                  color: color,
                  fontWeight: FontWeight.bold,
                  fontSize: 14,
                ),
              ),
            ] else if (table.status == TableStatus.reserved) ...[
              Text(
                table.customerName ?? 'Khách đặt bàn',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
              ),
              if (table.guestCount != null)
                Text(
                  '${table.guestCount} khách',
                  style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
                ),
              if (table.time != null)
                Text(
                  table.time!,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 12,
                  ),
                ),
            ] else
              Text(
                'Chạm để mở bàn',
                style: TextStyle(color: Colors.grey.shade500, fontSize: 12),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildTakeawayView() {
    final List<RestaurantTable> items = _takeawayTables;

    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(14),
        children: [
          SizedBox(
            width: double.infinity,
            height: 48,
            child: FilledButton.icon(
              onPressed: _createTakeaway,
              style: FilledButton.styleFrom(
                backgroundColor: AppTheme.orangeColor,
              ),
              icon: const Icon(Icons.add_shopping_cart),
              label: const Text('Tạo đơn mang về'),
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Đơn mang về không cần nhập số khách.',
            style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
          ),
          const SizedBox(height: 14),
          if (items.isEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 70),
              child: _emptyStateContent(
                icon: Icons.shopping_bag_outlined,
                title: 'Chưa có đơn mang về',
                subtitle: 'Bấm “Tạo đơn mang về” để bắt đầu gọi món.',
              ),
            )
          else
            ...items.map(
              (table) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _buildTakeawayCard(table),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildTakeawayCard(RestaurantTable table) {
    final List<OrderItem> items = tableOrders[table.id] ?? const <OrderItem>[];
    final int quantity = items.fold<int>(0, (sum, item) => sum + item.quantity);

    return InkWell(
      onTap: () => _openTable(table),
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(15),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: Colors.grey.shade200),
        ),
        child: Row(
          children: [
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: AppTheme.orangeColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(
                Icons.shopping_bag_outlined,
                color: AppTheme.orangeColor,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    table.name,
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    '$quantity món • ${_formatMoney(table.amount ?? 0)}',
                    style: TextStyle(color: Colors.grey.shade700),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }

  Widget _emptyState({
    required IconData icon,
    required String title,
    required String subtitle,
  }) {
    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.only(top: 130),
        children: [
          _emptyStateContent(icon: icon, title: title, subtitle: subtitle),
        ],
      ),
    );
  }

  Widget _emptyStateContent({
    required IconData icon,
    required String title,
    required String subtitle,
  }) {
    return Column(
      children: [
        Icon(icon, size: 52, color: Colors.grey.shade400),
        const SizedBox(height: 12),
        Text(
          title,
          textAlign: TextAlign.center,
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
        ),
        const SizedBox(height: 5),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 30),
          child: Text(
            subtitle,
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
          ),
        ),
      ],
    );
  }

  Widget _buildLoadError() {
    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.only(top: 130),
        children: [
          _emptyStateContent(
            icon: Icons.cloud_off_outlined,
            title: 'Không thể tải dữ liệu',
            subtitle: _loadError ?? 'Kiểm tra mạng hoặc Backend rồi thử lại.',
          ),
        ],
      ),
    );
  }

  Widget _buildBottomNavigation() {
    return BottomNavigationBar(
      currentIndex: 0,
      type: BottomNavigationBarType.fixed,
      selectedItemColor: AppTheme.orangeColor,
      unselectedItemColor: Colors.grey,
      onTap: (index) async {
        if (index == 0) return;
        if (index == 1) {
          await _openMenuFromBottomNav();
          return;
        }
        if (index == 2) {
          await _openUtilityTab(2);
          return;
        }
        if (index == 3) {
          await _openUtilityTab(3);
        }
      },
      items: [
        const BottomNavigationBarItem(
          icon: Icon(Icons.grid_view_rounded),
          label: 'Bàn',
        ),
        const BottomNavigationBarItem(
          icon: Icon(Icons.restaurant_menu),
          label: 'Thực đơn',
        ),
        BottomNavigationBarItem(icon: _notificationIcon(), label: 'Thông báo'),
        const BottomNavigationBarItem(
          icon: Icon(Icons.person_outline),
          label: 'Cá nhân',
        ),
      ],
    );
  }
}

class _DashboardTab {
  final String keyValue;
  final String label;
  final IconData icon;

  const _DashboardTab({
    required this.keyValue,
    required this.label,
    required this.icon,
  });
}
