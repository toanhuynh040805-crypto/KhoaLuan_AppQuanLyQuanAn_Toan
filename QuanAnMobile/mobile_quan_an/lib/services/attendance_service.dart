import '../models/attendance_state.dart';
import 'api_client.dart';
import 'location_service.dart';

class AttendanceService {
  final ApiClient _apiClient;

  const AttendanceService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<void> loadToday({
    required String employeeCode,
    required AttendanceState state,
  }) async {
    final dynamic response =
        await _apiClient.get(
      '/attendance/today?employeeCode=$employeeCode',
    );

    if (response is! Map) {
      throw const ApiException(
        message:
            'Không thể tải ca làm hôm nay',
      );
    }

    state.applyTodayJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<void> checkIn({
    required String employeeCode,
    required AttendanceState state,
    required LocationCheckResult location,
  }) async {
    await _apiClient.post(
      '/attendance/check-in',
      body: {
        'employeeCode': employeeCode,
        'latitude': location.latitude,
        'longitude': location.longitude,
        'accuracyMeters': location.accuracyMeters,
      },
    );

    await loadToday(
      employeeCode: employeeCode,
      state: state,
    );
  }

  Future<void> checkOut({
    required String employeeCode,
    required AttendanceState state,
    required LocationCheckResult location,
  }) async {
    await _apiClient.post(
      '/attendance/check-out',
      body: {
        'employeeCode': employeeCode,
        'latitude': location.latitude,
        'longitude': location.longitude,
        'accuracyMeters': location.accuracyMeters,
      },
    );

    await loadToday(
      employeeCode: employeeCode,
      state: state,
    );

    await loadHistory(
      employeeCode: employeeCode,
      state: state,
    );
  }

  Future<void> loadHistory({
    required String employeeCode,
    required AttendanceState state,
  }) async {
    final dynamic response =
        await _apiClient.get(
      '/attendance/history?employeeCode=$employeeCode',
    );

    if (response is! List) {
      state.replaceHistory([]);
      return;
    }

    final List<ShiftRecord> records =
        response
            .whereType<Map>()
            .map(
              (item) =>
                  ShiftRecord.fromJson(
                Map<String, dynamic>.from(
                  item,
                ),
              ),
            )
            .toList();

    state.replaceHistory(records);
  }
}
