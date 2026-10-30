class OrderItem {
  final int? id;
  final String name;
  final int quantity;
  final int unitPrice;
  final String status;
  final String? note;
  final DateTime? createdAt;

  // Món khách yêu cầu mang về
  final bool isTakeaway;

  const OrderItem({
    this.id,
    required this.name,
    required this.quantity,
    required this.unitPrice,
    required this.status,
    this.note,
    this.createdAt,
    this.isTakeaway = false,
  });

  int get totalPrice => unitPrice * quantity;

  factory OrderItem.fromJson(
    Map<String, dynamic> json,
  ) {
    return OrderItem(
      id: json['id'] == null
          ? null
          : _parseInt(json['id']),
      name: json['name']?.toString() ?? '',
      quantity: _parseInt(
        json['quantity'],
      ),
      unitPrice: _parseInt(
        json['unitPrice'],
      ),
      status:
          json['status']?.toString() ??
              'Chờ chế biến',
      note: json['note']?.toString(),
      createdAt: _parseDateTime(json['createdAt'] ?? json['CreatedAt']),
      isTakeaway: _parseBool(json['isTakeaway']),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'quantity': quantity,
      'unitPrice': unitPrice,
      'status': status,
      'note': note,
      'createdAt': createdAt?.toIso8601String(),
      'isTakeaway': isTakeaway,
    };
  }

  OrderItem copyWith({
    int? id,
    String? name,
    int? quantity,
    int? unitPrice,
    String? status,
    String? note,
    bool clearNote = false,
    DateTime? createdAt,
    bool clearCreatedAt = false,
    bool? isTakeaway,
  }) {
    return OrderItem(
      id: id ?? this.id,
      name: name ?? this.name,
      quantity: quantity ?? this.quantity,
      unitPrice: unitPrice ?? this.unitPrice,
      status: status ?? this.status,
      note: clearNote ? null : note ?? this.note,
      createdAt: clearCreatedAt ? null : createdAt ?? this.createdAt,
      isTakeaway: isTakeaway ?? this.isTakeaway,
    );
  }

  static DateTime? _parseDateTime(dynamic value) {
    if (value == null) return null;
    if (value is DateTime) return value;

    final String raw = value.toString().trim();
    if (raw.isEmpty) return null;

    return DateTime.tryParse(raw)?.toLocal();
  }

  static bool _parseBool(dynamic value) {
    if (value is bool) {
      return value;
    }

    if (value is num) {
      return value != 0;
    }

    final String text = value?.toString().toLowerCase() ?? '';
    return text == 'true' || text == '1';
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
}