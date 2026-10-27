import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/attendance_service.dart';
import '../../services/location_service.dart';
import '../../widgets/top_message.dart';

class AttendanceLocationSettingsScreen
    extends StatefulWidget {
  const AttendanceLocationSettingsScreen({
    super.key,
  });

  @override
  State<AttendanceLocationSettingsScreen>
      createState() =>
          _AttendanceLocationSettingsScreenState();
}

class _AttendanceLocationSettingsScreenState
    extends State<
        AttendanceLocationSettingsScreen> {
  final TextEditingController
      _radiusController =
      TextEditingController(text: '100');

  final TextEditingController
      _accuracyController =
      TextEditingController(text: '50');

  AttendanceLocationSettingsDto? _settings;
  double? _selectedLatitude;
  double? _selectedLongitude;
  double? _currentAccuracy;
  bool _currentIsMocked = false;
  bool _loading = true;
  bool _capturing = false;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _radiusController.dispose();
    _accuracyController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
    });

    try {
      final settings =
          await AppServices.attendanceService
              .getLocationSettings();

      if (!mounted) return;

      setState(() {
        _settings = settings;
        _selectedLatitude =
            settings.latitude;
        _selectedLongitude =
            settings.longitude;

        _radiusController.text =
            settings.radiusMeters
                .round()
                .toString();

        _accuracyController.text =
            settings.maxAccuracyMeters
                .round()
                .toString();

        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
      });

      TopMessage.show(
        context,
        message: e.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _captureCurrentLocation()
      async {
    if (_capturing) return;

    setState(() {
      _capturing = true;
    });

    try {
      final LocationCheckResult result =
          await AppServices.locationService
              .checkCurrentLocation(
        requestPermission: true,
      );

      if (!mounted) return;

      if (!result.serviceEnabled) {
        TopMessage.show(
          context,
          message:
              'Hãy bật GPS/Dịch vụ vị trí trước.',
          type: TopMessageType.warning,
        );
        return;
      }

      if (!result.permissionGranted) {
        TopMessage.show(
          context,
          message: result.message,
          type: TopMessageType.warning,
        );
        return;
      }

      if (result.latitude == null ||
          result.longitude == null) {
        TopMessage.show(
          context,
          message:
              'Không thể lấy tọa độ hiện tại. Hãy thử lại.',
          type: TopMessageType.error,
        );
        return;
      }

      setState(() {
        _selectedLatitude =
            result.latitude;
        _selectedLongitude =
            result.longitude;
        _currentAccuracy =
            result.accuracyMeters;
        _currentIsMocked =
            result.isMocked;
      });

      TopMessage.show(
        context,
        message:
            'Đã lấy vị trí hiện tại. Bấm Lưu vị trí quán để áp dụng.',
        type: TopMessageType.success,
      );
    } catch (_) {
      if (!mounted) return;

      TopMessage.show(
        context,
        message:
            'Không thể lấy vị trí hiện tại. Hãy kiểm tra GPS và thử lại.',
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() {
          _capturing = false;
        });
      }
    }
  }

  Future<void> _save() async {
    if (_saving) return;

    final double? latitude =
        _selectedLatitude;
    final double? longitude =
        _selectedLongitude;

    if (latitude == null ||
        longitude == null) {
      TopMessage.show(
        context,
        message:
            'Hãy lấy vị trí hiện tại trước khi lưu.',
        type: TopMessageType.warning,
      );
      return;
    }

    final double? radius =
        double.tryParse(
      _radiusController.text.trim(),
    );

    final double? maxAccuracy =
        double.tryParse(
      _accuracyController.text.trim(),
    );

    if (radius == null ||
        radius < 10 ||
        radius > 1000) {
      TopMessage.show(
        context,
        message:
            'Bán kính điểm danh phải từ 10 đến 1000 mét.',
        type: TopMessageType.warning,
      );
      return;
    }

    if (maxAccuracy == null ||
        maxAccuracy < 5 ||
        maxAccuracy > 200) {
      TopMessage.show(
        context,
        message:
            'Sai số GPS cho phép phải từ 5 đến 200 mét.',
        type: TopMessageType.warning,
      );
      return;
    }

    setState(() {
      _saving = true;
    });

    try {
      final saved =
          await AppServices.attendanceService
              .updateLocationSettings(
        latitude: latitude,
        longitude: longitude,
        radiusMeters: radius,
        maxAccuracyMeters: maxAccuracy,
      );

      if (!mounted) return;

      setState(() {
        _settings = saved;
        _selectedLatitude =
            saved.latitude;
        _selectedLongitude =
            saved.longitude;
      });

      TopMessage.show(
        context,
        message:
            'Đã lưu vị trí quán. Nhân viên sẽ được kiểm tra theo vị trí này khi điểm danh.',
        type: TopMessageType.success,
      );
    } on ApiException catch (e) {
      if (!mounted) return;

      TopMessage.show(
        context,
        message: e.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() {
          _saving = false;
        });
      }
    }
  }

  String _coordinate(double? value) {
    if (value == null) return 'Chưa thiết lập';
    return value.toStringAsFixed(6);
  }

  String _updatedAt() {
    final DateTime? value =
        _settings?.updatedAt;

    if (value == null) {
      return 'Chưa từng cập nhật';
    }

    return
        '${value.day.toString().padLeft(2, '0')}/'
        '${value.month.toString().padLeft(2, '0')}/'
        '${value.year} '
        '${value.hour.toString().padLeft(2, '0')}:'
        '${value.minute.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor:
          const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        title: const Text(
          'Vị trí điểm danh',
          style: TextStyle(
            color: AppTheme.textColor,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
      body: _loading
          ? const Center(
              child:
                  CircularProgressIndicator(),
            )
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding:
                    const EdgeInsets.all(16),
                children: [
                  Container(
                    padding:
                        const EdgeInsets.all(
                      16,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius:
                          BorderRadius.circular(
                        14,
                      ),
                      border: Border.all(
                        color:
                            Colors.grey.shade200,
                      ),
                    ),
                    child: Column(
                      crossAxisAlignment:
                          CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(
                              _settings
                                          ?.isConfigured ==
                                      true
                                  ? Icons
                                      .location_on
                                  : Icons
                                      .location_off_outlined,
                              color: _settings
                                          ?.isConfigured ==
                                      true
                                  ? Colors.green
                                  : Colors.orange,
                            ),
                            const SizedBox(
                              width: 10,
                            ),
                            Expanded(
                              child: Text(
                                _settings
                                            ?.isConfigured ==
                                        true
                                    ? 'Vị trí quán đang được áp dụng'
                                    : 'Chưa thiết lập vị trí quán',
                                style:
                                    const TextStyle(
                                  fontSize: 16,
                                  fontWeight:
                                      FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 14),
                        _infoRow(
                          'Vĩ độ',
                          _coordinate(
                            _settings?.latitude,
                          ),
                        ),
                        _infoRow(
                          'Kinh độ',
                          _coordinate(
                            _settings?.longitude,
                          ),
                        ),
                        _infoRow(
                          'Bán kính',
                          '${_settings?.radiusMeters.round() ?? 100} m',
                        ),
                        _infoRow(
                          'Sai số GPS tối đa',
                          '${_settings?.maxAccuracyMeters.round() ?? 50} m',
                        ),
                        _infoRow(
                          'Cập nhật',
                          _updatedAt(),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 14),
                  Container(
                    padding:
                        const EdgeInsets.all(
                      16,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius:
                          BorderRadius.circular(
                        14,
                      ),
                      border: Border.all(
                        color:
                            Colors.grey.shade200,
                      ),
                    ),
                    child: Column(
                      crossAxisAlignment:
                          CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Thiết lập vị trí mới',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight:
                                FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Đứng tại quán, bật GPS rồi lấy vị trí hiện tại. Sau khi lưu, tất cả nhân viên sẽ được kiểm tra theo tọa độ này.',
                          style: TextStyle(
                            fontSize: 12,
                            height: 1.4,
                            color: Colors
                                .grey.shade700,
                          ),
                        ),
                        const SizedBox(height: 16),
                        _infoRow(
                          'Vĩ độ đang chọn',
                          _coordinate(
                            _selectedLatitude,
                          ),
                        ),
                        _infoRow(
                          'Kinh độ đang chọn',
                          _coordinate(
                            _selectedLongitude,
                          ),
                        ),
                        if (_currentAccuracy !=
                            null)
                          _infoRow(
                            'Sai số GPS lúc lấy',
                            '${_currentAccuracy!.round()} m',
                          ),
                        if (_currentIsMocked)
                          Padding(
                            padding:
                                const EdgeInsets
                                    .only(
                              top: 8,
                            ),
                            child: Text(
                              'Thiết bị đang báo vị trí mô phỏng. Khi vận hành thật nên thiết lập bằng điện thoại tại quán.',
                              style: TextStyle(
                                color: Colors
                                    .orange.shade800,
                                fontSize: 12,
                              ),
                            ),
                          ),
                        const SizedBox(height: 14),
                        SizedBox(
                          width:
                              double.infinity,
                          height: 48,
                          child:
                              OutlinedButton.icon(
                            onPressed:
                                _capturing
                                    ? null
                                    : _captureCurrentLocation,
                            icon: _capturing
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child:
                                        CircularProgressIndicator(
                                      strokeWidth:
                                          2,
                                    ),
                                  )
                                : const Icon(
                                    Icons
                                        .my_location,
                                  ),
                            label: Text(
                              _capturing
                                  ? 'Đang lấy vị trí...'
                                  : 'Lấy vị trí hiện tại làm vị trí quán',
                            ),
                          ),
                        ),
                        const SizedBox(height: 14),
                        TextField(
                          controller:
                              _radiusController,
                          keyboardType:
                              TextInputType
                                  .number,
                          decoration:
                              const InputDecoration(
                            labelText:
                                'Bán kính cho phép (m)',
                            helperText:
                                'Khuyến nghị: 50 - 100 m',
                            border:
                                OutlineInputBorder(),
                            prefixIcon: Icon(
                              Icons
                                  .radar_outlined,
                            ),
                          ),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller:
                              _accuracyController,
                          keyboardType:
                              TextInputType
                                  .number,
                          decoration:
                              const InputDecoration(
                            labelText:
                                'Sai số GPS tối đa (m)',
                            helperText:
                                'Khuyến nghị: 30 - 50 m',
                            border:
                                OutlineInputBorder(),
                            prefixIcon: Icon(
                              Icons
                                  .gps_fixed_outlined,
                            ),
                          ),
                        ),
                        const SizedBox(height: 18),
                        SizedBox(
                          width:
                              double.infinity,
                          height: 50,
                          child:
                              FilledButton.icon(
                            onPressed:
                                _saving
                                    ? null
                                    : _save,
                            style:
                                FilledButton
                                    .styleFrom(
                              backgroundColor:
                                  AppTheme
                                      .orangeColor,
                            ),
                            icon: _saving
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child:
                                        CircularProgressIndicator(
                                      strokeWidth:
                                          2,
                                      color:
                                          Colors.white,
                                    ),
                                  )
                                : const Icon(
                                    Icons.save_outlined,
                                  ),
                            label: Text(
                              _saving
                                  ? 'Đang lưu...'
                                  : 'Lưu vị trí quán',
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _infoRow(
    String label,
    String value,
  ) {
    return Padding(
      padding:
          const EdgeInsets.symmetric(
        vertical: 5,
      ),
      child: Row(
        crossAxisAlignment:
            CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 145,
            child: Text(
              label,
              style: TextStyle(
                color:
                    Colors.grey.shade700,
                fontSize: 12,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: const TextStyle(
                fontSize: 12,
                fontWeight:
                    FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
