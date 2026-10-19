import 'dart:math';

import 'api_client.dart';

class TableLockLease {
  final int tableId;
  final String clientToken;
  final DateTime? expiresAtUtc;

  const TableLockLease({
    required this.tableId,
    required this.clientToken,
    this.expiresAtUtc,
  });
}

/// Giữ quyền thao tác một bàn theo lease ngắn hạn.
/// TableDetail gửi heartbeat định kỳ; backend tự giải phóng nếu thiết bị mất kết nối.
class TableLockService {
  final ApiClient _apiClient;
  final Random _random = Random.secure();

  TableLockService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<TableLockLease> acquire({
    required int tableId,
    required String employeeCode,
  }) async {
    final String token = _newToken();
    final dynamic response = await _apiClient.post(
      '/mobile/table-locks/$tableId/acquire',
      body: <String, dynamic>{
        'employeeCode': employeeCode,
        'clientToken': token,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể khóa bàn để thao tác',
      );
    }

    final String serverToken =
        response['clientToken']?.toString().trim() ?? '';

    return TableLockLease(
      tableId: tableId,
      clientToken: serverToken.isEmpty ? token : serverToken,
      expiresAtUtc: DateTime.tryParse(
        response['expiresAtUtc']?.toString() ?? '',
      ),
    );
  }

  Future<void> heartbeat({
    required int tableId,
    required String employeeCode,
    required String clientToken,
  }) async {
    await _apiClient.post(
      '/mobile/table-locks/$tableId/heartbeat',
      body: <String, dynamic>{
        'employeeCode': employeeCode,
        'clientToken': clientToken,
      },
    );
  }

  Future<void> release({
    required int tableId,
    required String employeeCode,
    required String clientToken,
  }) async {
    await _apiClient.post(
      '/mobile/table-locks/$tableId/release',
      body: <String, dynamic>{
        'employeeCode': employeeCode,
        'clientToken': clientToken,
      },
    );
  }

  String _newToken() {
    final StringBuffer buffer = StringBuffer(
      '${DateTime.now().microsecondsSinceEpoch.toRadixString(16)}-',
    );
    for (int i = 0; i < 24; i++) {
      buffer.write(_random.nextInt(16).toRadixString(16));
    }
    return buffer.toString();
  }
}
