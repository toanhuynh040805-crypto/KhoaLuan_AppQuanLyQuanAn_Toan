import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

class LocationCheckResult {
  final bool isConfigured;
  final bool serviceEnabled;
  final bool permissionGranted;
  final bool isInside;
  final double? latitude;
  final double? longitude;
  final double? distanceMeters;
  final double? radiusMeters;
  final double? accuracyMeters;
  final bool isMocked;
  final bool requiresSecureContext;
  final String message;

  const LocationCheckResult({
    required this.isConfigured,
    required this.serviceEnabled,
    required this.permissionGranted,
    required this.isInside,
    this.latitude,
    this.longitude,
    this.distanceMeters,
    this.radiusMeters,
    this.accuracyMeters,
    this.isMocked = false,
    this.requiresSecureContext = false,
    required this.message,
  });

  bool get canAttend {
    return serviceEnabled &&
        permissionGranted &&
        latitude != null &&
        longitude != null &&
        !isMocked &&
        (!isConfigured || isInside);
  }
}

class LocationService {
  const LocationService();

  bool get _isInsecureWebContext {
    if (!kIsWeb) return false;

    final Uri uri = Uri.base;
    final String host = uri.host.toLowerCase();
    final bool localhost =
        host == 'localhost' ||
        host == '127.0.0.1' ||
        host == '::1';

    return uri.scheme != 'https' && !localhost;
  }

  Future<LocationCheckResult> checkCurrentLocation({
    bool requestPermission = true,
  }) async {
    if (_isInsecureWebContext) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        requiresSecureContext: true,
        message:
            'Bản Web đang mở bằng HTTP nên trình duyệt không cho truy cập GPS thật. '
            'Điểm danh trên Web cần chạy bằng HTTPS.',
      );
    }

    final bool serviceEnabled =
        await Geolocator.isLocationServiceEnabled();

    if (!serviceEnabled) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: false,
        permissionGranted: false,
        isInside: false,
        message: 'Dịch vụ vị trí đang tắt.',
      );
    }

    LocationPermission permission =
        await Geolocator.checkPermission();

    if (permission == LocationPermission.denied &&
        requestPermission) {
      permission = await Geolocator.requestPermission();
    }

    if (permission == LocationPermission.denied) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        message: 'Bạn chưa cấp quyền vị trí.',
      );
    }

    if (permission == LocationPermission.deniedForever) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        message:
            'Quyền vị trí đã bị từ chối vĩnh viễn. Hãy bật lại trong Cài đặt ứng dụng.',
      );
    }

    final Position position =
        await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: Duration(seconds: 12),
      ),
    );

    if (position.isMocked) {
      return LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: true,
        isInside: false,
        latitude: position.latitude,
        longitude: position.longitude,
        accuracyMeters: position.accuracy,
        isMocked: true,
        message:
            'Thiết bị đang sử dụng vị trí mô phỏng. Hãy dùng GPS thật trên thiết bị tại quán.',
      );
    }

    return LocationCheckResult(
      isConfigured: false,
      serviceEnabled: true,
      permissionGranted: true,
      isInside: false,
      latitude: position.latitude,
      longitude: position.longitude,
      accuracyMeters: position.accuracy,
      isMocked: false,
      message:
          'Đã lấy GPS thật từ thiết bị. Máy chủ sẽ đối chiếu với vị trí quán đã lưu.',
    );
  }

  Future<bool> openLocationSettings() {
    return Geolocator.openLocationSettings();
  }

  Future<bool> openAppSettings() {
    return Geolocator.openAppSettings();
  }
}
