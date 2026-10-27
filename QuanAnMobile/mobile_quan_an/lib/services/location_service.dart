import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LocationCheckResult {
  final bool isConfigured;
  final bool serviceEnabled;
  final bool permissionGranted;
  final bool isInside;
  final double? latitude;
  final double? longitude;
  final double? distanceMeters;
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

    return uri.scheme != 'https' &&
        !localhost;
  }

  static const String _envLatitudeRaw =
      String.fromEnvironment(
    'RESTAURANT_LATITUDE',
    defaultValue: '',
  );

  static const String _envLongitudeRaw =
      String.fromEnvironment(
    'RESTAURANT_LONGITUDE',
    defaultValue: '',
  );

  static const String _envRadiusRaw =
      String.fromEnvironment(
    'RESTAURANT_RADIUS_METERS',
    defaultValue: '100',
  );

  static double get geofenceRadiusMeters {
    return double.tryParse(
          _envRadiusRaw,
        ) ??
        100;
  }

  static const String _debugLatitudeKey =
      'attendance_restaurant_debug_latitude';

  static const String _debugLongitudeKey =
      'attendance_restaurant_debug_longitude';

  bool get _hasEnvironmentLocation {
    return double.tryParse(
              _envLatitudeRaw,
            ) !=
            null &&
        double.tryParse(
              _envLongitudeRaw,
            ) !=
            null;
  }

  Future<({double latitude, double longitude})?>
      _getRestaurantLocation() async {
    if (_hasEnvironmentLocation) {
      return (
        latitude:
            double.parse(_envLatitudeRaw),
        longitude:
            double.parse(_envLongitudeRaw),
      );
    }

    if (!kDebugMode) {
      return null;
    }

    final SharedPreferences prefs =
        await SharedPreferences.getInstance();

    final double? latitude =
        prefs.getDouble(_debugLatitudeKey);

    final double? longitude =
        prefs.getDouble(_debugLongitudeKey);

    if (latitude == null ||
        longitude == null) {
      return null;
    }

    return (
      latitude: latitude,
      longitude: longitude,
    );
  }

  Future<LocationCheckResult>
      checkCurrentLocation({
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
            'Bản Web đang mở bằng HTTP nên Safari không cho truy cập GPS. '
            'Điểm danh trên iPhone Web cần mở ứng dụng bằng HTTPS.',
      );
    }

    final bool serviceEnabled =
        await Geolocator
            .isLocationServiceEnabled();

    if (!serviceEnabled) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: false,
        permissionGranted: false,
        isInside: false,
        message:
            'Dịch vụ vị trí đang tắt.',
      );
    }

    LocationPermission permission =
        await Geolocator.checkPermission();

    if (permission ==
            LocationPermission.denied &&
        requestPermission) {
      permission =
          await Geolocator
              .requestPermission();
    }

    if (permission ==
        LocationPermission.denied) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        message:
            'Bạn chưa cấp quyền vị trí.',
      );
    }

    if (permission ==
        LocationPermission.deniedForever) {
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
      locationSettings:
          const LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: Duration(
          seconds: 12,
        ),
      ),
    );

    final restaurant =
        await _getRestaurantLocation();

    if (restaurant == null) {
      return LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: true,
        isInside: false,
        latitude: position.latitude,
        longitude: position.longitude,
        accuracyMeters:
            position.accuracy,
        isMocked: position.isMocked,
        message:
            'Đã lấy vị trí GPS. Máy chủ sẽ kiểm tra phạm vi nhà hàng khi điểm danh.',
      );
    }

    final double distance =
        Geolocator.distanceBetween(
      position.latitude,
      position.longitude,
      restaurant.latitude,
      restaurant.longitude,
    );

    final bool inside =
        distance <=
            geofenceRadiusMeters;

    if (!kDebugMode &&
        position.isMocked) {
      return LocationCheckResult(
        isConfigured: true,
        serviceEnabled: true,
        permissionGranted: true,
        isInside: false,
        latitude: position.latitude,
        longitude: position.longitude,
        distanceMeters: distance,
        accuracyMeters:
            position.accuracy,
        isMocked: true,
        message:
            'Không thể điểm danh bằng vị trí mô phỏng.',
      );
    }

    return LocationCheckResult(
      isConfigured: true,
      serviceEnabled: true,
      permissionGranted: true,
      isInside: inside,
      latitude: position.latitude,
      longitude: position.longitude,
      distanceMeters: distance,
      accuracyMeters:
          position.accuracy,
      isMocked: position.isMocked,
      message: inside
          ? 'Bạn đang ở trong phạm vi điểm danh.'
          : 'Bạn đang ở ngoài phạm vi điểm danh.',
    );
  }

  Future<LocationCheckResult>
      calibrateDebugRestaurantLocation() async {
    if (!kDebugMode) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        message:
            'Chức năng này chỉ dùng khi debug.',
      );
    }

    final bool serviceEnabled =
        await Geolocator
            .isLocationServiceEnabled();

    if (!serviceEnabled) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: false,
        permissionGranted: false,
        isInside: false,
        message:
            'Dịch vụ vị trí đang tắt.',
      );
    }

    LocationPermission permission =
        await Geolocator.checkPermission();

    if (permission ==
        LocationPermission.denied) {
      permission =
          await Geolocator
              .requestPermission();
    }

    if (permission ==
            LocationPermission.denied ||
        permission ==
            LocationPermission.deniedForever) {
      return const LocationCheckResult(
        isConfigured: false,
        serviceEnabled: true,
        permissionGranted: false,
        isInside: false,
        message:
            'Cần cấp quyền vị trí để cấu hình vị trí test.',
      );
    }

    final Position position =
        await Geolocator.getCurrentPosition(
      locationSettings:
          const LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: Duration(
          seconds: 12,
        ),
      ),
    );

    final SharedPreferences prefs =
        await SharedPreferences.getInstance();

    await prefs.setDouble(
      _debugLatitudeKey,
      position.latitude,
    );

    await prefs.setDouble(
      _debugLongitudeKey,
      position.longitude,
    );

    return LocationCheckResult(
      isConfigured: true,
      serviceEnabled: true,
      permissionGranted: true,
      isInside: true,
      latitude: position.latitude,
      longitude: position.longitude,
      distanceMeters: 0,
      accuracyMeters:
          position.accuracy,
      isMocked: position.isMocked,
      message:
          'Đã lưu vị trí hiện tại làm vị trí nhà hàng để test debug.',
    );
  }

  Future<bool> openLocationSettings() {
    return Geolocator
        .openLocationSettings();
  }

  Future<bool> openAppSettings() {
    return Geolocator.openAppSettings();
  }
}
