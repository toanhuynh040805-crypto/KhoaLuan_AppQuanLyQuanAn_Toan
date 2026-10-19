import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../models/app_notification.dart';
import 'api_config.dart';
import 'mobile_access_policy.dart';

class DishStatusRealtimeEvent {
  final int orderItemId;
  final int tableId;
  final String tableName;
  final String itemName;
  final String status;

  const DishStatusRealtimeEvent({
    required this.orderItemId,
    required this.tableId,
    required this.tableName,
    required this.itemName,
    required this.status,
  });

  factory DishStatusRealtimeEvent.fromJson(
    Map<String, dynamic> json,
  ) {
    return DishStatusRealtimeEvent(
      orderItemId: _parseInt(json['orderItemId']),
      tableId: _parseInt(json['tableId']),
      tableName: json['tableName']?.toString() ?? '',
      itemName: json['itemName']?.toString() ?? '',
      status: json['status']?.toString() ?? '',
    );
  }
}

class PaymentCompletedRealtimeEvent {
  final int tableId;
  final int orderId;
  final int invoiceId;
  final int totalAmount;

  const PaymentCompletedRealtimeEvent({
    required this.tableId,
    required this.orderId,
    required this.invoiceId,
    required this.totalAmount,
  });

  factory PaymentCompletedRealtimeEvent.fromJson(
    Map<String, dynamic> json,
  ) {
    return PaymentCompletedRealtimeEvent(
      tableId: _parseInt(json['tableId']),
      orderId: _parseInt(json['orderId']),
      invoiceId: _parseInt(json['invoiceId']),
      totalAmount: _parseInt(json['totalAmount']),
    );
  }
}

class TableStatusRealtimeEvent {
  final int tableId;
  final String tableName;
  final dynamic status;

  const TableStatusRealtimeEvent({
    required this.tableId,
    required this.tableName,
    required this.status,
  });

  factory TableStatusRealtimeEvent.fromJson(
    Map<String, dynamic> json,
  ) {
    return TableStatusRealtimeEvent(
      tableId: _parseInt(json['tableId']),
      tableName: json['tableName']?.toString() ?? '',
      status: json['status'],
    );
  }
}

class RealtimeService {
  HubConnection? _connection;
  String? _employeeCode;
  String? _roleKey;

  // Khi Thu ngân/Quản lý vừa tự thanh toán, backend phát PaymentCompleted và
  // TableStatusUpdated gần như cùng lúc. TableScreen nằm dưới route chi tiết
  // không cần reload trong vài giây đó vì sau Navigator.pop nó sẽ tự _loadData.
  // Giữ mốc theo tableId để bỏ qua các event trạng thái bàn thuộc chính giao
  // dịch vừa hoàn tất, tránh rebuild màn phía dưới trong lúc route đang tháo.
  final Map<int, DateTime> _recentLocalPaymentTables = <int, DateTime>{};

  final StreamController<AppNotification> _notificationController =
      StreamController<AppNotification>.broadcast();

  final StreamController<DishStatusRealtimeEvent> _dishStatusController =
      StreamController<DishStatusRealtimeEvent>.broadcast();

  final StreamController<PaymentCompletedRealtimeEvent> _paymentController =
      StreamController<PaymentCompletedRealtimeEvent>.broadcast();

  final StreamController<TableStatusRealtimeEvent> _tableStatusController =
      StreamController<TableStatusRealtimeEvent>.broadcast();

  final StreamController<void> _kitchenQueueController =
      StreamController<void>.broadcast();

  Stream<AppNotification> get notifications =>
      _notificationController.stream;

  Stream<DishStatusRealtimeEvent> get dishStatusChanges =>
      _dishStatusController.stream;

  Stream<PaymentCompletedRealtimeEvent> get paymentCompletions =>
      _paymentController.stream;

  Stream<TableStatusRealtimeEvent> get tableStatusChanges =>
      _tableStatusController.stream;

  Stream<void> get kitchenQueueChanges =>
      _kitchenQueueController.stream;

  bool get _isCashierOrManager =>
      MobileAccessPolicy.canCompletePayment(_roleKey ?? '');

  Future<void> start({
    required String employeeCode,
    required String roleKey,
  }) async {
    if (_connection != null &&
        _employeeCode == employeeCode &&
        _roleKey == roleKey) {
      return;
    }

    await stop();

    _employeeCode = employeeCode;
    _roleKey = roleKey;

    final HubConnection connection = HubConnectionBuilder()
        .withUrl(ApiConfig.hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on(
      'NotificationReceived',
      (arguments) {
        final Map<String, dynamic>? data = _firstMap(arguments);

        if (data == null) {
          return;
        }

        _notificationController.add(
          AppNotification.fromJson(data),
        );
      },
    );

    // Contracts realtime gốc của WinForms. Mobile bếp/quầy nước chỉ cần
    // biết hàng đợi đã thay đổi rồi tải lại đúng StationId của tài khoản.
    connection.on(
      'NewTicket',
      (_) => _kitchenQueueController.add(null),
    );

    connection.on(
      'ItemStatusUpdated',
      (_) => _kitchenQueueController.add(null),
    );

    connection.on(
      'OrderItemCancelled',
      (_) => _kitchenQueueController.add(null),
    );

    connection.on(
      'DishStatusChanged',
      (arguments) {
        final Map<String, dynamic>? data = _firstMap(arguments);

        if (data == null) {
          return;
        }

        _dishStatusController.add(
          DishStatusRealtimeEvent.fromJson(data),
        );
      },
    );

    connection.on(
      'PaymentCompleted',
      (arguments) {
        final Map<String, dynamic>? data = _firstMap(arguments);

        if (data == null) {
          return;
        }

        final PaymentCompletedRealtimeEvent event =
            PaymentCompletedRealtimeEvent.fromJson(data);

        // Thu ngân/Quản lý tự gửi POST /payments. Không phát lại event này vào
        // chính màn chi tiết vì HTTP response sẽ là nguồn duy nhất đóng route.
        // Đồng thời đánh dấu tableId để TableStatusUpdated kế tiếp của cùng giao
        // dịch không làm TableScreen bên dưới rebuild giữa animation Navigator.
        if (_isCashierOrManager) {
          if (event.tableId > 0) {
            _recentLocalPaymentTables[event.tableId] = DateTime.now();
          }
          return;
        }

        // Phục vụ vẫn cần realtime để biết Thu ngân ở thiết bị khác đã thanh toán.
        _paymentController.add(event);
      },
    );

    connection.on(
      'TableStatusUpdated',
      (arguments) {
        final Map<String, dynamic>? data = _firstMap(arguments);

        if (data == null) {
          return;
        }

        final TableStatusRealtimeEvent event =
            TableStatusRealtimeEvent.fromJson(data);

        if (_isCashierOrManager) {
          final DateTime now = DateTime.now();

          _recentLocalPaymentTables.removeWhere(
            (_, createdAt) =>
                now.difference(createdAt) > const Duration(seconds: 3),
          );

          final DateTime? localPaymentAt =
              _recentLocalPaymentTables[event.tableId];

          if (localPaymentAt != null &&
              now.difference(localPaymentAt) <=
                  const Duration(seconds: 3)) {
            return;
          }
        }

        _tableStatusController.add(event);
      },
    );

    connection.onreconnected(
      ({String? connectionId}) {
        _joinGroups(
          connection,
          employeeCode,
          roleKey,
        );
      },
    );

    _connection = connection;

    await connection.start();

    await _joinGroups(
      connection,
      employeeCode,
      roleKey,
    );
  }

  Future<void> _joinGroups(
    HubConnection connection,
    String employeeCode,
    String roleKey,
  ) async {
    await connection.invoke(
      'JoinEmployeeGroup',
      args: <Object>[
        employeeCode,
      ],
    );

    if (roleKey.trim().isNotEmpty) {
      await connection.invoke(
        'JoinDepartmentGroup',
        args: <Object>[
          roleKey,
        ],
      );
    }
  }

  Future<void> stop() async {
    final HubConnection? connection = _connection;

    _connection = null;
    _employeeCode = null;
    _roleKey = null;
    _recentLocalPaymentTables.clear();

    if (connection != null) {
      try {
        await connection.stop();
      } catch (_) {
        // Không làm gián đoạn logout/restart app
        // nếu kết nối SignalR đã tự ngắt.
      }
    }
  }

  static Map<String, dynamic>? _firstMap(
    List<Object?>? arguments,
  ) {
    if (arguments == null || arguments.isEmpty) {
      return null;
    }

    final Object? first = arguments.first;

    if (first is Map) {
      return Map<String, dynamic>.from(first);
    }

    return null;
  }
}

int _parseInt(dynamic value) {
  if (value is int) {
    return value;
  }

  if (value is num) {
    return value.round();
  }

  return double.tryParse(value?.toString() ?? '')?.round() ?? 0;
}
