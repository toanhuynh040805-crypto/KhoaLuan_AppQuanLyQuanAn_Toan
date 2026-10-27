import '../models/cart_item.dart';
import '../models/employee_profile.dart';
import '../models/order_item.dart';
import 'api_client.dart';

class KitchenQueueItem {
  final int orderId;
  final int tableId;
  final String tableName;
  final DateTime createdAt;
  final OrderItem item;

  const KitchenQueueItem({
    required this.orderId,
    required this.tableId,
    required this.tableName,
    required this.createdAt,
    required this.item,
  });

  factory KitchenQueueItem.fromJson(Map<String, dynamic> json) {
    return KitchenQueueItem(
      orderId: _parseInt(json['orderId'] ?? json['OrderId']),
      tableId: _parseInt(json['tableId'] ?? json['TableId']),
      createdAt: _parseDateTime(json['createdAt'] ?? json['CreatedAt']),
      tableName:
          json['tableName']?.toString() ??
          json['TableName']?.toString() ??
          '',
      item: OrderItem(
        id: _parseInt(json['id'] ?? json['Id']),
        name:
            json['menuItemName']?.toString() ??
            json['MenuItemName']?.toString() ??
            '',
        quantity: _parseInt(json['quantity'] ?? json['Quantity']),
        unitPrice: 0,
        status: statusLabel(
          json['kitchenStatus'] ?? json['KitchenStatus'] ?? json['status'],
        ),
        note: json['note']?.toString() ?? json['Note']?.toString(),
        isTakeaway: _parseBool(json['isTakeaway'] ?? json['IsTakeaway']),
      ),
    );
  }

  static DateTime _parseDateTime(dynamic value) {
    if (value is DateTime) return value;
    return DateTime.tryParse(value?.toString() ?? '') ??
        DateTime.fromMillisecondsSinceEpoch(0);
  }

  static bool _parseBool(dynamic value) {
    if (value is bool) return value;
    if (value is num) return value != 0;
    final String text = value?.toString().toLowerCase() ?? '';
    return text == 'true' || text == '1';
  }

  static int _parseInt(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '') ?? 0;
  }

  static String statusLabel(dynamic value) {
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'Chờ chế biến',
        1 => 'Đang chế biến',
        2 => 'Đã xong',
        3 => 'Đã phục vụ',
        4 => 'Đã hủy',
        _ => 'Chờ chế biến',
      };
    }

    final String normalized = value?.toString().trim().toLowerCase() ?? '';
    return switch (normalized) {
      'moigoi' || 'mới gọi' || 'chờ chế biến' => 'Chờ chế biến',
      'dangchebien' || 'đang chế biến' => 'Đang chế biến',
      'dachebien' || 'daxong' || 'đã chế biến' || 'đã xong' => 'Đã xong',
      'daphucvu' || 'đã phục vụ' => 'Đã phục vụ',
      'dahuy' || 'đã hủy' => 'Đã hủy',
      _ => value?.toString() ?? 'Chờ chế biến',
    };
  }
}

class OrderService {
  final ApiClient _apiClient;

  const OrderService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<List<KitchenQueueItem>> getKitchenItems({
    required int stationId,
  }) async {
    final dynamic response = await _apiClient.get(
      '/orderitems?departmentId=$stationId',
    );

    if (response is! List) return <KitchenQueueItem>[];

    return response
        .whereType<Map>()
        .map(
          (item) => KitchenQueueItem.fromJson(
            Map<String, dynamic>.from(item),
          ),
        )
        .where((item) => item.item.id != null && item.item.id! > 0)
        .toList();
  }

  Future<List<OrderItem>> addItems({
    required int tableId,
    required String employeeCode,
    required List<CartItem> items,
  }) async {
    final dynamic response = await _apiClient.post(
      '/orders/add-items',
      body: {
        'tableId': tableId,
        'employeeCode': employeeCode,
        // Mỗi món tách tối đa 2 dòng: phần ăn tại chỗ và phần mang về.
        'items': items.expand((item) {
          final int takeaway = item.takeawayQuantity.clamp(0, item.quantity);
          final int dineIn = item.quantity - takeaway;

          return <Map<String, dynamic>>[
            if (dineIn > 0)
              {
                'menuItemId': item.menuItem.id,
                'quantity': dineIn,
                'note': item.note,
                'isTakeaway': false,
              },
            if (takeaway > 0)
              {
                'menuItemId': item.menuItem.id,
                'quantity': takeaway,
                'note': item.note,
                'isTakeaway': true,
              },
          ];
        }).toList(),
      },
    );

    if (response is! Map) {
      return [];
    }

    final dynamic data = response['items'];

    if (data is! List) {
      return [];
    }

    final List<OrderItem> persistedItems = data
        .whereType<Map>()
        .map(
          (item) => OrderItem.fromJson(
            Map<String, dynamic>.from(item),
          ),
        )
        .toList();

    if (persistedItems.isEmpty) {
      throw const ApiException(
        message:
            'Máy chủ không lưu được món vừa gọi. Vui lòng thử lại.',
      );
    }

    return persistedItems;
  }

  Future<String> updateItemStatus({
    required int orderItemId,
    required String status,
  }) async {
    if (status == 'Đã phục vụ') {
      final Set<int> updated = await markItemsServed(
        orderItemIds: <int>[orderItemId],
      );

      if (!updated.contains(orderItemId)) {
        throw const ApiException(
          message: 'Món chưa ở trạng thái Đã xong nên chưa thể phục vụ.',
        );
      }

      return 'Đã phục vụ';
    }

    final int? sharedKitchenStatus = switch (status) {
      'Đang chế biến' => 1,
      'Đã xong' => 2,
      _ => null,
    };

    if (sharedKitchenStatus != null) {
      // Dùng cùng endpoint/trạng thái với WinForms KitchenBoardControl.
      final dynamic response = await _apiClient.put(
        '/orderitems/$orderItemId/status',
        body: {
          'newStatus': sharedKitchenStatus,
          'userId': null,
          'cancelReason': null,
          'rowVer': null,
        },
      );

      if (response is Map) {
        return KitchenQueueItem.statusLabel(
          response['kitchenStatus'] ?? response['KitchenStatus'],
        );
      }
      return status;
    }

    final dynamic response = await _apiClient.put(
      '/orders/items/$orderItemId/status',
      body: {
        'status': status,
      },
    );

    if (response is Map) {
      return response['status']?.toString() ?? status;
    }

    return status;
  }

  Future<int> updateItemQuantity({
    required int orderItemId,
    required int quantity,
  }) async {
    final dynamic response = await _apiClient.put(
      '/mobile/order-actions/items/$orderItemId/quantity',
      body: {
        'quantity': quantity,
        'employeeCode': appEmployee.employeeCode,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể cập nhật số lượng món.',
      );
    }

    final dynamic raw =
        response['quantity'] ??
        response['Quantity'];

    final int savedQuantity = raw is int
        ? raw
        : int.tryParse(raw?.toString() ?? '') ?? 0;

    if (savedQuantity <= 0) {
      throw const ApiException(
        message: 'Máy chủ trả về số lượng món không hợp lệ.',
      );
    }

    return savedQuantity;
  }

  Future<int> decrementItemQuantity({
    required int orderItemId,
  }) async {
    final dynamic response = await _apiClient.post(
      '/mobile/order-actions/items/$orderItemId/decrement',
      body: {
        'orderItemIds': <int>[orderItemId],
        'employeeCode': appEmployee.employeeCode,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể cập nhật số lượng món.',
      );
    }

    final dynamic raw =
        response['quantity'] ??
        response['Quantity'];

    final int quantity = raw is int
        ? raw
        : int.tryParse(raw?.toString() ?? '') ?? 0;

    if (quantity <= 0) {
      throw const ApiException(
        message: 'Máy chủ trả về số lượng món không hợp lệ.',
      );
    }

    return quantity;
  }

  Future<void> cancelItem({
    required int orderItemId,
  }) async {
    final Set<int> updated = await cancelItems(
      orderItemIds: <int>[orderItemId],
    );

    if (!updated.contains(orderItemId)) {
      throw const ApiException(
        message: 'Chỉ có thể hủy món khi bếp chưa bắt đầu chế biến.',
      );
    }
  }

  Future<Set<int>> markItemsServed({
    required List<int> orderItemIds,
  }) async {
    final dynamic response = await _apiClient.post(
      '/mobile/order-actions/served',
      body: {
        'orderItemIds': orderItemIds,
        'employeeCode': appEmployee.employeeCode,
      },
    );

    return _parseUpdatedIds(response);
  }

  Future<Set<int>> cancelItems({
    required List<int> orderItemIds,
  }) async {
    final dynamic response = await _apiClient.post(
      '/mobile/order-actions/cancel',
      body: {
        'orderItemIds': orderItemIds,
        'employeeCode': appEmployee.employeeCode,
      },
    );

    return _parseUpdatedIds(response);
  }

  Future<int> clearTable({
    required int tableId,
    required String employeeCode,
  }) async {
    final dynamic response = await _apiClient.post(
      '/mobile/order-actions/clear-table',
      body: {
        'tableId': tableId,
        'employeeCode': employeeCode,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể xóa món và trả bàn',
      );
    }

    final dynamic raw = response['cancelledCount'];
    if (raw is int) return raw;
    return int.tryParse(raw?.toString() ?? '') ?? 0;
  }

  Set<int> _parseUpdatedIds(dynamic response) {
    if (response is! Map) {
      return <int>{};
    }

    final dynamic rawIds = response['updatedIds'];
    if (rawIds is! List) {
      return <int>{};
    }

    return rawIds
        .map((value) {
          if (value is int) return value;
          return int.tryParse(value?.toString() ?? '');
        })
        .whereType<int>()
        .where((value) => value > 0)
        .toSet();
  }
}