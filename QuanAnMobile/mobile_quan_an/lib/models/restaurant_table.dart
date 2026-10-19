enum TableStatus {
  empty,
  occupied,
  reserved,
}

class RestaurantTable {
  final int id;
  final String name;
  final TableStatus status;
  final String? customerName;
  final String? time;
  final int? amount;
  final int? capacity;
  final int? areaId;
  final String? areaName;
  final String? tableType;
  final int? guestCount;
  final bool canOperate;

  const RestaurantTable({
    required this.id,
    required this.name,
    required this.status,
    this.customerName,
    this.time,
    this.amount,
    this.capacity,
    this.areaId,
    this.areaName,
    this.tableType,
    this.guestCount,
    this.canOperate = true,
  });

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
    String? customerName,
    bool clearCustomerName = false,
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
    bool? canOperate,
  }) {
    return RestaurantTable(
      id: id,
      name: name ?? this.name,
      status: status ?? this.status,
      customerName:
          clearCustomerName ? null : customerName ?? this.customerName,
      time: clearTime ? null : time ?? this.time,
      amount: clearAmount ? null : amount ?? this.amount,
      capacity: capacity ?? this.capacity,
      areaId: areaId ?? this.areaId,
      areaName: areaName ?? this.areaName,
      tableType: tableType ?? this.tableType,
      guestCount:
          clearGuestCount ? null : guestCount ?? this.guestCount,
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
      customerName:
          json['customerName']?.toString(),
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
      'customerName': customerName,
      'time': time,
      'amount': amount,
      'capacity': capacity,
      'areaId': areaId,
      'areaName': areaName,
      'tableType': tableType,
      'guestCount': guestCount,
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
