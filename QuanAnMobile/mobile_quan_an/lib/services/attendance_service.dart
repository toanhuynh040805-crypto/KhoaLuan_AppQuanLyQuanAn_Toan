import '../models/attendance_state.dart';
import 'api_client.dart';
import 'location_service.dart';

class AttendanceLocationSettingsDto {
  final bool isConfigured;
  final double? latitude;
  final double? longitude;
  final double radiusMeters;
  final double maxAccuracyMeters;
  final DateTime? updatedAt;

  const AttendanceLocationSettingsDto({
    required this.isConfigured,
    required this.latitude,
    required this.longitude,
    required this.radiusMeters,
    required this.maxAccuracyMeters,
    required this.updatedAt,
  });

  factory AttendanceLocationSettingsDto.fromJson(
    Map<String, dynamic> json,
  ) {
    double? number(dynamic value) {
      if (value is num) {
        return value.toDouble();
      }
      return double.tryParse(
        value?.toString() ?? '',
      );
    }

    return AttendanceLocationSettingsDto(
      isConfigured:
          json['isConfigured'] == true,
      latitude: number(
        json['latitude'],
      ),
      longitude: number(
        json['longitude'],
      ),
      radiusMeters:
          number(json['radiusMeters']) ??
              100,
      maxAccuracyMeters:
          number(
            json['maxAccuracyMeters'],
          ) ??
              50,
      updatedAt: DateTime.tryParse(
        json['updatedAt']?.toString() ?? '',
      ),
    );
  }
}

class AttendanceService {
  final ApiClient _apiClient;

  const AttendanceService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<AttendanceLocationSettingsDto>
      getLocationSettings() async {
    final dynamic response =
        await _apiClient.get(
      '/attendance-settings',
    );

    if (response is! Map) {
      throw const ApiException(
        message:
            'Không thể tải cấu hình vị trí điểm danh.',
      );
    }

    return AttendanceLocationSettingsDto
        .fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<AttendanceLocationSettingsDto>
      updateLocationSettings({
    required double latitude,
    required double longitude,
    required double radiusMeters,
    required double maxAccuracyMeters,
  }) async {
    final dynamic response =
        await _apiClient.put(
      '/attendance-settings',
      body: {
        'latitude': latitude,
        'longitude': longitude,
        'radiusMeters': radiusMeters,
        'maxAccuracyMeters':
            maxAccuracyMeters,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message:
            'Không thể lưu vị trí nhà hàng.',
      );
    }

    return AttendanceLocationSettingsDto
        .fromJson(
      Map<String, dynamic>.from(response),
    );
  }

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
