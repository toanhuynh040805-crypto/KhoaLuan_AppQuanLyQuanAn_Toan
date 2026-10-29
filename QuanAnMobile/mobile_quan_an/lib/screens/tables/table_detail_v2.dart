import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/app_notification.dart';
import '../../models/cart_item.dart';
import '../../models/employee_profile.dart';
import '../../models/member_customer.dart';
import '../../models/order_item.dart';
import '../../models/payment_preview.dart';
import '../../models/restaurant_table.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/mobile_access_policy.dart';
import '../../services/realtime_service.dart';
import '../../widgets/top_message.dart';
import '../invoices/receipt_preview_screen.dart';
import '../menu/menu_screen.dart';
import '../order_log/order_log_screen.dart';
import 'payment_dialog.dart';
import 'kitchen_requests_screen.dart';
import 'order_item_edit_screen.dart';

class TableDetailV2 extends StatefulWidget {
  final RestaurantTable table;
  final List<OrderItem> initialOrderItems;
  final List<AppNotification> notifications;
  final bool initialPaymentRequested;
  final String? tableLockToken;
  final Future<bool> Function(bool paymentRequested)? onTransferTable;
  final Future<bool> Function()? onMergeTable;
  final Future<bool> Function()? onLinkTables;
  final Future<bool> Function(List<OrderItem> sourceItems)? onSplitTable;

  const TableDetailV2({
    super.key,
    required this.table,
    required this.initialOrderItems,
    required this.notifications,
    this.initialPaymentRequested = false,
    this.tableLockToken,
    this.onTransferTable,
    this.onMergeTable,
    this.onLinkTables,
    this.onSplitTable,
  });

  @override
  State<TableDetailV2> createState() => _TableDetailV2State();
}

class _TableDetailV2State extends State<TableDetailV2> {
  late RestaurantTable currentTable;
  late List<OrderItem> orderItems;
  late int currentAmount;
  bool isPaymentRequested = false;
  bool paymentCompleted = false;
  bool _savingGuestCount = false;
  bool _guestCountEditorOpen = false;
  int _guestCountDraft = 1;
  bool _bulkActionRunning = false;
  bool _closingAfterPayment = false;
  bool _tableLockLost = false;
  bool _tableLockLostDialogShowing = false;
  bool _tableActionRunning = false;
  String _tableLockLostMessage = '';
  MemberCustomer? _memberCustomer;
  Timer? _tableLockHeartbeatTimer;
  Timer? _paymentRequestLockTimer;
  bool _paymentRequestLocked = false;
  int _paymentRequestRemainingSeconds = 0;

  StreamSubscription<DishStatusRealtimeEvent>? _dishStatusSubscription;
  StreamSubscription<PaymentCompletedRealtimeEvent>? _paymentSubscription;
  StreamSubscription<TableLockTakenOverRealtimeEvent>?
      _tableLockTakeoverSubscription;

  bool get _canManageTables =>
      MobileAccessPolicy.canManageTables(appEmployee.role);

  bool get _canOrder => MobileAccessPolicy.canOrder(appEmployee.role);

  bool get _canCancelOrderItem =>
      MobileAccessPolicy.canCancelOrderItem(appEmployee.role);

  bool get _canMarkServed => MobileAccessPolicy.canMarkServed(appEmployee.role);

  bool get _canRequestPayment =>
      MobileAccessPolicy.canRequestPayment(appEmployee.role);

  bool get _canCompletePayment =>
      MobileAccessPolicy.canCompletePayment(appEmployee.role);

  int get totalQuantity =>
      orderItems.fold<int>(0, (sum, item) => sum + item.quantity);

  bool get allItemsServed =>
      orderItems.isNotEmpty &&
      orderItems.every((item) => item.status == 'Đã phục vụ');

  bool get _isTableActive =>
      currentTable.isTakeaway || currentTable.status == TableStatus.occupied;

  @override
  void initState() {
    super.initState();
    currentTable = widget.table;
    orderItems = widget.initialOrderItems
        .where((item) => item.status != 'Đã hủy')
        .toList();
    currentAmount = orderItems.fold<int>(
      0,
      (sum, item) => sum + item.totalPrice,
    );
    isPaymentRequested = widget.initialPaymentRequested;

    unawaited(_loadMemberCustomer());
    if (_canRequestPayment && !_canCompletePayment) {
      unawaited(_loadPaymentRequestLock());
    }

    _dishStatusSubscription = AppServices.realtimeService.dishStatusChanges
        .listen(_handleDishStatus);

    _paymentSubscription = AppServices.realtimeService.paymentCompletions
        .listen(_handlePaymentCompleted);

    _tableLockTakeoverSubscription =
        AppServices.realtimeService.tableLockTakeovers
            .listen(_handleTableLockTakenOver);

    final String token = widget.tableLockToken?.trim() ?? '';
    if (token.isNotEmpty) {
      _tableLockHeartbeatTimer = Timer.periodic(
        const Duration(seconds: 15),
        (_) => unawaited(_heartbeatTableLock()),
      );
    }
  }

  @override
  void dispose() {
    _tableLockHeartbeatTimer?.cancel();
    _paymentRequestLockTimer?.cancel();
    _dishStatusSubscription?.cancel();
    _paymentSubscription?.cancel();
    _tableLockTakeoverSubscription?.cancel();
    super.dispose();
  }

  Future<void> _loadPaymentRequestLock() async {
    try {
      final PaymentRequestLockState state =
          await AppServices.paymentService.getPaymentRequestLockState(
        tableId: currentTable.id,
      );

      if (!mounted) return;

      setState(() {
        isPaymentRequested =
            isPaymentRequested || state.hasRequest;
        _paymentRequestLocked = state.isLocked;
        _paymentRequestRemainingSeconds =
            state.remainingSeconds;
      });

      if (state.isLocked &&
          state.remainingSeconds > 0) {
        _startPaymentRequestLockTimer();
      }
    } catch (_) {
      // Không chặn màn bàn nếu endpoint trạng thái khóa tạm mất kết nối.
    }
  }

  void _startPaymentRequestLockTimer() {
    _paymentRequestLockTimer?.cancel();

    if (_paymentRequestRemainingSeconds <= 0) {
      if (mounted) {
        setState(() => _paymentRequestLocked = false);
      }
      return;
    }

    _paymentRequestLockTimer = Timer.periodic(
      const Duration(seconds: 1),
      (timer) {
        if (!mounted) {
          timer.cancel();
          return;
        }

        if (_paymentRequestRemainingSeconds <= 1) {
          timer.cancel();
          setState(() {
            _paymentRequestRemainingSeconds = 0;
            _paymentRequestLocked = false;
          });
          return;
        }

        setState(() {
          _paymentRequestRemainingSeconds -= 1;
        });
      },
    );
  }

  String get _paymentRequestCountdownLabel {
    final int seconds = _paymentRequestRemainingSeconds;
    final int minutes = seconds ~/ 60;
    final int remain = seconds % 60;
    return '$minutes:${remain.toString().padLeft(2, '0')}';
  }
  Future<void> _heartbeatTableLock() async {
    final String token = widget.tableLockToken?.trim() ?? '';
    if (token.isEmpty || _closingAfterPayment || _tableLockLost) return;

    try {
      await AppServices.tableLockService.heartbeat(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
        clientToken: token,
      );
    } on ApiException catch (error) {
      if (!mounted || error.statusCode != 409) return;

      _tableLockLost = true;
      _tableLockLostMessage = error.message;
      _tableLockHeartbeatTimer?.cancel();

      await _showLockLostAndClose();
    }
  }

  Future<void> _showLockLostAndClose() async {
    if (!mounted ||
        _closingAfterPayment ||
        _tableLockLostDialogShowing) {
      return;
    }

    _tableLockLostDialogShowing = true;

    try {
      // Nếu nhân viên đang mở menu/dialog con từ màn chi tiết bàn thì đóng các
      // route con trước. Phiên cũ không được tiếp tục thao tác sau khi bàn đã
      // được tài khoản khác tiếp quản.
      final Route<dynamic>? detailRoute = ModalRoute.of(context);
      if (detailRoute != null && !detailRoute.isCurrent) {
        Navigator.of(context).popUntil((route) => route == detailRoute);
        await Future<void>.delayed(const Duration(milliseconds: 120));
      }

      if (!mounted || _closingAfterPayment) return;

      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Bàn đã được tài khoản khác tiếp quản'),
          content: Text(
            _tableLockLostMessage.isEmpty
                ? 'Quyền thao tác bàn của bạn đã kết thúc.'
                : _tableLockLostMessage,
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('OK'),
            ),
          ],
        ),
      );

      await Future<void>.delayed(const Duration(milliseconds: 120));
      if (mounted && !_closingAfterPayment) {
        Navigator.of(context).pop(_result());
      }
    } finally {
      _tableLockLostDialogShowing = false;
    }
  }

  void _handleTableLockTakenOver(
    TableLockTakenOverRealtimeEvent event,
  ) {
    if (!mounted ||
        _closingAfterPayment ||
        event.tableId != currentTable.id) {
      return;
    }

    final String currentToken = widget.tableLockToken?.trim() ?? '';
    if (currentToken.isEmpty) return;

    // Cùng một tài khoản có thể đăng nhập nhiều thiết bị. So token để chỉ
    // đúng thiết bị đang giữ lease cũ bị đẩy ra.
    if (event.previousClientToken.trim().isNotEmpty &&
        event.previousClientToken.trim() != currentToken) {
      return;
    }

    final String employeeName = event.takenOverByEmployeeName.trim();
    final String employeeCode = event.takenOverByEmployeeCode.trim();
    String account = employeeName.isNotEmpty ? employeeName : employeeCode;

    if (employeeCode.isNotEmpty &&
        employeeName.isNotEmpty &&
        employeeCode.toLowerCase() != employeeName.toLowerCase()) {
      account = '$employeeName ($employeeCode)';
    }

    if (account.isEmpty) {
      account = 'một tài khoản khác';
    }

    _tableLockLost = true;
    _tableLockLostMessage =
        'Tài khoản $account đã tiếp quản ${_displayTableName()}. '
        'Bạn không thể tiếp tục thao tác trên bàn này.';
    _tableLockHeartbeatTimer?.cancel();

    unawaited(_showLockLostAndClose());
  }

  Map<String, dynamic> _result({
    bool transferTable = false,
    bool mergeTable = false,
    bool linkTables = false,
    bool splitTable = false,
    bool releaseTable = false,
    bool tableCleared = false,
  }) {
    return {
      'amount': currentAmount,
      'orderItems': orderItems,
      'paymentRequested': isPaymentRequested,
      'paymentCompleted': paymentCompleted,
      'guestCount': currentTable.guestCount,
      'transferTable': transferTable,
      'mergeTable': mergeTable,
      'linkTables': linkTables,
      'splitTable': splitTable,
      'releaseTable': releaseTable,
      'tableCleared': tableCleared,
      'tableActivated': _isTableActive,
    };
  }

  void _showRoleDenied(String action) {
    TopMessage.show(
      context,
      message:
          'Vai trò ${appEmployee.role} không có quyền $action trên Mobile.',
      type: TopMessageType.warning,
    );
  }

  void _handleDishStatus(DishStatusRealtimeEvent event) {
    if (!mounted || event.tableId != currentTable.id) {
      return;
    }

    final int index = orderItems.indexWhere(
      (item) => item.id == event.orderItemId,
    );

    if (index < 0) {
      return;
    }

    final OrderItem oldItem = orderItems[index];

    setState(() {
      if (event.status == 'Đã hủy') {
        orderItems.removeAt(index);
      } else {
        orderItems[index] = OrderItem(
          id: oldItem.id,
          name: oldItem.name,
          quantity: oldItem.quantity,
          unitPrice: oldItem.unitPrice,
          status: event.status,
          note: oldItem.note,
          isTakeaway: oldItem.isTakeaway,
        );
      }

      currentAmount = orderItems.fold<int>(
        0,
        (sum, item) => sum + item.totalPrice,
      );
    });
  }

  void _handlePaymentCompleted(PaymentCompletedRealtimeEvent event) {
    if (!mounted || event.tableId != currentTable.id) {
      return;
    }

    unawaited(_finishPaymentAndClose());
  }

  Future<void> _finishPaymentAndClose() async {
    if (!mounted || _closingAfterPayment) {
      return;
    }

    // Không rebuild/hiện thông báo trên route đang bị đóng.
    _closingAfterPayment = true;
    paymentCompleted = true;
    isPaymentRequested = false;
    _paymentRequestLocked = false;
    _paymentRequestRemainingSeconds = 0;
    _paymentRequestLockTimer?.cancel();
    orderItems.clear();
    currentAmount = 0;

    // Chặn heartbeat/realtime chạm vào State trong lúc route bị tháo.
    _tableLockHeartbeatTimer?.cancel();
    await _dishStatusSubscription?.cancel();
    _dishStatusSubscription = null;
    await _paymentSubscription?.cancel();
    _paymentSubscription = null;

    // Realtime thanh toán có thể đến trong lúc PaymentDialog vẫn đang đóng.
    // Chờ route chi tiết trở lại current trước khi pop để không pop nhầm dialog.
    for (int attempt = 0; attempt < 8; attempt++) {
      if (!mounted) return;
      if (ModalRoute.of(context)?.isCurrent == true) break;
      await Future<void>.delayed(const Duration(milliseconds: 120));
    }

    if (!mounted || ModalRoute.of(context)?.isCurrent != true) return;
    Navigator.of(context).pop(_result());
  }

  String _displayTableName() {
    if (currentTable.isTakeaway) {
      return 'Đơn ${currentTable.name}';
    }
    return currentTable.name;
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

  Future<bool> _activateTable({bool showSuccess = true}) async {
    if (_isTableActive) return true;

    if (!_canManageTables) {
      _showRoleDenied('mở bàn');
      return false;
    }

    try {
      await AppServices.tableService.receiveTable(
        tableId: currentTable.id,
        customerCount: 0,
      );

      if (!mounted) return false;

      setState(() {
        currentTable = currentTable.copyWith(
          status: TableStatus.occupied,
          amount: 0,
          clearGuestCount: true,
          clearCustomerName: true,
          clearTime: true,
        );
      });

      if (showSuccess) {
        TopMessage.show(
          context,
          message: 'Đã mở ${currentTable.name}',
          type: TopMessageType.success,
        );
      }

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

  void _editGuestCount() {
    if (currentTable.isTakeaway) return;

    if (!_canManageTables) {
      _showRoleDenied('cập nhật số khách');
      return;
    }

    setState(() {
      _guestCountDraft = currentTable.guestCount ?? 1;
      if (_guestCountDraft < 1) _guestCountDraft = 1;
      final int? capacity = currentTable.capacity;
      if (capacity != null && capacity > 0 && _guestCountDraft > capacity) {
        _guestCountDraft = capacity;
      }
      _guestCountEditorOpen = true;
    });
  }

  void _closeGuestCountEditor() {
    if (!_guestCountEditorOpen || _savingGuestCount) return;
    setState(() => _guestCountEditorOpen = false);
  }

  void _changeGuestCountDraft(int delta) {
    if (_savingGuestCount) return;
    final int max =
        (currentTable.capacity != null && currentTable.capacity! > 0)
        ? currentTable.capacity!
        : 99;
    final int next = (_guestCountDraft + delta).clamp(1, max);
    if (next == _guestCountDraft) return;
    setState(() => _guestCountDraft = next);
  }

  Future<void> _applyGuestCount(int result) async {
    if (_savingGuestCount) return;

    // This editor lives inside TableDetailV2 itself. There is no dialog route,
    // TextField controller, Overlay entry or Navigator.pop involved here.
    // That avoids the Flutter InheritedElement `_dependents.isEmpty` crash
    // that happened while removing the old guest-count AlertDialog.
    FocusManager.instance.primaryFocus?.unfocus();
    setState(() {
      _guestCountEditorOpen = false;
      _savingGuestCount = true;
    });

    try {
      // Xóa số khách khi bàn vẫn Trống chỉ thay đổi UI cục bộ, không mở bàn.
      if (result <= 0 && !_isTableActive) {
        if (!mounted) return;
        setState(() {
          currentTable = currentTable.copyWith(clearGuestCount: true);
        });
        TopMessage.show(
          context,
          message: 'Đã xóa số khách. Bàn vẫn đang ở trạng thái Trống',
          type: TopMessageType.success,
        );
        return;
      }

      final bool wasInactive = !_isTableActive;
      if (result > 0 && wasInactive) {
        final bool activated = await _activateTable(showSuccess: false);
        if (!activated || !mounted) return;
      }

      try {
        final int? saved = await AppServices.tableService.updateGuestCount(
          tableId: currentTable.id,
          guestCount: result > 0 ? result : null,
        );

        if (!mounted) return;

        setState(() {
          currentTable = currentTable.copyWith(
            guestCount: saved,
            clearGuestCount: saved == null,
          );
        });

        TopMessage.show(
          context,
          message: saved == null
              ? 'Đã xóa số khách'
              : 'Đã cập nhật $saved khách',
          type: TopMessageType.success,
        );
      } on ApiException {
        // Nếu chỉ vừa mở bàn để lưu số khách nhưng lưu thất bại, trả lại Trống
        // để không tạo bàn Đang dùng - 0đ ngoài ý muốn.
        if (wasInactive) {
          try {
            await AppServices.tableService.releaseTable(
              tableId: currentTable.id,
            );
            if (mounted) {
              setState(() {
                currentTable = currentTable.copyWith(
                  status: TableStatus.empty,
                  clearAmount: true,
                  clearGuestCount: true,
                  clearCustomerName: true,
                  clearTime: true,
                );
              });
            }
          } catch (_) {}
        }
        rethrow;
      }
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() => _savingGuestCount = false);
      }
    }
  }

  Widget _buildGuestCountEditor() {
    final int? capacity = currentTable.capacity;
    final int max = (capacity != null && capacity > 0) ? capacity : 99;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppTheme.orangeColor.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Số lượng khách',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                ),
              ),
              Text(
                'Tối đa $max khách',
                style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              IconButton.filledTonal(
                onPressed: _savingGuestCount || _guestCountDraft <= 1
                    ? null
                    : () => _changeGuestCountDraft(-1),
                icon: const Icon(Icons.remove),
              ),
              Container(
                constraints: const BoxConstraints(minWidth: 76),
                padding: const EdgeInsets.symmetric(horizontal: 18),
                alignment: Alignment.center,
                child: Text(
                  '$_guestCountDraft',
                  style: const TextStyle(
                    fontSize: 26,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              IconButton.filledTonal(
                onPressed: _savingGuestCount || _guestCountDraft >= max
                    ? null
                    : () => _changeGuestCountDraft(1),
                icon: const Icon(Icons.add),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              TextButton(
                onPressed: _savingGuestCount ? null : _closeGuestCountEditor,
                child: const Text('Hủy'),
              ),
              const Spacer(),
              TextButton(
                onPressed: _savingGuestCount ? null : () => _applyGuestCount(0),
                style: TextButton.styleFrom(foregroundColor: Colors.red),
                child: const Text('Xóa số khách'),
              ),
              const SizedBox(width: 8),
              FilledButton(
                onPressed: _savingGuestCount
                    ? null
                    : () => _applyGuestCount(_guestCountDraft),
                child: const Text('Lưu'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Future<bool> _addItems(List<CartItem> cartItems) async {
    if (!_canOrder) {
      _showRoleDenied('gọi món');
      return false;
    }

    try {
      final List<OrderItem> added = await AppServices.orderService.addItems(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
        items: cartItems,
      );

      if (!mounted) return false;

      setState(() {
        // /orders/add-items cập nhật DiningTables -> DangPhucVu trong cùng
        // transaction. Chỉ sau khi lưu món thành công mới đổi trạng thái UI.
        currentTable = currentTable.copyWith(
          status: TableStatus.occupied,
          amount: 0,
          clearTime: true,
        );
        orderItems.addAll(added);
        currentAmount = orderItems.fold<int>(
          0,
          (sum, item) => sum + item.totalPrice,
        );
      });

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

  Future<void> _openMenu() async {
    if (!_canOrder) {
      _showRoleDenied('gọi món');
      return;
    }

    final List<CartItem>? newItems = await Navigator.push<List<CartItem>>(
      context,
      MaterialPageRoute(
        builder: (context) => MenuScreen(
          table: currentTable,
          notifications: widget.notifications,
        ),
      ),
    );

    if (!mounted || newItems == null || newItems.isEmpty) {
      return;
    }

    final bool success = await _addItems(newItems);
    if (!mounted || !success) return;

    TopMessage.show(
      context,
      message: 'Gọi món thành công',
      type: TopMessageType.success,
    );
  }

  Future<void> _markAsServed(int index) async {
    if (!_canMarkServed) {
      _showRoleDenied('xác nhận đã phục vụ');
      return;
    }

    final OrderItem item = orderItems[index];
    if (item.status != 'Đã xong' || item.id == null) {
      return;
    }

    try {
      final String status = await AppServices.orderService.updateItemStatus(
        orderItemId: item.id!,
        status: 'Đã phục vụ',
      );

      if (!mounted) return;

      setState(() {
        final int currentIndex = orderItems.indexWhere(
          (current) => current.id == item.id,
        );
        if (currentIndex < 0) return;

        final OrderItem current = orderItems[currentIndex];
        orderItems[currentIndex] = OrderItem(
          id: current.id,
          name: current.name,
          quantity: current.quantity,
          unitPrice: current.unitPrice,
          status: status,
          note: current.note,
          isTakeaway: current.isTakeaway,
        );
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _markAllAsServed() async {
    if (!_canMarkServed || _bulkActionRunning) {
      return;
    }

    final List<int> ids = orderItems
        .where((item) => item.status == 'Đã xong' && item.id != null)
        .map((item) => item.id!)
        .toList();

    if (ids.isEmpty) {
      return;
    }

    setState(() => _bulkActionRunning = true);

    try {
      final Set<int> updatedIds = await AppServices.orderService
          .markItemsServed(orderItemIds: ids);

      if (!mounted) return;

      setState(() {
        orderItems = orderItems.map((item) {
          if (item.id == null || !updatedIds.contains(item.id)) {
            return item;
          }

          return OrderItem(
            id: item.id,
            name: item.name,
            quantity: item.quantity,
            unitPrice: item.unitPrice,
            status: 'Đã phục vụ',
            note: item.note,
            isTakeaway: item.isTakeaway,
          );
        }).toList();
      });

      TopMessage.show(
        context,
        message: 'Đã xác nhận phục vụ ${updatedIds.length} món',
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
        setState(() => _bulkActionRunning = false);
      }
    }
  }

  Future<void> _editItem(OrderItem item) async {
    if (!_canCancelOrderItem ||
        item.id == null ||
        item.status != 'Chờ chế biến' ||
        _bulkActionRunning) {
      return;
    }

    final int? savedQuantity =
        await Navigator.push<int>(
      context,
      MaterialPageRoute<int>(
        builder: (_) =>
            OrderItemEditScreen(
          item: item,
        ),
      ),
    );

    if (!mounted ||
        savedQuantity == null) {
      return;
    }

    final int index = orderItems
        .indexWhere(
      (current) =>
          current.id == item.id,
    );

    if (index < 0) return;

    final OrderItem current =
        orderItems[index];

    setState(() {
      orderItems[index] = OrderItem(
        id: current.id,
        name: current.name,
        quantity: savedQuantity,
        unitPrice: current.unitPrice,
        status: current.status,
        note: current.note,
        isTakeaway:
            current.isTakeaway,
      );

      currentAmount =
          orderItems.fold<int>(
        0,
        (sum, current) =>
            sum + current.totalPrice,
      );
    });
  }

  Future<void> _cancelItem(OrderItem item) async {
    if (!_canCancelOrderItem || item.id == null) {
      return;
    }

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Hủy món'),
        content: Text('Hủy "${item.name}"?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Không'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Hủy món'),
          ),
        ],
      ),
    );

    if (!mounted || confirmed != true) return;

    try {
      await AppServices.orderService.cancelItem(orderItemId: item.id!);

      if (!mounted) return;

      setState(() {
        orderItems.removeWhere((current) => current.id == item.id);
        currentAmount = orderItems.fold<int>(
          0,
          (sum, current) => sum + current.totalPrice,
        );
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _requestPayment() async {
    if (!_canRequestPayment) {
      _showRoleDenied('gửi yêu cầu thanh toán');
      return;
    }

        if (!allItemsServed) {
      // Bàn ăn tại chỗ: vẫn bắt buộc phục vụ đủ món.
      if (!currentTable.isTakeaway) {
        TopMessage.show(
          context,
          message: 'Tất cả món phải được phục vụ trước khi yêu cầu thanh toán.',
          type: TopMessageType.warning,
        );
        return;
      }

      // Đơn mang về: cho thanh toán sớm nhưng hỏi xác nhận.
      final int pendingCount = orderItems
          .where((item) => item.status != 'Đã phục vụ')
          .length;

      final bool? confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Thanh toán sớm'),
          content: Text(
            'Còn $pendingCount món chưa được phục vụ. Đơn mang về có thể '
            'thanh toán trước, bếp vẫn tiếp tục chế biến sau khi khách trả tiền.\n\n'
            'Gửi yêu cầu thanh toán?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Chưa'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('Gửi yêu cầu'),
            ),
          ],
        ),
      );

      if (!mounted || confirmed != true) return;
    }

    try {
      final int lockSeconds =
          await AppServices.paymentService.requestPayment(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
      );

      if (!mounted) return;

      setState(() {
        isPaymentRequested = true;
        _paymentRequestLocked = true;
        _paymentRequestRemainingSeconds = lockSeconds;
      });
      _startPaymentRequestLockTimer();

      TopMessage.show(
        context,
        message: 'Đã gửi yêu cầu đến Thu ngân',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      if (error.statusCode == 409) {
        await _loadPaymentRequestLock();
        if (!mounted) return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: error.statusCode == 409
            ? TopMessageType.warning
            : TopMessageType.error,
      );
    }
  }

  Future<void> _openReceiptPreview() async {
    if (!_canCompletePayment) {
      _showRoleDenied('xem bill / mã QR thanh toán');
      return;
    }

    if (orderItems.isEmpty || _closingAfterPayment) {
      return;
    }

    try {
      final PaymentPreview preview =
          await AppServices.paymentService
              .getPreview(
        tableId: currentTable.id,
      );

      if (!mounted) return;

      final List<ReceiptLineView> lines =
          orderItems
              .where(
                (item) =>
                    item.status != 'Đã hủy',
              )
              .map(
                (item) => ReceiptLineView(
                  name: item.name,
                  quantity: item.quantity,
                  unitPrice: item.unitPrice,
                  lineTotal: item.totalPrice,
                ),
              )
              .toList();

      await Navigator.push<void>(
        context,
        MaterialPageRoute(
          builder: (_) =>
              ReceiptPreviewScreen(
            title: 'PHIẾU TẠM TÍNH',
            referenceId:
                preview.orderId,
            tableName:
                _displayTableName(),
            waiterName:
                preview.waiterName
                        .trim()
                        .isNotEmpty
                    ? preview.waiterName
                    : appEmployee
                        .fullName,
            orderTime:
                preview.orderTime,
            paymentTime: null,
            items: lines,
            subTotal:
                preview.subTotal,
            discountAmount:
                preview.discountAmount,
            totalAmount:
                preview.totalAmount,
            shopName:
                preview.shopName,
            shopAddress:
                preview.shopAddress,
            bankName:
                preview.bankName,
            bankAccountNo:
                preview.bankAccountNo,
            bankAccountName:
                preview.bankAccountName,
            bankBranch:
                preview.bankBranch,
            transferContent:
                preview.transferContent,
            qrImageUrl:
                preview.qrImageUrl,
          ),
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _confirmPayment() async {
    if (_closingAfterPayment) return;

    if (!_canCompletePayment) {
      _showRoleDenied('xác nhận thanh toán');
      return;
    }

    if (!currentTable.isTakeaway && !allItemsServed) {
      TopMessage.show(
        context,
        message: 'Vẫn còn món chưa được phục vụ',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      final PaymentPreview preview = await AppServices.paymentService
          .getPreview(tableId: currentTable.id);

      if (!mounted || _closingAfterPayment) return;

      final DialogRoute<PaymentDialogResult> paymentRoute =
          DialogRoute<PaymentDialogResult>(
            context: context,
            barrierDismissible: false,
            builder: (_) => PaymentDialog(preview: preview),
          );

      final PaymentDialogResult? payment = await Navigator.of(
        context,
      ).push<PaymentDialogResult>(paymentRoute);

      // Chỉ tiếp tục sau khi dialog + overlay đã gỡ hoàn toàn.
      await paymentRoute.completed;

      if (!mounted || payment == null || _closingAfterPayment) {
        return;
      }

      await AppServices.paymentService.completePayment(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
        paymentMethod: payment.method,
        transactionRef: payment.transactionRef,
      );

      if (!mounted) return;
      await _finishPaymentAndClose();
    } on ApiException catch (error) {
      if (!mounted || _closingAfterPayment) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _setTablePriority() async {
    if (!_canOrder) {
      _showRoleDenied('ưu tiên bàn');
      return;
    }

    try {
      await AppServices.referenceOpsService.setTablePriority(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
        active: true,
      );
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Đã gửi yêu cầu ưu tiên ${_displayTableName()} xuống bếp',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _setItemPriority(OrderItem item) async {
    if (!_canOrder || item.id == null) {
      _showRoleDenied('ưu tiên món');
      return;
    }

    try {
      await AppServices.referenceOpsService.setItemPriority(
        orderItemId: item.id!,
        employeeCode: appEmployee.employeeCode,
        active: true,
      );
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Đã ưu tiên món ${item.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _openKitchenRequests() async {
    if (!_canOrder || _closingAfterPayment || _tableLockLost) {
      if (!_canOrder) {
        _showRoleDenied('gửi yêu cầu xuống bếp');
      }
      return;
    }

    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => KitchenRequestsScreen(
          table: currentTable,
          initialItems: orderItems,
        ),
      ),
    );

    if (!mounted) return;
    if (_tableLockLost) {
      await _showLockLostAndClose();
    }
  }

  Future<void> _loadMemberCustomer() async {
    try {
      final MemberCustomer? member = await AppServices.customerService
          .getForTable(currentTable.id);
      if (!mounted) return;
      setState(() => _memberCustomer = member);
    } catch (_) {
      // Không để lỗi tải thành viên chặn màn chi tiết bàn.
    }
  }

  Future<void> _openCustomerMembership() async {
    if (_closingAfterPayment || _tableLockLost) return;

    if (!_isTableActive) {
      TopMessage.show(
        context,
        message:
            'Hãy mở bàn, nhập số khách hoặc gọi món trước khi gắn thành viên.',
        type: TopMessageType.warning,
      );
      return;
    }

    final TextEditingController phoneController = TextEditingController();
    List<MemberCustomer> results = <MemberCustomer>[];
    bool loading = false;

    final MemberCustomer? selected = await showModalBottomSheet<MemberCustomer>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            Future<void> search() async {
              final String phone = phoneController.text.trim();
              if (phone.isEmpty) return;
              setSheetState(() => loading = true);
              try {
                final List<MemberCustomer> found = await AppServices
                    .customerService
                    .searchByPhone(phone);
                if (!sheetContext.mounted) return;
                setSheetState(() {
                  results = found;
                  loading = false;
                });
              } on ApiException catch (error) {
                if (!sheetContext.mounted) return;
                setSheetState(() => loading = false);
                TopMessage.show(
                  sheetContext,
                  message: error.message,
                  type: TopMessageType.error,
                );
              }
            }

            return Padding(
              padding: EdgeInsets.fromLTRB(
                20,
                4,
                20,
                20 + MediaQuery.viewInsetsOf(sheetContext).bottom,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Expanded(
                        child: Text(
                          'Khách hàng / Thành viên',
                          style: TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      IconButton(
                        onPressed: () => Navigator.pop(sheetContext),
                        icon: const Icon(Icons.close),
                      ),
                    ],
                  ),
                  if (_memberCustomer != null) ...[
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.green.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        'Đang gắn: ${_memberCustomer!.fullName} • ${_memberCustomer!.phone} • ${_memberCustomer!.memberLevel}',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                    const SizedBox(height: 12),
                  ],
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: phoneController,
                          keyboardType: TextInputType.phone,
                          decoration: const InputDecoration(
                            labelText: 'Số điện thoại khách',
                            hintText: 'Nhập SĐT để tìm thành viên',
                            border: OutlineInputBorder(),
                            prefixIcon: Icon(Icons.phone_outlined),
                          ),
                          onSubmitted: (_) => search(),
                        ),
                      ),
                      const SizedBox(width: 10),
                      FilledButton(
                        onPressed: loading ? null : search,
                        child: loading
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Text('Tìm'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (results.isNotEmpty)
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 230),
                      child: ListView.separated(
                        shrinkWrap: true,
                        itemCount: results.length,
                        separatorBuilder: (_, __) => const Divider(height: 1),
                        itemBuilder: (_, index) {
                          final MemberCustomer member = results[index];
                          return ListTile(
                            leading: const CircleAvatar(
                              child: Icon(Icons.person_outline),
                            ),
                            title: Text(member.fullName),
                            subtitle: Text(
                              '${member.phone} • ${member.memberLevel} • ${member.loyaltyPoint} điểm',
                            ),
                            trailing: const Icon(Icons.chevron_right),
                            onTap: () => Navigator.pop(sheetContext, member),
                          );
                        },
                      ),
                    ),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                      onPressed: () async {
                        final String phone = phoneController.text.trim();
                        final MemberCustomer? created =
                            await _createMemberDialog(phone);
                        if (created != null && sheetContext.mounted) {
                          Navigator.pop(sheetContext, created);
                        }
                      },
                      icon: const Icon(Icons.person_add_alt_1_outlined),
                      label: const Text('Tạo thành viên mới'),
                    ),
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    phoneController.dispose();
    if (!mounted || selected == null) return;

    try {
      final MemberCustomer attached = await AppServices.customerService
          .attachToTable(
            tableId: currentTable.id,
            customerId: selected.id,
            employeeCode: appEmployee.employeeCode,
          );
      if (!mounted) return;
      setState(() => _memberCustomer = attached);
      TopMessage.show(
        context,
        message: 'Đã gắn ${attached.fullName} vào đơn',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<MemberCustomer?> _createMemberDialog(String initialPhone) async {
    final TextEditingController nameController = TextEditingController();
    final TextEditingController phoneController = TextEditingController(
      text: initialPhone,
    );

    final MemberCustomer? result = await showDialog<MemberCustomer>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Tạo thành viên mới'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: nameController,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(
                labelText: 'Tên khách hàng',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: phoneController,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(
                labelText: 'Số điện thoại',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () async {
              final String name = nameController.text.trim();
              final String phone = phoneController.text.trim();
              if (name.isEmpty || phone.isEmpty) {
                TopMessage.show(
                  dialogContext,
                  message: 'Vui lòng nhập tên và số điện thoại',
                  type: TopMessageType.warning,
                );
                return;
              }
              try {
                final MemberCustomer created = await AppServices.customerService
                    .create(fullName: name, phone: phone);
                if (dialogContext.mounted) {
                  Navigator.pop(dialogContext, created);
                }
              } on ApiException catch (error) {
                if (!dialogContext.mounted) return;
                TopMessage.show(
                  dialogContext,
                  message: error.message,
                  type: TopMessageType.error,
                );
              }
            },
            child: const Text('Tạo thành viên'),
          ),
        ],
      ),
    );

    nameController.dispose();
    phoneController.dispose();
    return result;
  }

  Future<void> _clearTableAndRelease() async {
    if (!_canManageTables || _bulkActionRunning || _closingAfterPayment) {
      if (!_canManageTables) {
        _showRoleDenied('xóa món và trả bàn');
      }
      return;
    }

    final int blockedCount = orderItems
        .where((item) => item.status != 'Chờ chế biến')
        .length;

    if (blockedCount > 0) {
      TopMessage.show(
        context,
        message:
            'Có $blockedCount món bếp đã bắt đầu xử lý hoặc đã phục vụ. Chỉ món Chờ chế biến mới có thể xóa.',
        type: TopMessageType.warning,
        duration: const Duration(milliseconds: 2400),
      );
      return;
    }

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(
          currentTable.isTakeaway
              ? 'Xóa đơn mang về'
              : 'Xóa tất cả món và trả bàn',
        ),
        content: Text(
          orderItems.isEmpty
              ? 'Đưa ${_displayTableName()} về trạng thái Trống?'
              : 'Hủy toàn bộ ${orderItems.length} món giống thao tác nút X, sau đó đưa ${_displayTableName()} về Trống?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Không'),
          ),
          FilledButton.icon(
            onPressed: () => Navigator.pop(dialogContext, true),
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            icon: const Icon(Icons.delete_outline),
            label: const Text('Xóa tất cả'),
          ),
        ],
      ),
    );

    if (!mounted || confirmed != true) return;
    setState(() => _bulkActionRunning = true);

    try {
      await AppServices.orderService.clearTable(
        tableId: currentTable.id,
        employeeCode: appEmployee.employeeCode,
      );

      if (!mounted) return;
      _closingAfterPayment = true;
      _tableLockHeartbeatTimer?.cancel();
      await _dishStatusSubscription?.cancel();
      _dishStatusSubscription = null;
      await _paymentSubscription?.cancel();
      _paymentSubscription = null;

      orderItems.clear();
      currentAmount = 0;
      isPaymentRequested = false;

      if (!mounted) return;
      Navigator.of(context).pop(_result(tableCleared: true));
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _bulkActionRunning = false);
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } catch (_) {
      if (!mounted) return;
      setState(() => _bulkActionRunning = false);
      TopMessage.show(
        context,
        message: 'Không thể xóa tất cả món và trả bàn',
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _releaseTable() async {
    if (!_canManageTables) {
      _showRoleDenied('trả bàn');
      return;
    }

    if (orderItems.isNotEmpty) {
      TopMessage.show(
        context,
        message: 'Bàn đã có món. Hãy hoàn tất thanh toán trước.',
        type: TopMessageType.warning,
      );
      return;
    }

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(currentTable.isTakeaway ? 'Hủy đơn mang về' : 'Trả bàn'),
        content: Text(
          currentTable.isTakeaway
              ? 'Đóng ${currentTable.name} vì chưa có món?'
              : 'Đưa ${currentTable.name} về trạng thái trống?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Không'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Xác nhận'),
          ),
        ],
      ),
    );

    if (!mounted || confirmed != true) return;

    Navigator.pop(context, _result(releaseTable: true));
  }

  Future<void> _handleTableMenuAction(String value) async {
    if (_closingAfterPayment || _tableLockLost || _tableActionRunning) {
      return;
    }

    setState(() => _tableActionRunning = true);

    bool changed = false;
    try {
      switch (value) {
        case 'transfer':
          changed =
              await (widget.onTransferTable?.call(isPaymentRequested) ??
                  Future<bool>.value(false));
          break;
        case 'merge':
          changed =
              await (widget.onMergeTable?.call() ?? Future<bool>.value(false));
          break;
        case 'link':
          changed =
              await (widget.onLinkTables?.call() ?? Future<bool>.value(false));
          break;
        case 'split':
          changed =
              await (widget.onSplitTable?.call(
                    List<OrderItem>.from(orderItems),
                  ) ??
                  Future<bool>.value(false));
          break;
      }
    } finally {
      if (mounted) {
        setState(() => _tableActionRunning = false);
      }
    }

    if (!mounted || !changed) return;

    // Dialog chọn bàn được mở trên route chi tiết hiện tại. Chỉ sau khi thao
    // tác thành công mới đóng chi tiết để sơ đồ tải lại dữ liệu đã đồng bộ.
    Navigator.of(context).pop();
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'Đang chế biến':
        return Colors.orange;
      case 'Đã xong':
        return Colors.blue;
      case 'Đã phục vụ':
        return Colors.green;
      default:
        return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop || _closingAfterPayment) return;
        Navigator.pop(context, _result());
      },
      child: Scaffold(
        backgroundColor: const Color(0xFFF6F7F9),
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          leading: IconButton(
            onPressed: _closingAfterPayment
                ? null
                : () => Navigator.pop(context, _result()),
            icon: const Icon(Icons.arrow_back),
          ),
          title: Text(
            _displayTableName(),
            style: const TextStyle(
              color: AppTheme.textColor,
              fontWeight: FontWeight.bold,
              fontSize: 18,
            ),
          ),
          actions: [
            if (_canCompletePayment &&
                orderItems.isNotEmpty &&
                !_closingAfterPayment)
              IconButton(
                tooltip: 'Xem / in bill tạm tính',
                onPressed:
                    _openReceiptPreview,
                icon: const Icon(
                  Icons.print_outlined,
                ),
              ),
            if (_canOrder && orderItems.isNotEmpty && !_closingAfterPayment)
              IconButton(
                tooltip: 'Yêu cầu xuống bếp',
                onPressed: _openKitchenRequests,
                icon: const Icon(Icons.restaurant),
              ),
            if (_canOrder && _isTableActive && !_closingAfterPayment)
              IconButton(
                tooltip: 'Khách hàng / Thành viên',
                onPressed: _openCustomerMembership,
                icon: Icon(
                  _memberCustomer == null
                      ? Icons.person_add_alt_1_outlined
                      : Icons.person_outline,
                  color: _memberCustomer == null ? null : Colors.green,
                ),
              ),
            if (_canManageTables && !currentTable.isTakeaway)
              IconButton(
                tooltip: 'Nhật ký Order',
                onPressed: _closingAfterPayment
                    ? null
                    : () => Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => OrderLogScreen(
                            tableId: currentTable.id,
                            tableName: _displayTableName(),
                          ),
                        ),
                      ),
                icon: const Icon(Icons.history_outlined),
              ),
            if (_canManageTables && _isTableActive)
              IconButton(
                tooltip: currentTable.isTakeaway
                    ? 'Xóa toàn bộ món và hủy đơn'
                    : 'Xóa toàn bộ món và trả bàn',
                onPressed: _closingAfterPayment ? null : _clearTableAndRelease,
                icon: const Icon(Icons.delete_outline, color: Colors.redAccent),
              ),
            if (_canManageTables && !currentTable.isTakeaway)
              PopupMenuButton<String>(
                tooltip: 'Thao tác bàn',
                enabled: _isTableActive && !_tableActionRunning,
                onSelected: _handleTableMenuAction,
                itemBuilder: (context) => const [
                  PopupMenuItem(value: 'transfer', child: Text('Chuyển bàn')),
                  PopupMenuItem(value: 'merge', child: Text('Gộp đơn')),
                  PopupMenuItem(value: 'link', child: Text('Ghép bàn')),
                  PopupMenuItem(value: 'split', child: Text('Tách bàn')),
                ],
              ),
          ],
        ),
        body: Column(
          children: [
            Expanded(
              child: RefreshIndicator(
                onRefresh: () async {},
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    _buildTableInfo(),
                    if (_guestCountEditorOpen) ...[
                      const SizedBox(height: 10),
                      _buildGuestCountEditor(),
                    ],
                    const SizedBox(height: 14),
                    if (orderItems.isEmpty)
                      _buildEmptyOrder()
                    else
                      ...orderItems.asMap().entries.map(
                        (entry) => Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: _buildOrderItem(entry.key, entry.value),
                        ),
                      ),
                    const SizedBox(height: 4),
                    _buildSummary(),
                  ],
                ),
              ),
            ),
            _buildActions(),
          ],
        ),
      ),
    );
  }

  Widget _buildTableInfo() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Icon(
                currentTable.isTakeaway
                    ? Icons.shopping_bag_outlined
                    : Icons.table_restaurant_outlined,
                color: AppTheme.orangeColor,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      currentTable.isTakeaway
                          ? 'Đơn mang về'
                          : (currentTable.areaName ?? 'Tại quán'),
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                      ),
                    ),
                    if (currentTable.time != null)
                      Text(
                        'Bắt đầu ${currentTable.time}',
                        style: TextStyle(
                          color: Colors.grey.shade600,
                          fontSize: 12,
                        ),
                      ),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color:
                      (_isTableActive
                              ? AppTheme.orangeColor
                              : const Color(0xFF2E9B65))
                          .withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  _isTableActive ? 'Đang dùng' : 'Trống',
                  style: TextStyle(
                    color: _isTableActive
                        ? AppTheme.orangeColor
                        : const Color(0xFF2E9B65),
                    fontWeight: FontWeight.w600,
                    fontSize: 12,
                  ),
                ),
              ),
            ],
          ),
          if (!currentTable.isTakeaway) ...[
            const Divider(height: 28),
            InkWell(
              onTap: _savingGuestCount ? null : _editGuestCount,
              borderRadius: BorderRadius.circular(10),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    const Icon(
                      Icons.people_outline,
                      size: 20,
                      color: AppTheme.orangeColor,
                    ),
                    const SizedBox(width: 10),
                    const Text(
                      'Số khách',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                    const Spacer(),
                    if (_savingGuestCount)
                      const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else ...[
                      Text(
                        currentTable.guestCount == null
                            ? 'Chưa nhập'
                            : '${currentTable.guestCount} khách',
                        style: TextStyle(
                          color: currentTable.guestCount == null
                              ? Colors.grey.shade600
                              : AppTheme.textColor,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(width: 6),
                      const Icon(Icons.edit_outlined, size: 18),
                    ],
                  ],
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildEmptyOrder() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 34),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Column(
        children: [
          Icon(Icons.restaurant_menu, size: 42, color: Colors.grey.shade400),
          const SizedBox(height: 10),
          const Text(
            'Chưa gọi món',
            style: TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 4),
          Text(
            _isTableActive
                ? 'Có thể nhập số khách trước hoặc gọi món ngay.'
                : 'Bàn vẫn đang Trống. Nhập số khách, gọi món hoặc bấm Mở bàn để bắt đầu.',
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
          ),
        ],
      ),
    );
  }

  Widget _buildOrderItem(
    int index,
    OrderItem item,
  ) {
    final Color color =
        _statusColor(item.status);

    final bool canEdit =
        _canCancelOrderItem &&
        item.id != null &&
        item.status ==
            'Chờ chế biến' &&
        !_bulkActionRunning;

    return GestureDetector(
      behavior:
          HitTestBehavior.opaque,
      onTap:
          canEdit
              ? () => _editItem(item)
              : null,
      child: Container(
        padding:
            const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius:
              BorderRadius.circular(14),
          border: Border.all(
            color:
                canEdit
                    ? AppTheme
                        .orangeColor
                        .withValues(
                          alpha: 0.28,
                        )
                    : Colors
                        .grey.shade200,
          ),
        ),
        child: Row(
          crossAxisAlignment:
              CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment:
                    CrossAxisAlignment
                        .start,
                children: [
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          item.name,
                          style:
                              const TextStyle(
                            fontWeight:
                                FontWeight.w700,
                            fontSize: 15,
                          ),
                        ),
                      ),
                      if (item.isTakeaway &&
                          !currentTable
                              .isTakeaway) ...[
                        const SizedBox(
                          width: 8,
                        ),
                        Container(
                          padding:
                              const EdgeInsets
                                  .symmetric(
                            horizontal: 7,
                            vertical: 2,
                          ),
                          decoration:
                              BoxDecoration(
                            color: AppTheme
                                .orangeColor
                                .withValues(
                              alpha: 0.12,
                            ),
                            borderRadius:
                                BorderRadius
                                    .circular(
                              8,
                            ),
                          ),
                          child:
                              const Text(
                            'Mang về',
                            style:
                                TextStyle(
                              color: AppTheme
                                  .orangeColor,
                              fontSize: 11,
                              fontWeight:
                                  FontWeight
                                      .w700,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(
                    height: 5,
                  ),
                  Text(
                    'SL: ${item.quantity}  •  '
                    '${_formatMoney(item.totalPrice)}',
                    style: TextStyle(
                      color:
                          Colors.grey.shade700,
                      fontSize: 13,
                    ),
                  ),
                  if (canEdit) ...[
                    const SizedBox(
                      height: 5,
                    ),
                    const Text(
                      'Chạm vào món để sửa số lượng',
                      style: TextStyle(
                        color: AppTheme
                            .orangeColor,
                        fontSize: 11,
                        fontWeight:
                            FontWeight.w600,
                      ),
                    ),
                  ],
                  if (item.note != null &&
                      item.note!
                          .trim()
                          .isNotEmpty) ...[
                    const SizedBox(
                      height: 5,
                    ),
                    Text(
                      item.note!,
                      style: TextStyle(
                        color: Colors
                            .grey.shade600,
                        fontSize: 12,
                        fontStyle:
                            FontStyle.italic,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            Column(
              crossAxisAlignment:
                  CrossAxisAlignment.end,
              children: [
                Container(
                  padding:
                      const EdgeInsets
                          .symmetric(
                    horizontal: 9,
                    vertical: 5,
                  ),
                  decoration:
                      BoxDecoration(
                    color: color
                        .withValues(
                      alpha: 0.1,
                    ),
                    borderRadius:
                        BorderRadius
                            .circular(
                      14,
                    ),
                  ),
                  child: Text(
                    item.status,
                    style: TextStyle(
                      color: color,
                      fontSize: 11,
                      fontWeight:
                          FontWeight.w600,
                    ),
                  ),
                ),
                if (_canMarkServed &&
                    item.status ==
                        'Đã xong')
                  IconButton(
                    onPressed:
                        _bulkActionRunning
                            ? null
                            : () =>
                                _markAsServed(
                                  index,
                                ),
                    tooltip:
                        'Đã phục vụ',
                    icon: const Icon(
                      Icons
                          .check_circle_outline,
                      color: Colors.green,
                    ),
                  )
                else if (_canCancelOrderItem &&
                    item.status ==
                        'Chờ chế biến')
                  IconButton(
                    onPressed:
                        _bulkActionRunning
                            ? null
                            : () =>
                                _cancelItem(
                                  item,
                                ),
                    tooltip: 'Hủy món',
                    icon: const Icon(
                      Icons
                          .cancel_outlined,
                      color: Colors.red,
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSummary() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'TỔNG QUAN ĐƠN',
                  style: TextStyle(
                    color: Colors.grey.shade600,
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 5),
                Text(
                  '$totalQuantity món',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
              ],
            ),
          ),
          Text(
            _formatMoney(currentAmount),
            style: const TextStyle(
              color: AppTheme.orangeColor,
              fontSize: 17,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActions() {
    final int doneCount = orderItems
        .where((item) => item.status == 'Đã xong' && item.id != null)
        .length;
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 18),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: Colors.grey.shade200)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (_canOrder)
            SizedBox(
              width: double.infinity,
              height: 48,
              child: FilledButton.icon(
                onPressed: _bulkActionRunning || _closingAfterPayment
                    ? null
                    : _openMenu,
                style: FilledButton.styleFrom(
                  backgroundColor: AppTheme.orangeColor,
                ),
                icon: const Icon(Icons.add),
                label: Text(orderItems.isEmpty ? 'Gọi món' : 'Gọi thêm món'),
              ),
            ),
          if (_canOrder) const SizedBox(height: 9),
          if (_canMarkServed && doneCount >= 2) ...[
            SizedBox(
              width: double.infinity,
              height: 46,
              child: FilledButton.icon(
                onPressed: _bulkActionRunning || _closingAfterPayment
                    ? null
                    : _markAllAsServed,
                style: FilledButton.styleFrom(backgroundColor: Colors.green),
                icon: const Icon(Icons.done_all),
                label: Text('Đã phục vụ tất cả ($doneCount)'),
              ),
            ),
            const SizedBox(height: 9),
          ],
          if (orderItems.isEmpty && _canManageTables)
            SizedBox(
              width: double.infinity,
              height: 48,
              child: OutlinedButton(
                onPressed: _closingAfterPayment
                    ? null
                    : (_isTableActive
                          ? _releaseTable
                          : () async {
                              await _activateTable();
                            }),
                style: OutlinedButton.styleFrom(
                  foregroundColor: _isTableActive
                      ? Colors.red
                      : AppTheme.orangeColor,
                ),
                child: Text(
                  currentTable.isTakeaway
                      ? 'Hủy đơn mang về'
                      : (_isTableActive ? 'Trả bàn' : 'Mở bàn'),
                ),
              ),
            )
          else if (orderItems.isNotEmpty)
            SizedBox(
              width: double.infinity,
              height: 48,
              child: _canCompletePayment
                  ? FilledButton.icon(
                      onPressed: _bulkActionRunning || _closingAfterPayment
                          ? null
                          : _confirmPayment,
                      style: FilledButton.styleFrom(
                        backgroundColor: Colors.green,
                      ),
                      icon: const Icon(Icons.payments_outlined),
                      label: Text(
                        isPaymentRequested
                            ? 'Xác nhận thanh toán'
                            : 'Thanh toán',
                      ),
                    )
                  : _paymentRequestLocked
                  ? FilledButton(
                      onPressed: null,
                      child: Text(
                        'Đang chờ Thu ngân ($_paymentRequestCountdownLabel)',
                      ),
                    )
                  : OutlinedButton(
                      onPressed:
                          _canRequestPayment &&
                              !_bulkActionRunning &&
                              !_closingAfterPayment
                          ? _requestPayment
                          : null,
                      child: Text(
                        !_canRequestPayment
                            ? 'Không có quyền yêu cầu thanh toán'
                            : isPaymentRequested
                                ? 'Gọi thanh toán lại'
                                : 'Khách gọi thanh toán',
                      ),
                    ),
            ),
        ],
      ),
    );
  }
}