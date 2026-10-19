import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';

import '../../app/theme.dart';
import '../../models/restaurant_table.dart';
import '../menu/menu_screen.dart';
import '../../models/cart_item.dart';
import '../../models/order_item.dart';
import '../../models/app_notification.dart';
import '../../models/employee_profile.dart';
import '../../models/payment_preview.dart';
import '../../widgets/top_message.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../services/realtime_service.dart';
import '../../services/mobile_access_policy.dart';

class TableDetailScreen extends StatefulWidget {
  final RestaurantTable table;
  final List<OrderItem> initialOrderItems;
  final List<AppNotification> notifications;

  final bool initialPaymentRequested;

  const TableDetailScreen({
    super.key,
    required this.table,
    required this.initialOrderItems,
    required this.notifications,
    this.initialPaymentRequested = false,
  });

  @override
  State<TableDetailScreen> createState() => _TableDetailScreenState();
}

class _TableDetailScreenState extends State<TableDetailScreen> {
  bool isPaymentRequested = false;
  bool paymentCompleted = false;

  late List<OrderItem> orderItems;
  late int currentAmount;

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

  void _showRoleDenied(String action) {
    TopMessage.show(
      context,
      message:
          'Vai trò ${appEmployee.role} không có quyền $action trên Mobile.',
      type: TopMessageType.warning,
    );
  }

  StreamSubscription<DishStatusRealtimeEvent>? _dishStatusSubscription;

  StreamSubscription<PaymentCompletedRealtimeEvent>? _paymentSubscription;

  @override
  void initState() {
    super.initState();

    orderItems = widget.initialOrderItems
        .where((item) => item.status != 'Đã hủy')
        .toList();

    isPaymentRequested = widget.initialPaymentRequested;

    // Luon lay tong tu OrderItem dang hien thi. Day la nguon dung nhat,
    // tranh truong hop table.amount cua du lieu cu = 0.
    currentAmount = orderItems.fold<int>(
      0,
      (sum, item) => sum + item.totalPrice,
    );

    _dishStatusSubscription = AppServices.realtimeService.dishStatusChanges
        .listen(_handleRealtimeDishStatus);

    _paymentSubscription = AppServices.realtimeService.paymentCompletions
        .listen(_handleRealtimePayment);
  }

  void _handleRealtimeDishStatus(DishStatusRealtimeEvent event) {
    if (!mounted || event.tableId != widget.table.id) {
      return;
    }

    final int index = orderItems.indexWhere(
      (item) => item.id == event.orderItemId,
    );

    if (index == -1) {
      return;
    }

    final OrderItem oldItem = orderItems[index];

    setState(() {
      if (event.status == 'Đã hủy') {
        orderItems.removeAt(index);
        currentAmount -= oldItem.totalPrice;

        if (currentAmount < 0) {
          currentAmount = 0;
        }

        return;
      }

      orderItems[index] = OrderItem(
        id: oldItem.id,
        name: oldItem.name,
        quantity: oldItem.quantity,
        unitPrice: oldItem.unitPrice,
        status: event.status,
        note: oldItem.note,
      );
    });
  }

  void _handleRealtimePayment(PaymentCompletedRealtimeEvent event) {
    if (!mounted || event.tableId != widget.table.id || paymentCompleted) {
      return;
    }

    unawaited(_finishRealtimePayment());
  }

  Future<void> _finishRealtimePayment() async {
    if (!mounted || paymentCompleted) return;

    setState(() {
      paymentCompleted = true;
      isPaymentRequested = false;
      orderItems.clear();
      currentAmount = 0;
    });

    TopMessage.show(
      context,
      message: 'Thanh toán ${widget.table.name} đã hoàn tất',
      type: TopMessageType.success,
    );

    await Future<void>.delayed(const Duration(milliseconds: 360));
    if (!mounted || ModalRoute.of(context)?.isCurrent != true) return;

    Navigator.pop(context, {
      'amount': 0,
      'orderItems': <OrderItem>[],
      'paymentRequested': false,
      'paymentCompleted': true,
    });
  }

  @override
  void dispose() {
    _dishStatusSubscription?.cancel();
    _paymentSubscription?.cancel();
    super.dispose();
  }

  Future<bool> addNewOrderItems(List<CartItem> cartItems) async {
    if (!_canOrder) {
      _showRoleDenied('gọi món');
      return false;
    }
    try {
      final List<OrderItem> addedItems = await AppServices.orderService
          .addItems(
            tableId: widget.table.id,
            employeeCode: appEmployee.employeeCode,
            items: cartItems,
          );

      if (!mounted) {
        return false;
      }

      setState(() {
        orderItems.addAll(addedItems);

        for (final item in addedItems) {
          currentAmount += item.totalPrice;
        }
      });

      return true;
    } on ApiException catch (error) {
      if (!mounted) {
        return false;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );

      return false;
    }
  }

  Future<void> markAsServed(int index) async {
    if (!_canMarkServed) {
      _showRoleDenied('xác nhận đã phục vụ');
      return;
    }
    final item = orderItems[index];

    if (item.status != 'Đã xong') {
      return;
    }

    if (item.id == null) {
      TopMessage.show(
        context,
        message: 'Món này chưa được đồng bộ database',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      final String newStatus = await AppServices.orderService.updateItemStatus(
        orderItemId: item.id!,
        status: 'Đã phục vụ',
      );

      if (!mounted) {
        return;
      }

      setState(() {
        orderItems[index] = OrderItem(
          id: item.id,
          name: item.name,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          status: newStatus,
          note: item.note,
        );
      });

      TopMessage.show(
        context,
        message: '${item.name} đã được phục vụ',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  // DEBUG ONLY: giả lập thao tác Bếp cho đến khi WinForms Bếp tích hợp hoàn chỉnh.
  Future<void> _simulateKitchenUpdate() async {
    int index = orderItems.indexWhere((item) => item.status == 'Đang chế biến');

    String targetStatus;

    if (index != -1) {
      targetStatus = 'Đã xong';
    } else {
      index = orderItems.indexWhere((item) => item.status == 'Chờ chế biến');

      if (index == -1) {
        TopMessage.show(
          context,
          message: 'Không có món nào cần Bếp xử lý',
          type: TopMessageType.warning,
        );
        return;
      }

      targetStatus = 'Đang chế biến';
    }

    final OrderItem item = orderItems[index];

    if (item.id == null) {
      TopMessage.show(
        context,
        message: 'Món này chưa được đồng bộ database',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      final String newStatus = await AppServices.orderService.updateItemStatus(
        orderItemId: item.id!,
        status: targetStatus,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        orderItems[index] = OrderItem(
          id: item.id,
          name: item.name,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          status: newStatus,
          note: item.note,
        );
      });

      if (newStatus == 'Đã xong') {
        // Notification DB + SignalR được backend tạo.
        // Không insert local để tránh thông báo trùng.
        TopMessage.show(
          context,
          message: 'Bếp thông báo: ${item.name} đã xong',
          type: TopMessageType.info,
        );
      } else {
        final String noteText =
            item.note != null && item.note!.trim().isNotEmpty
            ? ' - Ghi chú: ${item.note}'
            : '';

        TopMessage.show(
          context,
          message: 'Bếp đã nhận món: ${item.name}$noteText',
          type: TopMessageType.info,
        );
      }
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  int get totalQuantity {
    return orderItems.fold(0, (sum, item) => sum + item.quantity);
  }

  bool get allItemsServed {
    if (orderItems.isEmpty) {
      return false;
    }

    return orderItems.every((item) => item.status == 'Đã phục vụ');
  }

  int get totalAmount {
    return currentAmount;
  }

  String formatMoney(int amount) {
    final value = amount.toString();
    final buffer = StringBuffer();

    for (int i = 0; i < value.length; i++) {
      if (i > 0 && (value.length - i) % 3 == 0) {
        buffer.write('.');
      }
      buffer.write(value[i]);
    }

    return '${buffer.toString()}đ';
  }

  Future<void> _requestPayment() async {
    if (!_canRequestPayment) {
      _showRoleDenied('gửi yêu cầu thanh toán');
      return;
    }
    if (!allItemsServed) {
      final int remainingItems = orderItems
          .where((item) => item.status != 'Đã phục vụ')
          .length;

      TopMessage.show(
        context,
        message:
            'Còn $remainingItems món chưa được phục vụ. '
            'Chưa thể yêu cầu thanh toán.',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      await AppServices.paymentService.requestPayment(
        tableId: widget.table.id,
        employeeCode: appEmployee.employeeCode,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        isPaymentRequested = true;
      });

      TopMessage.show(
        context,
        message: 'Đã gửi yêu cầu thanh toán cho ${widget.table.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _confirmPayment() async {
    if (!_canCompletePayment) {
      _showRoleDenied('xác nhận thanh toán');
      return;
    }
    if (!allItemsServed) {
      TopMessage.show(
        context,
        message: 'Không thể thanh toán khi vẫn còn món chưa được phục vụ',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      final PaymentPreview preview = await AppServices.paymentService
          .getPreview(tableId: widget.table.id);

      if (!mounted) {
        return;
      }

      final String? paymentMethod = await _showPaymentDialog(preview);

      if (!mounted || paymentMethod == null) {
        return;
      }

      await AppServices.paymentService.completePayment(
        tableId: widget.table.id,
        employeeCode: appEmployee.employeeCode,
        paymentMethod: paymentMethod,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        paymentCompleted = true;
        isPaymentRequested = false;
        orderItems.clear();
        currentAmount = 0;
      });

      TopMessage.show(
        context,
        message: 'Thanh toán ${widget.table.name} thành công',
        type: TopMessageType.success,
      );

      await Future.delayed(const Duration(milliseconds: 650));

      if (!mounted) {
        return;
      }

      Navigator.pop(context, {
        'amount': 0,
        'orderItems': <OrderItem>[],
        'paymentRequested': false,
        'paymentCompleted': true,
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<String?> _showPaymentDialog(PaymentPreview preview) async {
    String selectedMethod = 'TienMat';

    return showDialog<String>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text('Xác nhận thanh toán'),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildPaymentSummaryRow(
                      'Tạm tính',
                      formatMoney(preview.subTotal),
                    ),

                    if (preview.discountAmount > 0) ...[
                      const SizedBox(height: 6),
                      _buildPaymentSummaryRow(
                        preview.appliedCampaignName != null
                            ? 'Giảm giá (${preview.appliedCampaignName})'
                            : 'Giảm giá',
                        '-${formatMoney(preview.discountAmount)}',
                      ),
                    ],

                    if (preview.vipRoomFee > 0) ...[
                      const SizedBox(height: 6),
                      _buildPaymentSummaryRow(
                        'Phí phòng VIP',
                        formatMoney(preview.vipRoomFee),
                      ),
                    ],

                    if (preview.vipMinimumGap > 0) ...[
                      const SizedBox(height: 6),
                      _buildPaymentSummaryRow(
                        'Bù hóa đơn tối thiểu',
                        formatMoney(preview.vipMinimumGap),
                      ),
                    ],

                    if (preview.taxAmount > 0) ...[
                      const SizedBox(height: 6),
                      _buildPaymentSummaryRow(
                        'Thuế',
                        formatMoney(preview.taxAmount),
                      ),
                    ],

                    const Divider(height: 24),

                    _buildPaymentSummaryRow(
                      'Tổng thanh toán',
                      formatMoney(preview.totalAmount),
                      isTotal: true,
                    ),

                    const SizedBox(height: 18),

                    const Text(
                      'Phương thức thanh toán',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),

                    RadioGroup<String>(
                      groupValue: selectedMethod,
                      onChanged: (value) {
                        if (value == null) return;

                        setDialogState(() {
                          selectedMethod = value;
                        });
                      },
                      child: const Column(
                        children: [
                          RadioListTile<String>(
                            value: 'TienMat',
                            contentPadding: EdgeInsets.zero,
                            title: Text('Tiền mặt'),
                          ),
                          RadioListTile<String>(
                            value: 'TheNganHang',
                            contentPadding: EdgeInsets.zero,
                            title: Text('Thẻ ngân hàng'),
                          ),
                          RadioListTile<String>(
                            value: 'ChuyenKhoan',
                            contentPadding: EdgeInsets.zero,
                            title: Text('Chuyển khoản'),
                          ),
                          RadioListTile<String>(
                            value: 'ViDienTu',
                            contentPadding: EdgeInsets.zero,
                            title: Text('Ví điện tử'),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () {
                    Navigator.pop(dialogContext);
                  },
                  child: const Text('Hủy'),
                ),
                ElevatedButton(
                  onPressed: () {
                    Navigator.pop(dialogContext, selectedMethod);
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.green,
                    foregroundColor: Colors.white,
                  ),
                  child: const Text('Thanh toán'),
                ),
              ],
            );
          },
        );
      },
    );
  }

  Widget _buildPaymentSummaryRow(
    String label,
    String value, {
    bool isTotal = false,
  }) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            label,
            style: TextStyle(
              fontWeight: isTotal ? FontWeight.bold : FontWeight.normal,
            ),
          ),
        ),
        const SizedBox(width: 12),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.bold,
            color: isTotal ? AppTheme.orangeColor : AppTheme.textColor,
          ),
        ),
      ],
    );
  }

  Future<void> _confirmCancelOrderItem(OrderItem item) async {
    if (!_canCancelOrderItem) {
      _showRoleDenied('hủy món');
      return;
    }
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Hủy món'),
          content: Text('Bạn có chắc muốn hủy "${item.name}" không?'),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.pop(dialogContext, false);
              },
              child: const Text('Không'),
            ),

            ElevatedButton(
              onPressed: () {
                Navigator.pop(dialogContext, true);
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.red,
                foregroundColor: Colors.white,
              ),
              child: const Text('Hủy món'),
            ),
          ],
        );
      },
    );

    if (!mounted || confirmed != true) {
      return;
    }

    if (item.id == null) {
      TopMessage.show(
        context,
        message: 'Món này chưa được đồng bộ database',
        type: TopMessageType.warning,
      );
      return;
    }

    try {
      await AppServices.orderService.cancelItem(orderItemId: item.id!);
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
      return;
    }

    if (!mounted) {
      return;
    }

    setState(() {
      // Nếu SignalR đã xử lý trước thì không trừ tiền lần 2.
      final bool stillExists = orderItems.any(
        (current) => current.id == item.id,
      );

      if (stillExists) {
        currentAmount -= item.totalPrice;

        if (currentAmount < 0) {
          currentAmount = 0;
        }

        orderItems.removeWhere((current) => current.id == item.id);
      }
    });

    TopMessage.show(
      context,
      message: 'Đã hủy món ${item.name}',
      type: TopMessageType.warning,
    );
  }

  Future<void> _confirmReleaseTable() async {
    if (!_canManageTables) {
      _showRoleDenied('trả bàn');
      return;
    }
    // Chỉ cho trả bàn khi không còn món
    if (orderItems.isNotEmpty) {
      TopMessage.show(
        context,
        message: 'Không thể trả bàn khi vẫn còn món trong đơn',
        type: TopMessageType.warning,
      );
      return;
    }

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Trả bàn'),
          content: Text(
            'Bạn có chắc muốn trả ${widget.table.name} về trạng thái trống không?',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.pop(dialogContext, false);
              },
              child: const Text('Không'),
            ),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(dialogContext, true);
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.red,
                foregroundColor: Colors.white,
              ),
              child: const Text('Trả bàn'),
            ),
          ],
        );
      },
    );

    if (!mounted || confirmed != true) {
      return;
    }

    Navigator.pop(context, {
      'amount': 0,
      'orderItems': <OrderItem>[],
      'paymentRequested': false,
      'paymentCompleted': false,
      'releaseTable': true,
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        leading: IconButton(
          onPressed: () {
            Navigator.pop(context, {
              'amount': totalAmount,
              'orderItems': orderItems,
              'paymentRequested': isPaymentRequested,
            });
          },
          icon: const Icon(Icons.arrow_back),
        ),
        title: Text(
          widget.table.name,
          style: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.bold,
            color: AppTheme.textColor,
          ),
        ),
        actions: [
          // =========================================
          // DEBUG: mô phỏng thao tác từ Bếp.
          // Backend vẫn phát SignalR thật; release sẽ ẩn nút này.
          // =========================================
          if (kDebugMode &&
              MobileAccessPolicy.roleOf(appEmployee.role) == MobileRole.quanLy)
            IconButton(
              onPressed: _simulateKitchenUpdate,
              tooltip: 'DEBUG: Mô phỏng cập nhật từ Bếp',
              icon: const Icon(Icons.notifications_none),
            ),

          // =========================================
          // THAO TÁC BÀN
          // =========================================
          if (_canManageTables)
            PopupMenuButton<String>(
              icon: const Icon(Icons.more_vert, color: AppTheme.textColor),
              tooltip: 'Thao tác bàn',

              onSelected: (value) {
                switch (value) {
                  case 'transfer':
                    Navigator.pop(context, {
                      'amount': totalAmount,
                      'orderItems': orderItems,
                      'paymentRequested': isPaymentRequested,
                      'transferTable': true,
                    });
                    break;

                  case 'merge':
                    Navigator.pop(context, {
                      'amount': totalAmount,
                      'orderItems': orderItems,
                      'paymentRequested': isPaymentRequested,
                      'mergeTable': true,
                    });
                    break;

                  case 'split':
                    Navigator.pop(context, {
                      'amount': totalAmount,
                      'orderItems': orderItems,
                      'paymentRequested': isPaymentRequested,
                      'splitTable': true,
                    });
                    break;
                }
              },

              itemBuilder: (context) => const [
                PopupMenuItem<String>(
                  value: 'transfer',
                  child: Row(
                    children: [
                      Icon(Icons.swap_horiz),
                      SizedBox(width: 12),
                      Text('Chuyển bàn'),
                    ],
                  ),
                ),

                PopupMenuItem<String>(
                  value: 'merge',
                  child: Row(
                    children: [
                      Icon(Icons.merge_type),
                      SizedBox(width: 12),
                      Text('Gộp bàn'),
                    ],
                  ),
                ),

                PopupMenuItem<String>(
                  value: 'split',
                  child: Row(
                    children: [
                      Icon(Icons.call_split),
                      SizedBox(width: 12),
                      Text('Tách bàn'),
                    ],
                  ),
                ),
              ],
            ),

          const SizedBox(width: 4),
        ],
      ),

      body: Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ...orderItems.asMap().entries.map((entry) {
                    final index = entry.key;
                    final item = entry.value;

                    return Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _buildOrderItem(
                        name: item.name,
                        quantity: item.quantity,
                        unitPrice: item.unitPrice,
                        note: item.note,
                        status: item.status,
                        statusColor: _getStatusColor(item.status),

                        // Đã xong -> cho phép nhân viên xác nhận đã phục vụ
                        onServed: _canMarkServed && item.status == 'Đã xong'
                            ? () => markAsServed(index)
                            : null,

                        // Chờ chế biến -> chỉ Phục vụ/Quản lý được hủy.
                        onCancel:
                            _canCancelOrderItem && item.status == 'Chờ chế biến'
                            ? () => _confirmCancelOrderItem(item)
                            : null,
                      ),
                    );
                  }),

                  const SizedBox(height: 4),

                  _buildOrderSummary(),
                ],
              ),
            ),
          ),

          _buildActions(context),
        ],
      ),
    );
  }

  Widget _buildOrderSummary() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'TỔNG QUAN ĐƠN',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: Colors.grey.shade600,
            ),
          ),

          const SizedBox(height: 8),

          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                '$totalQuantity món',
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),

              Text(
                formatMoney(totalAmount),
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppTheme.orangeColor,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Color _getStatusColor(String status) {
    switch (status) {
      case 'Chờ chế biến':
        return Colors.grey;

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

  Widget _buildOrderItem({
    required String name,
    required int quantity,
    required int unitPrice,
    String? note,
    required String status,
    required Color statusColor,
    VoidCallback? onServed,
    VoidCallback? onCancel,
  }) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w600,
                  ),
                ),

                const SizedBox(height: 6),

                Row(
                  children: [
                    Text(
                      'SL: $quantity',
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.grey.shade600,
                      ),
                    ),
                    const SizedBox(width: 10),
                    Text(
                      formatMoney(unitPrice * quantity),
                      style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: AppTheme.orangeColor,
                      ),
                    ),
                  ],
                ),

                if (note != null && note.trim().isNotEmpty) ...[
                  const SizedBox(height: 5),

                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                        Icons.edit_note,
                        size: 16,
                        color: Colors.grey.shade500,
                      ),

                      const SizedBox(width: 4),

                      Expanded(
                        child: Text(
                          note,
                          style: TextStyle(
                            fontSize: 12,
                            color: Colors.grey.shade600,
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),

          // Trạng thái món
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              status,
              style: TextStyle(
                color: statusColor,
                fontSize: 11,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),

          // Nút xác nhận đã phục vụ
          if (onServed != null) ...[
            const SizedBox(width: 8),

            IconButton(
              onPressed: onServed,
              tooltip: 'Xác nhận đã phục vụ',
              icon: const Icon(Icons.check_circle_outline, color: Colors.green),
            ),
          ],

          // Nút hủy món
          if (onCancel != null) ...[
            const SizedBox(width: 4),

            IconButton(
              onPressed: onCancel,
              tooltip: 'Hủy món',
              icon: const Icon(Icons.cancel_outlined, color: Colors.red),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildActions(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 20),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: Colors.grey.shade200)),
      ),
      child: Column(
        children: [
          // =========================
          // GỌI THÊM MÓN
          // =========================
          if (_canOrder)
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton.icon(
                onPressed: () async {
                  final List<CartItem>? newItems =
                      await Navigator.push<List<CartItem>>(
                        context,
                        MaterialPageRoute(
                          builder: (context) => MenuScreen(
                            table: widget.table,
                            notifications: widget.notifications,
                          ),
                        ),
                      );

                  if (!context.mounted) {
                    return;
                  }

                  if (newItems != null && newItems.isNotEmpty) {
                    final bool success = await addNewOrderItems(newItems);

                    if (!context.mounted || !success) {
                      return;
                    }

                    TopMessage.show(
                      context,
                      message: 'Gọi món thành công',
                      type: TopMessageType.success,
                    );
                  }
                },
                icon: const Icon(Icons.add_circle_outline),
                label: const Text(
                  'Gọi thêm món',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppTheme.orangeColor,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
              ),
            ),

          const SizedBox(height: 10),

          // =========================
          // TRẢ BÀN / THANH TOÁN
          // =========================
          if (orderItems.isEmpty && _canManageTables)
            SizedBox(
              width: double.infinity,
              height: 50,
              child: OutlinedButton(
                onPressed: _confirmReleaseTable,
                style: OutlinedButton.styleFrom(
                  foregroundColor: Colors.red,
                  side: const BorderSide(color: Colors.red),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                child: const Text(
                  'Trả bàn',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
            )
          else if (orderItems.isNotEmpty)
            SizedBox(
              width: double.infinity,
              height: 50,
              child: isPaymentRequested
                  ? ElevatedButton(
                      onPressed: _canCompletePayment ? _confirmPayment : null,
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.green,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ),
                      child: Text(
                        _canCompletePayment
                            ? 'Xác nhận thanh toán'
                            : 'Đang chờ Thu ngân xác nhận',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    )
                  : OutlinedButton(
                      onPressed: _canRequestPayment ? _requestPayment : null,

                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppTheme.orangeColor,
                        side: const BorderSide(color: AppTheme.orangeColor),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ),
                      child: Text(
                        _canRequestPayment
                            ? 'Khách gọi thanh toán'
                            : 'Không có quyền yêu cầu thanh toán',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
            ),
        ],
      ),
    );
  }
}
