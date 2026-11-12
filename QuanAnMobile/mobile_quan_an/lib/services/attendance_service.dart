import '../models/attendance_state.dart';
import 'api_client.dart';
import 'location_service.dart';

class AttendanceLocationValidationDto {
  final bool isInside;
  final bool isMocked;
  final double? distanceMeters;
  final double? radiusMeters;
  final double? accuracyMeters;
  final String message;

  const AttendanceLocationValidationDto({
    required this.isInside,
    required this.isMocked,
    this.distanceMeters,
    this.radiusMeters,
    this.accuracyMeters,
    required this.message,
  });

  factory AttendanceLocationValidationDto.fromJson(
    Map<String, dynamic> json,
  ) {
    double? number(dynamic value) =>
        value is num ? value.toDouble() : double.tryParse('$value');

    return AttendanceLocationValidationDto(
      isInside: json['isInside'] == true,
      isMocked: json['isMocked'] == true,
      distanceMeters: number(json['distanceMeters']),
      radiusMeters: number(json['radiusMeters']),
      accuracyMeters: number(json['accuracyMeters']),
      message: json['message']?.toString() ?? '',
    );
  }
}


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
    required double accuracyMeters,
    required bool isMocked,
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
        'accuracyMeters':
            accuracyMeters,
        'isMocked': isMocked,
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

  Future<AttendanceLocationValidationDto> checkLocation({
    required String employeeCode,
    required LocationCheckResult location,
  }) async {
    final dynamic response = await _apiClient.post(
      '/attendance/check-location',
      body: {
        'employeeCode': employeeCode,
        'latitude': location.latitude,
        'longitude': location.longitude,
        'accuracyMeters': location.accuracyMeters,
        'isMocked': location.isMocked,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể kiểm tra vị trí điểm danh.',
      );
    }

    return AttendanceLocationValidationDto.fromJson(
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
        'isMocked': location.isMocked,
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
        'isMocked': location.isMocked,
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