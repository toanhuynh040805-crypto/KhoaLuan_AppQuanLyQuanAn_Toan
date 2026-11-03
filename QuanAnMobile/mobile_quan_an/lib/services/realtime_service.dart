import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../models/app_notification.dart';
import 'api_config.dart';
import 'api_client.dart';

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

class TableLockTakenOverRealtimeEvent {
  final int tableId;
  final String tableName;
  final String previousClientToken;
  final String takenOverByEmployeeCode;
  final String takenOverByEmployeeName;

  const TableLockTakenOverRealtimeEvent({
    required this.tableId,
    required this.tableName,
    required this.previousClientToken,
    required this.takenOverByEmployeeCode,
    required this.takenOverByEmployeeName,
  });

  factory TableLockTakenOverRealtimeEvent.fromJson(
    Map<String, dynamic> json,
  ) {
    return TableLockTakenOverRealtimeEvent(
      tableId: _parseInt(json['tableId']),
      tableName: json['tableName']?.toString() ?? '',
      previousClientToken:
          json['previousClientToken']?.toString() ?? '',
      takenOverByEmployeeCode:
          json['takenOverByEmployeeCode']?.toString() ?? '',
      takenOverByEmployeeName:
          json['takenOverByEmployeeName']?.toString() ?? '',
    );
  }
}

class RealtimeService {
  RealtimeService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  final ApiClient _apiClient;
  HubConnection? _connection;
  String? _employeeCode;
  String? _roleKey;
  String? _departmentName;

  final StreamController<AppNotification> _notificationController =
      StreamController<AppNotification>.broadcast();

  final StreamController<DishStatusRealtimeEvent> _dishStatusController =
      StreamController<DishStatusRealtimeEvent>.broadcast();

  final StreamController<PaymentCompletedRealtimeEvent> _paymentController =
      StreamController<PaymentCompletedRealtimeEvent>.broadcast();

  final StreamController<TableStatusRealtimeEvent> _tableStatusController =
      StreamController<TableStatusRealtimeEvent>.broadcast();

  final StreamController<void> _reservationController =
      StreamController<void>.broadcast();

  final StreamController<TableLockTakenOverRealtimeEvent>
      _tableLockTakeoverController =
      StreamController<TableLockTakenOverRealtimeEvent>.broadcast();

  final StreamController<void> _kitchenQueueController =
      StreamController<void>.broadcast();

  final StreamController<void> _orderController =
      StreamController<void>.broadcast();

  Stream<AppNotification> get notifications =>
      _notificationController.stream;

  Stream<DishStatusRealtimeEvent> get dishStatusChanges =>
      _dishStatusController.stream;

  Stream<PaymentCompletedRealtimeEvent> get paymentCompletions =>
      _paymentController.stream;

  Stream<TableStatusRealtimeEvent> get tableStatusChanges =>
      _tableStatusController.stream;

  Stream<void> get reservationChanges =>
      _reservationController.stream;

  Stream<TableLockTakenOverRealtimeEvent> get tableLockTakeovers =>
      _tableLockTakeoverController.stream;

  Stream<void> get kitchenQueueChanges =>
      _kitchenQueueController.stream;

  Stream<void> get orderChanges =>
      _orderController.stream;

  Future<void> start({
    required String employeeCode,
    required String roleKey,
    String? departmentName,
  }) async {
    final String normalizedDepartment = departmentName?.trim() ?? '';

    if (_connection != null &&
        _employeeCode == employeeCode &&
        _roleKey == roleKey &&
        (_departmentName ?? '') == normalizedDepartment) {
      return;
    }

    await stop();

    _employeeCode = employeeCode;
    _roleKey = roleKey;
    _departmentName = normalizedDepartment;

    final HttpConnectionOptions httpOptions = HttpConnectionOptions(
      accessTokenFactory: () async => _apiClient.accessToken ?? '',
    );

    final HubConnection connection = HubConnectionBuilder()
        .withUrl(ApiConfig.hubUrl, options: httpOptions)
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
      (_) {
        _kitchenQueueController.add(null);
        _orderController.add(null);
      },
    );

    connection.on(
      'ItemStatusUpdated',
      (_) {
        _kitchenQueueController.add(null);
        _orderController.add(null);
      },
    );

    connection.on(
      'OrderItemCancelled',
      (_) {
        _kitchenQueueController.add(null);
        _orderController.add(null);
      },
    );

    connection.on(
      'KitchenPriorityUpdated',
      (_) => _kitchenQueueController.add(null),
    );

    connection.on(
      'OrderUpdated',
      (_) => _orderController.add(null),
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

        // Luôn phát cho Mobile, kể cả tài khoản Quản lý/Thu ngân.
        // Nếu chính Mobile vừa thanh toán thì màn chi tiết có guard
        // _closingAfterPayment để không đóng route hai lần; nếu WinForms thanh
        // toán thì Mobile nhận ngay và tự cập nhật/đóng bàn.
        _paymentController.add(event);
      },
    );

    connection.on(
      'TableLockTakenOver',
      (arguments) {
        final Map<String, dynamic>? data = _firstMap(arguments);

        if (data == null) {
          return;
        }

        _tableLockTakeoverController.add(
          TableLockTakenOverRealtimeEvent.fromJson(data),
        );
      },
    );

    connection.on(
      'ReservationUpdated',
      (_) => _reservationController.add(null),
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

        _tableStatusController.add(event);
      },
    );

    connection.onreconnected(
      ({String? connectionId}) {
        _joinGroups(
          connection,
          employeeCode,
          roleKey,
          normalizedDepartment,
        );
      },
    );

    _connection = connection;

    await connection.start();

    await _joinGroups(
      connection,
      employeeCode,
      roleKey,
      normalizedDepartment,
    );
  }

  Future<void> _joinGroups(
    HubConnection connection,
    String employeeCode,
    String roleKey,
    String departmentName,
  ) async {
    await connection.invoke(
      'JoinEmployeeGroup',
      args: <Object>[
        employeeCode,
      ],
    );

    final String normalizedRole = roleKey.trim();
    if (normalizedRole.isNotEmpty) {
      await connection.invoke(
        'JoinRoleGroup',
        args: <Object>[
          normalizedRole,
        ],
      );
    }

    final String normalizedDepartment = departmentName.trim();
    if (normalizedDepartment.isNotEmpty) {
      await connection.invoke(
        'JoinDepartmentGroup',
        args: <Object>[
          normalizedDepartment,
        ],
      );
    }
  }

  Future<void> stop() async {
    final HubConnection? connection = _connection;

    _connection = null;
    _employeeCode = null;
    _roleKey = null;
    _departmentName = null;

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
