import 'api_client.dart';

class OrderAuditService {
  final ApiClient _apiClient;

  const OrderAuditService({required ApiClient apiClient})
      : _apiClient = apiClient;

  Future<Map<String, dynamic>> getAudit({
    required DateTime date,
    int? tableId,
    int? paymentRequestedByUserId,
  }) async {
    final List<String> query = <String>[
      'date=${date.year.toString().padLeft(4, '0')}-'
          '${date.month.toString().padLeft(2, '0')}-'
          '${date.day.toString().padLeft(2, '0')}',
      if (tableId != null) 'tableId=$tableId',
      if (paymentRequestedByUserId != null)
        'paymentRequestedByUserId=$paymentRequestedByUserId',
    ];

    final dynamic response =
        await _apiClient.get('/order-audit?${query.join('&')}');

    if (response is Map<String, dynamic>) {
      return response;
    }
    if (response is Map) {
      return response.map(
        (key, value) => MapEntry(key.toString(), value),
      );
    }

    return <String, dynamic>{};
  }
}
