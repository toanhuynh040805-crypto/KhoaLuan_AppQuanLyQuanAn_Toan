import '../models/order_item.dart';
import '../models/restaurant_table.dart';
import 'api_client.dart';

class TableService {
  final ApiClient _apiClient;

  const TableService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<List<RestaurantTable>> getTables() async {
    final dynamic response =
        await _apiClient.get('/mobile/dining/tables');

    dynamic data = response;

    if (response is Map) {
      data =
          response['data'] ??
          response['tables'] ??
          [];
    }

    if (data is! List) {
      return [];
    }

    bool restricted = false;
    final Set<int> assignedAreaIds = <int>{};

    try {
      final dynamic accessResponse =
          await _apiClient.get('/area-assignments/me');

      if (accessResponse is Map) {
        restricted = accessResponse['restricted'] == true;
        final dynamic rawIds = accessResponse['areaIds'];

        if (rawIds is List) {
          for (final dynamic raw in rawIds) {
            final int parsed = _parseInt(raw);
            if (parsed > 0) assignedAreaIds.add(parsed);
          }
        }
      }
    } catch (_) {
      // Nếu endpoint phân công tạm thời không khả dụng thì bỏ dấu cảnh báo.
      // Phân công khu vực chỉ mang tính nhắc việc, không khóa thao tác nghiệp vụ.
      restricted = false;
      assignedAreaIds.clear();
    }

    final List<RestaurantTable> parsedTables = data
        .whereType<Map>()
        .map(
          (item) => RestaurantTable.fromJson(
            Map<String, dynamic>.from(item),
          ),
        )
        .where((table) => table.id > 0 && table.name.trim().isNotEmpty)
        .map(
          (table) => table.copyWith(
            canOperate: !restricted ||
                (table.areaId != null &&
                    assignedAreaIds.contains(table.areaId)),
          ),
        )
        .toList();

    // Một số dữ liệu cũ có nhiều DiningTable khác TableId nhưng lại cùng
    // số bàn trong cùng khu vực. Mobile chỉ nên hiển thị một card duy nhất.
    // Nếu trùng tên, ưu tiên bản đang phục vụ/đã đặt và có dữ liệu nghiệp vụ;
    // nếu vẫn bằng nhau thì lấy TableId mới hơn. Không xóa dữ liệu lịch sử DB.
    final Map<String, RestaurantTable> uniqueTables = {};

    for (final RestaurantTable table in parsedTables) {
      final String key = _tableIdentity(table);
      final RestaurantTable? current = uniqueTables[key];

      if (current == null || _shouldReplaceTable(current, table)) {
        uniqueTables[key] = table;
      }
    }

    return uniqueTables.values.toList();
  }

  Future<Map<int, List<OrderItem>>> getInitialOrders() async {
    final dynamic response = await _apiClient.get(
      '/tables/orders',
    );

    dynamic data = response;

    if (response is Map) {
      data =
          response['data'] ??
          response['orders'] ??
          [];
    }

    if (data is! List) {
      return {};
    }

    final Map<int, List<OrderItem>> result = {};

    for (final dynamic tableOrder in data) {
      if (tableOrder is! Map) {
        continue;
      }

      final Map<String, dynamic> map =
          Map<String, dynamic>.from(
        tableOrder,
      );

      final int tableId = _parseInt(map['tableId']);

      final dynamic itemsData = map['items'];

      if (tableId == 0 || itemsData is! List) {
        continue;
      }

      result[tableId] = itemsData
          .whereType<Map>()
          .map(
            (item) => OrderItem.fromJson(
              Map<String, dynamic>.from(item),
            ),
          )
          .toList();
    }

    return result;
  }

  Future<void> receiveTable({
    required int tableId,
    int customerCount = 0,
  }) async {
    await _apiClient.put(
      '/mobile/dining/tables/$tableId/receive',
      body: {
        'customerCount': customerCount,
      },
    );
  }

  Future<int?> updateGuestCount({
    required int tableId,
    required int? guestCount,
  }) async {
    final dynamic response = await _apiClient.put(
      '/mobile/dining/tables/$tableId/guest-count',
      body: {
        'guestCount': guestCount ?? 0,
      },
    );

    if (response is Map) {
      final int value = _parseInt(response['guestCount']);
      return value > 0 ? value : null;
    }

    return guestCount != null && guestCount > 0
        ? guestCount
        : null;
  }

  Future<RestaurantTable> openTakeaway() async {
    final dynamic response = await _apiClient.post(
      '/mobile/dining/takeaway/open',
    );

    if (response is! Map) {
      throw const FormatException(
        'Dữ liệu đơn mang về không hợp lệ',
      );
    }

    return RestaurantTable.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<void> releaseTable({
    required int tableId,
  }) async {
    await _apiClient.put(
      '/mobile/dining/tables/$tableId/release',
    );
  }

  Future<void> transferTable({
    required int sourceTableId,
    required int targetTableId,
  }) async {
    await _apiClient.post(
      '/tables/transfer',
      body: {
        'sourceTableId': sourceTableId,
        'targetTableId': targetTableId,
      },
    );
  }

  Future<void> mergeTable({
    required int sourceTableId,
    required int targetTableId,
  }) async {
    await _apiClient.post(
      '/tables/merge',
      body: {
        'sourceTableId': sourceTableId,
        'targetTableId': targetTableId,
      },
    );
  }

  Future<void> linkTables({
    required int primaryTableId,
    required List<int> tableIds,
    required String employeeCode,
  }) async {
    await _apiClient.post(
      '/tables/link',
      body: {
        'primaryTableId': primaryTableId,
        'tableIds': tableIds,
        'employeeCode': employeeCode,
      },
    );
  }

  Future<void> splitTable({
    required int sourceTableId,
    required int targetTableId,
    required List<Map<String, dynamic>> items,
  }) async {
    await _apiClient.post(
      '/tables/split',
      body: {
        'sourceTableId': sourceTableId,
        'targetTableId': targetTableId,
        'items': items,
      },
    );
  }

  String _tableIdentity(RestaurantTable table) {
    final String normalizedArea = table.areaId != null
        ? 'id:${table.areaId}'
        : 'name:${_normalizeKey(table.areaName ?? '')}';
    final String normalizedName = _normalizeKey(table.name);

    return '$normalizedArea|$normalizedName';
  }

  String _normalizeKey(String value) {
    return value
        .trim()
        .toLowerCase()
        .replaceAll(RegExp(r'\s+'), ' ');
  }

  bool _shouldReplaceTable(
    RestaurantTable current,
    RestaurantTable candidate,
  ) {
    final int currentPriority = _tableDisplayPriority(current);
    final int candidatePriority = _tableDisplayPriority(candidate);

    if (candidatePriority != currentPriority) {
      return candidatePriority > currentPriority;
    }

    return candidate.id > current.id;
  }

  int _tableDisplayPriority(RestaurantTable table) {
    int score;

    switch (table.status) {
      case TableStatus.occupied:
        score = 300;
        break;
      case TableStatus.reserved:
        score = 200;
        break;
      case TableStatus.empty:
        score = 100;
        break;
    }

    if ((table.amount ?? 0) > 0) {
      score += 40;
    }
    if ((table.guestCount ?? 0) > 0) {
      score += 20;
    }
    if ((table.customerName ?? '').trim().isNotEmpty) {
      score += 10;
    }
    if ((table.time ?? '').trim().isNotEmpty) {
      score += 5;
    }

    return score;
  }

  int _parseInt(dynamic value) {
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
