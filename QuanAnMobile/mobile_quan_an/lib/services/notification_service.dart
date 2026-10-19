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
}
