enum TableStatus {
  empty,
  occupied,
  reserved,
}

class RestaurantTable {
  final int id;
  final String name;
  final TableStatus status;
  final int? customerId;
  final String? customerName;
  final String? customerPhone;
  final String? time;
  final int? amount;
  final int? capacity;
  final int? areaId;
  final String? areaName;
  final String? tableType;
  final int? guestCount;
  final String? currentSessionType;
  final int? nextReservationId;
  final DateTime? nextReservationTime;
  final String? nextReservationCustomerName;
  final int? nextReservationGuestCount;
  final bool canOperate;

  const RestaurantTable({
    required this.id,
    required this.name,
    required this.status,
    this.customerId,
    this.customerName,
    this.customerPhone,
    this.time,
    this.amount,
    this.capacity,
    this.areaId,
    this.areaName,
    this.tableType,
    this.guestCount,
    this.currentSessionType,
    this.nextReservationId,
    this.nextReservationTime,
    this.nextReservationCustomerName,
    this.nextReservationGuestCount,
    this.canOperate = true,
  });

  bool get hasUpcomingReservation =>
      nextReservationId != null && nextReservationTime != null;

  bool get isReservationSession =>
      (currentSessionType ?? '').trim().toLowerCase() == 'reservation';

  bool get isWalkInSession =>
      (currentSessionType ?? '').trim().toLowerCase() == 'walkin';

  bool get isTakeaway {
    final String normalizedType =
        (tableType ?? '').trim().toLowerCase();
    final String normalizedArea =
        (areaName ?? '').trim().toLowerCase();

    return normalizedType == 'mangve' ||
        normalizedType == 'mang về' ||
        normalizedArea == 'mang về' ||
        normalizedArea == 'mang ve';
  }

  RestaurantTable copyWith({
    String? name,
    TableStatus? status,
    int? customerId,
    bool clearCustomerId = false,
    String? customerName,
    bool clearCustomerName = false,
    String? customerPhone,
    bool clearCustomerPhone = false,
    String? time,
    bool clearTime = false,
    int? amount,
    bool clearAmount = false,
    int? capacity,
    int? areaId,
    String? areaName,
    String? tableType,
    int? guestCount,
    bool clearGuestCount = false,
    String? currentSessionType,
    bool clearCurrentSessionType = false,
    int? nextReservationId,
    bool clearNextReservationId = false,
    DateTime? nextReservationTime,
    bool clearNextReservationTime = false,
    String? nextReservationCustomerName,
    bool clearNextReservationCustomerName = false,
    int? nextReservationGuestCount,
    bool clearNextReservationGuestCount = false,
    bool? canOperate,
  }) {
    return RestaurantTable(
      id: id,
      name: name ?? this.name,
      status: status ?? this.status,
      customerId:
          clearCustomerId ? null : customerId ?? this.customerId,
      customerName:
          clearCustomerName ? null : customerName ?? this.customerName,
      customerPhone:
          clearCustomerPhone ? null : customerPhone ?? this.customerPhone,
      time: clearTime ? null : time ?? this.time,
      amount: clearAmount ? null : amount ?? this.amount,
      capacity: capacity ?? this.capacity,
      areaId: areaId ?? this.areaId,
      areaName: areaName ?? this.areaName,
      tableType: tableType ?? this.tableType,
      guestCount:
          clearGuestCount ? null : guestCount ?? this.guestCount,
      currentSessionType: clearCurrentSessionType
          ? null
          : currentSessionType ?? this.currentSessionType,
      nextReservationId: clearNextReservationId
          ? null
          : nextReservationId ?? this.nextReservationId,
      nextReservationTime: clearNextReservationTime
          ? null
          : nextReservationTime ?? this.nextReservationTime,
      nextReservationCustomerName: clearNextReservationCustomerName
          ? null
          : nextReservationCustomerName ?? this.nextReservationCustomerName,
      nextReservationGuestCount: clearNextReservationGuestCount
          ? null
          : nextReservationGuestCount ?? this.nextReservationGuestCount,
      canOperate: canOperate ?? this.canOperate,
    );
  }

  factory RestaurantTable.fromJson(
    Map<String, dynamic> json,
  ) {
    final int? parsedGuestCount =
        json['guestCount'] == null
            ? null
            : _parseNullableInt(json['guestCount']);

    return RestaurantTable(
      id: _parseInt(json['id']),
      name:
          json['name']?.toString() ??
              json['tableName']?.toString() ??
              '',
      status: _parseStatus(json['status']),
      customerId: json['customerId'] == null
          ? null
          : _parseNullableInt(json['customerId']),
      customerName:
          json['customerName']?.toString(),
      customerPhone:
          json['customerPhone']?.toString(),
      time: json['time']?.toString(),
      amount: json['amount'] == null
          ? null
          : _parseInt(json['amount']),
      capacity: json['capacity'] == null
          ? null
          : _parseInt(json['capacity']),
      areaId: json['areaId'] == null
          ? null
          : _parseInt(json['areaId']),
      areaName: json['areaName']?.toString(),
      tableType:
          json['tableType']?.toString(),
      guestCount:
          parsedGuestCount != null && parsedGuestCount > 0
              ? parsedGuestCount
              : null,
      currentSessionType: json['currentSessionType']?.toString(),
      nextReservationId: json['nextReservationId'] == null
          ? null
          : _parseNullableInt(json['nextReservationId']),
      nextReservationTime: _parseNullableDateTime(
        json['nextReservationTime'],
      ),
      nextReservationCustomerName:
          json['nextReservationCustomerName']?.toString(),
      nextReservationGuestCount: json['nextReservationGuestCount'] == null
          ? null
          : _parseNullableInt(json['nextReservationGuestCount']),
      canOperate: json['canOperate'] is bool
          ? json['canOperate'] as bool
          : true,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'status': _statusToJson(status),
      'customerId': customerId,
      'customerName': customerName,
      'customerPhone': customerPhone,
      'time': time,
      'amount': amount,
      'capacity': capacity,
      'areaId': areaId,
      'areaName': areaName,
      'tableType': tableType,
      'guestCount': guestCount,
      'currentSessionType': currentSessionType,
      'nextReservationId': nextReservationId,
      'nextReservationTime': nextReservationTime?.toIso8601String(),
      'nextReservationCustomerName': nextReservationCustomerName,
      'nextReservationGuestCount': nextReservationGuestCount,
      'canOperate': canOperate,
    };
  }

  static int _parseInt(dynamic value) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }

  static int? _parseNullableInt(dynamic value) {
    if (value == null) {
      return null;
    }

    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(value.toString());
  }

  static DateTime? _parseNullableDateTime(dynamic value) {
    if (value == null) return null;

    final String raw = value.toString().trim();
    if (raw.isEmpty) return null;

    return DateTime.tryParse(raw)?.toLocal();
  }

  static TableStatus _parseStatus(
    dynamic value,
  ) {
    if (value is num) {
      switch (value.toInt()) {
        case 1:
        case 3:
          return TableStatus.occupied;
        case 2:
          return TableStatus.reserved;
        case 0:
        default:
          return TableStatus.empty;
      }
    }

    final String status =
        value?.toString().trim().toLowerCase() ??
            '';

    switch (status) {
      case 'occupied':
      case 'đang dùng':
      case 'dang dung':
      case 'dangphucvu':
        return TableStatus.occupied;

      case 'reserved':
      case 'đã đặt':
      case 'da dat':
      case 'dadat':
        return TableStatus.reserved;

      case 'empty':
      case 'trống':
      case 'trong':
      default:
        return TableStatus.empty;
    }
  }

  static String _statusToJson(
    TableStatus status,
  ) {
    switch (status) {
      case TableStatus.empty:
        return 'empty';

      case TableStatus.occupied:
        return 'occupied';

      case TableStatus.reserved:
        return 'reserved';
    }
  }
}
