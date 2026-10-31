import '../models/app_notification.dart';
import 'api_client.dart';

class NotificationService {
  final ApiClient _apiClient;

  const NotificationService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<List<AppNotification>>
      getNotifications({
    required String employeeCode,
  }) async {
    final dynamic response =
        await _apiClient.get(
      '/notifications?employeeCode=$employeeCode',
    );

    dynamic data = response;

    if (response is Map) {
      data =
          response['data'] ??
          response['notifications'] ??
          [];
    }

    if (data is! List) {
      return [];
    }

    return data
        .whereType<Map>()
        .map(
          (item) =>
              AppNotification.fromJson(
            Map<String, dynamic>.from(
              item,
            ),
          ),
        )
        .toList();
  }

  Future<void> markAsRead({
    required int notificationId,
    required String employeeCode,
  }) async {
    await _apiClient.put(
      '/notifications/$notificationId/read',
      body: {
        'employeeCode': employeeCode,
      },
    );
  }

  Future<void> releaseOverdueReservation({
    required int reservationId,
  }) async {
    await _apiClient.post(
      '/reservations/$reservationId/release-overdue',
    );
  }

  Future<void> markAllAsRead({
    required String employeeCode,
  }) async {
    final String encoded = Uri.encodeQueryComponent(employeeCode);
    await _apiClient.put(
      '/notifications/read-all?employeeCode=$encoded',
    );
  }
}
