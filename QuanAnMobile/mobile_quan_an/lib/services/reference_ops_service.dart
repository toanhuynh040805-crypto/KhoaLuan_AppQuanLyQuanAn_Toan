import 'api_client.dart';

class ReferenceOpsService {
  final ApiClient _apiClient;

  const ReferenceOpsService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<List<String>> getRecentNotes({int take = 8}) async {
    final dynamic response = await _apiClient.get(
      '/mobile/ops/recent-notes?take=$take',
    );

    if (response is! List) return <String>[];

    return response
        .whereType<Map>()
        .map((item) => item['note']?.toString().trim() ?? '')
        .where((note) => note.isNotEmpty)
        .toList();
  }

  Future<Map<String, dynamic>> getOrderLog({
    required int tableId,
  }) async {
    final dynamic response = await _apiClient.get(
      '/mobile/ops/tables/$tableId/order-log',
    );

    if (response is! Map) {
      return <String, dynamic>{
        'tableId': tableId,
        'events': <dynamic>[],
      };
    }

    return Map<String, dynamic>.from(response);
  }

  Future<Map<String, Set<int>>> getKitchenPriorities() async {
    final dynamic response = await _apiClient.get(
      '/mobile/ops/kitchen-priorities',
    );

    final Set<int> itemIds = <int>{};
    final Set<int> tableIds = <int>{};

    if (response is Map) {
      final dynamic rawItems = response['itemIds'];
      final dynamic rawTables = response['tableIds'];

      if (rawItems is List) {
        for (final dynamic raw in rawItems) {
          final int? value = int.tryParse(raw.toString());
          if (value != null && value > 0) itemIds.add(value);
        }
      }

      if (rawTables is List) {
        for (final dynamic raw in rawTables) {
          final int? value = int.tryParse(raw.toString());
          if (value != null && value > 0) tableIds.add(value);
        }
      }
    }

    return <String, Set<int>>{
      'itemIds': itemIds,
      'tableIds': tableIds,
    };
  }

  Future<void> setItemPriority({
    required int orderItemId,
    required String employeeCode,
    required bool active,
  }) async {
    await _apiClient.post(
      '/mobile/ops/priority/item/$orderItemId',
      body: <String, dynamic>{
        'active': active,
        'employeeCode': employeeCode,
      },
    );
  }

  Future<void> setTablePriority({
    required int tableId,
    required String employeeCode,
    required bool active,
  }) async {
    await _apiClient.post(
      '/mobile/ops/priority/table/$tableId',
      body: <String, dynamic>{
        'active': active,
        'employeeCode': employeeCode,
      },
    );
  }

  Future<List<Map<String, dynamic>>> getInvoices({
    required DateTime date,
  }) async {
    final String day =
        '${date.year.toString().padLeft(4, '0')}-'
        '${date.month.toString().padLeft(2, '0')}-'
        '${date.day.toString().padLeft(2, '0')}';

    final dynamic response = await _apiClient.get(
      '/invoices?date=$day',
    );

    if (response is! List) return <Map<String, dynamic>>[];

    return response
        .whereType<Map>()
        .map((item) => Map<String, dynamic>.from(item))
        .toList();
  }

  Future<Map<String, dynamic>> getInvoiceDetail(int invoiceId) async {
    final dynamic response = await _apiClient.get('/invoices/$invoiceId');
    if (response is! Map) {
      throw const ApiException(message: 'Không thể tải chi tiết hóa đơn');
    }
    return Map<String, dynamic>.from(response);
  }
}
