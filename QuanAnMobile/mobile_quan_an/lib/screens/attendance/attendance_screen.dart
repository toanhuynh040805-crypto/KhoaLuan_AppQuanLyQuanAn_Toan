import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/attendance_state.dart';
import '../../models/employee_profile.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../services/location_service.dart';
import '../../widgets/top_message.dart';
import 'shift_history_screen.dart';

class AttendanceScreen extends StatefulWidget {
  final AttendanceState attendanceState;

  const AttendanceScreen({
    super.key,
    required this.attendanceState,
  });

  @override
  State<AttendanceScreen> createState() =>
      _AttendanceScreenState();
}

class _AttendanceScreenState
    extends State<AttendanceScreen> {
  DateTime _currentTime = DateTime.now();
  Timer? _clockTimer;

  bool _isLoading = true;
  bool _isSubmitting = false;
  bool _isCheckingLocation = false;

  LocationCheckResult? _locationResult;

  @override
  void initState() {
    super.initState();

    _clockTimer = Timer.periodic(
      const Duration(seconds: 1),
      (_) {
        if (!mounted) return;

        setState(() {
          _currentTime = DateTime.now();
        });
      },
    );

    _loadAttendance();
    _refreshLocation(
      showMessage: false,
      requestPermission: false,
    );
  }

  @override
  void dispose() {
    _clockTimer?.cancel();
    super.dispose();
  }

  Future<void> _loadAttendance() async {
    try {
      await AppServices.attendanceService.loadToday(
        employeeCode: appEmployee.employeeCode,
        state: widget.attendanceState,
      );

      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<LocationCheckResult?> _refreshLocation({
    bool showMessage = false,
    bool requestPermission = true,
  }) async {
    if (_isCheckingLocation) {
      return _locationResult;
    }

    setState(() {
      _isCheckingLocation = true;
    });

    try {
      final LocationCheckResult result =
          await AppServices.locationService
              .checkCurrentLocation(
        requestPermission: requestPermission,
      );

      if (!mounted) {
        return result;
      }

      setState(() {
        _locationResult = result;
        _isCheckingLocation = false;
      });

      if (showMessage) {
        TopMessage.show(
          context,
          message: result.message,
          type: result.canAttend
              ? TopMessageType.success
              : TopMessageType.warning,
        );
      }

      return result;
    } catch (_) {
      if (!mounted) {
        return null;
      }

      setState(() {
        _isCheckingLocation = false;
      });

      if (showMessage) {
        TopMessage.show(
          context,
          message:
              'Không thể lấy vị trí hiện tại. Hãy thử lại.',
          type: TopMessageType.error,
        );
      }

      return null;
    }
  }

  Future<LocationCheckResult?>
      _validateLocationForAttendance() async {
    final LocationCheckResult? result =
        await _refreshLocation(
      showMessage: false,
      requestPermission: true,
    );

    if (!mounted || result == null) {
      return null;
    }

    if (!result.serviceEnabled) {
      TopMessage.show(
        context,
        message:
            'Hãy bật GPS/Dịch vụ vị trí trước khi điểm danh.',
        type: TopMessageType.warning,
      );
      return null;
    }

    if (!result.permissionGranted) {
      TopMessage.show(
        context,
        message: result.message,
        type: TopMessageType.warning,
      );
      return null;
    }

    if (result.latitude == null ||
        result.longitude == null ||
        result.accuracyMeters == null) {
      TopMessage.show(
        context,
        message:
            'Không thể lấy tọa độ GPS hiện tại. Hãy thử lại.',
        type: TopMessageType.warning,
      );
      return null;
    }

    if (result.isConfigured &&
        !result.isInside) {
      final double? distance =
          result.distanceMeters;

      TopMessage.show(
        context,
        message: distance == null
            ? result.message
            : 'Bạn đang cách nhà hàng khoảng '
                '${distance.round()} m. '
                'Chỉ được điểm danh trong bán kính '
                '${LocationService.geofenceRadiusMeters.round()} m.',
        type: TopMessageType.warning,
      );
      return null;
    }

    return result;
  }

  Future<void> _calibrateDebugLocation() async {
    if (!kDebugMode ||
        _isCheckingLocation) {
      return;
    }

    setState(() {
      _isCheckingLocation = true;
    });

    try {
      final LocationCheckResult result =
          await AppServices.locationService
              .calibrateDebugRestaurantLocation();

      if (!mounted) return;

      setState(() {
        _locationResult = result;
        _isCheckingLocation = false;
      });

      TopMessage.show(
        context,
        message: result.message,
        type: result.canAttend
            ? TopMessageType.success
            : TopMessageType.warning,
      );
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _isCheckingLocation = false;
      });

      TopMessage.show(
        context,
        message:
            'Không thể lưu vị trí test. Hãy kiểm tra GPS.',
        type: TopMessageType.error,
      );
    }
  }

  DateTime? get _shiftStartDateTime {
    final AttendanceState state =
        widget.attendanceState;

    if (!state.hasShift) {
      return null;
    }

    final DateTime baseDate =
        state.attendanceDate ??
            _currentTime;

    return _shiftDateTime(
      baseDate,
      state.shiftStartTime,
    );
  }

  DateTime? get _shiftEndDateTime {
    final AttendanceState state =
        widget.attendanceState;

    if (!state.hasShift) {
      return null;
    }

    final DateTime baseDate =
        state.attendanceDate ??
            _currentTime;

    final DateTime? start =
        _shiftStartDateTime;

    DateTime? end = _shiftDateTime(
      baseDate,
      state.shiftEndTime,
    );

    if (start != null &&
        end != null &&
        !end.isAfter(start)) {
      end = end.add(
        const Duration(days: 1),
      );
    }

    return end;
  }

  DateTime? get _checkInAllowedFrom {
    return _shiftStartDateTime?.subtract(
      const Duration(minutes: 30),
    );
  }

  bool get _isTooEarlyToCheckIn {
    final DateTime? allowedFrom =
        _checkInAllowedFrom;

    return allowedFrom != null &&
        _currentTime.isBefore(allowedFrom);
  }

  bool get _isShiftEndedBeforeCheckIn {
    final DateTime? shiftEnd =
        _shiftEndDateTime;

    return !widget.attendanceState.checkedIn &&
        shiftEnd != null &&
        !_currentTime.isBefore(shiftEnd);
  }

  String get _checkInButtonLabel {
    final AttendanceState state =
        widget.attendanceState;

    if (state.status == 'VangMat') {
      return 'Ca đã đánh dấu vắng mặt';
    }

    if (_isShiftEndedBeforeCheckIn) {
      return 'Ca đã kết thúc';
    }

    final DateTime? allowedFrom =
        _checkInAllowedFrom;

    if (_isTooEarlyToCheckIn &&
        allowedFrom != null) {
      return 'Điểm danh từ ${_formatTime(allowedFrom)}';
    }

    return 'Điểm danh vào ca';
  }

  Future<void> _checkIn() async {
    if (_isSubmitting ||
        _isCheckingLocation ||
        !widget.attendanceState.hasShift ||
        widget.attendanceState.checkedIn) {
      return;
    }

    if (widget.attendanceState.status ==
        'VangMat') {
      TopMessage.show(
        context,
        message:
            'Ca làm đã được đánh dấu vắng mặt. '
            'Vui lòng liên hệ quản lý nếu cần điều chỉnh.',
        type: TopMessageType.warning,
      );
      return;
    }

    if (_isShiftEndedBeforeCheckIn) {
      TopMessage.show(
        context,
        message:
            'Ca làm đã kết thúc. '
            'Không thể điểm danh vào ca này.',
        type: TopMessageType.warning,
      );
      return;
    }

    final DateTime? allowedFrom =
        _checkInAllowedFrom;

    if (allowedFrom != null &&
        _currentTime.isBefore(allowedFrom)) {
      TopMessage.show(
        context,
        message:
            'Chưa đến thời gian điểm danh. '
            'Bạn có thể điểm danh từ '
            '${_formatTime(allowedFrom)}.',
        type: TopMessageType.warning,
      );
      return;
    }

    final LocationCheckResult? location =
        await _validateLocationForAttendance();

    if (location == null || !mounted) {
      return;
    }

    setState(() {
      _isSubmitting = true;
    });

    try {
      await AppServices.attendanceService.checkIn(
        employeeCode: appEmployee.employeeCode,
        state: widget.attendanceState,
        location: location,
      );

      if (!mounted) return;

      setState(() {
        _isSubmitting = false;
      });

      TopMessage.show(
        context,
        message: 'Điểm danh vào ca thành công',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      setState(() {
        _isSubmitting = false;
      });

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _checkOut() async {
    if (_isSubmitting ||
        _isCheckingLocation ||
        !widget.attendanceState.checkedIn ||
        widget.attendanceState.checkedOut) {
      return;
    }

    final LocationCheckResult? location =
        await _validateLocationForAttendance();

    if (location == null || !mounted) {
      return;
    }

    setState(() {
      _isSubmitting = true;
    });

    try {
      await AppServices.attendanceService.checkOut(
        employeeCode: appEmployee.employeeCode,
        state: widget.attendanceState,
        location: location,
      );

      if (!mounted) return;

      setState(() {
        _isSubmitting = false;
      });

      TopMessage.show(
        context,
        message: 'Kết thúc ca thành công',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      setState(() {
        _isSubmitting = false;
      });

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  String _formatTime(DateTime? time) {
    if (time == null) {
      return '--:--';
    }

    final String hour =
        time.hour.toString().padLeft(2, '0');
    final String minute =
        time.minute.toString().padLeft(2, '0');

    return '$hour:$minute';
  }

  String _formatDate(DateTime date) {
    final String day =
        date.day.toString().padLeft(2, '0');
    final String month =
        date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  String _formatHeaderDate(DateTime date) {
    const List<String> weekdays = [
      'Thứ Hai',
      'Thứ Ba',
      'Thứ Tư',
      'Thứ Năm',
      'Thứ Sáu',
      'Thứ Bảy',
      'Chủ Nhật',
    ];

    return '${weekdays[date.weekday - 1]}, '
        '${date.day} Tháng ${date.month}';
  }

  TimeOfDay? _parseClock(String value) {
    final List<String> parts =
        value.split(':');

    if (parts.length < 2) {
      return null;
    }

    final int? hour =
        int.tryParse(parts[0]);
    final int? minute =
        int.tryParse(parts[1]);

    if (hour == null || minute == null) {
      return null;
    }

    return TimeOfDay(
      hour: hour,
      minute: minute,
    );
  }

  DateTime? _shiftDateTime(
    DateTime date,
    String clock,
  ) {
    final TimeOfDay? time =
        _parseClock(clock);

    if (time == null) {
      return null;
    }

    return DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
  }

  String _getCheckInStatus() {
    final AttendanceState state =
        widget.attendanceState;

    if (state.status == 'VangMat') {
      return 'Vắng mặt';
    }

    if (!state.checkedIn) {
      return 'Chưa điểm danh';
    }

    if (state.lateMinutes > 0) {
      return 'Đi trễ ${state.lateMinutes} phút';
    }

    return 'Đúng giờ';
  }

  String _getCheckOutStatus() {
    final AttendanceState state =
        widget.attendanceState;

    if (state.status == 'VangMat') {
      return 'Không áp dụng';
    }

    if (!state.checkedOut) {
      return 'Chưa kết thúc ca';
    }

    if (state.earlyLeaveMinutes > 0) {
      return 'Về sớm ${state.earlyLeaveMinutes} phút';
    }

    return 'Đủ giờ';
  }

  String _getAttendanceOverallStatus() {
    final AttendanceState state =
        widget.attendanceState;

    switch (state.attendanceStatus) {
      case 'VangMat':
        return 'Vắng mặt';
      case 'DiTre_VeSom':
        return 'Đi trễ & về sớm';
      case 'DiTre':
        return 'Đi trễ';
      case 'VeSom':
        return 'Về sớm';
      case 'DungGio_DuGio':
        return 'Đúng giờ • Đủ giờ';
      case 'DangLam_DiTre':
        return 'Đang làm • Đi trễ';
      case 'DangLam_DungGio':
        return 'Đang làm • Đúng giờ';
      default:
        return state.checkedIn
            ? 'Đang trong ca'
            : 'Chưa điểm danh';
    }
  }

  Color _getCheckInStatusColor() {
    final AttendanceState state =
        widget.attendanceState;

    if (!state.checkedIn) {
      return Colors.grey;
    }

    return state.lateMinutes > 0
        ? Colors.red
        : Colors.green;
  }

  Color _getCheckOutStatusColor() {
    final AttendanceState state =
        widget.attendanceState;

    if (!state.checkedOut) {
      return Colors.grey;
    }

    return state.earlyLeaveMinutes > 0
        ? Colors.orange
        : Colors.green;
  }

  Color _getAttendanceOverallColor() {
    final AttendanceState state =
        widget.attendanceState;

    if (state.status == 'VangMat') {
      return Colors.red;
    }

    if (!state.checkedIn) {
      return Colors.grey;
    }

    if (state.lateMinutes > 0 ||
        state.earlyLeaveMinutes > 0) {
      return Colors.red;
    }

    return Colors.green;
  }

  String get _shiftLabel {
    final AttendanceState state =
        widget.attendanceState;

    if (!state.hasShift) {
      return 'Chưa được xếp ca';
    }

    return '${state.shiftName}: '
        '${state.shiftStartTime} - ${state.shiftEndTime}';
  }

  @override
  Widget build(BuildContext context) {
    final AttendanceState state =
        widget.attendanceState;

    return Scaffold(
      backgroundColor:
          const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          onPressed: () {
            Navigator.pop(context);
          },
          icon: const Icon(
            Icons.arrow_back,
            color: AppTheme.textColor,
          ),
        ),
        title: const Text(
          'Điểm danh',
          style: TextStyle(
            color: AppTheme.textColor,
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),

      body: _isLoading
          ? const Center(
              child:
                  CircularProgressIndicator(),
            )
          : RefreshIndicator(
              onRefresh: _loadAttendance,
              child: SingleChildScrollView(
                physics:
                    const AlwaysScrollableScrollPhysics(),
                padding:
                    const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment:
                      CrossAxisAlignment.start,
                  children: [
                    Center(
                      child: Column(
                        children: [
                          Text(
                            _formatTime(
                              _currentTime,
                            ),
                            style:
                                const TextStyle(
                              fontSize: 34,
                              fontWeight:
                                  FontWeight.bold,
                              color:
                                  AppTheme.textColor,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            _formatHeaderDate(
                              _currentTime,
                            ),
                            style: TextStyle(
                              fontSize: 14,
                              color:
                                  Colors.grey.shade700,
                            ),
                          ),
                          const SizedBox(height: 16),
                          Container(
                            padding:
                                const EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 9,
                            ),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius:
                                  BorderRadius.circular(24),
                              border: Border.all(
                                color:
                                    Colors.grey.shade300,
                              ),
                            ),
                            child: Row(
                              mainAxisSize:
                                  MainAxisSize.min,
                              children: [
                                const Icon(
                                  Icons.work_outline,
                                  size: 20,
                                  color:
                                      AppTheme.primaryColor,
                                ),
                                const SizedBox(width: 8),
                                Flexible(
                                  child: Text(
                                    _shiftLabel,
                                    style:
                                        const TextStyle(
                                      fontSize: 14,
                                      fontWeight:
                                          FontWeight.w600,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 24),
                    _buildEmployeeCard(),
                    const SizedBox(height: 16),
                    _buildLocationCard(),
                    const SizedBox(height: 20),

                    const Text(
                      'Ca làm hôm nay',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight:
                            FontWeight.bold,
                      ),
                    ),

                    const SizedBox(height: 10),
                    _buildShiftCard(),
                    const SizedBox(height: 16),
                    _buildCurrentStatusCard(),
                    const SizedBox(height: 24),

                    SizedBox(
                      width: double.infinity,
                      height: 52,
                      child: ElevatedButton.icon(
                        onPressed:
                            _isSubmitting ||
                                    _isCheckingLocation ||
                                    !state.hasShift ||
                                    state.status ==
                                        'VangMat' ||
                                    _isShiftEndedBeforeCheckIn
                                ? null
                                : !state.checkedIn
                                    ? _checkIn
                                    : state.checkedOut
                                        ? null
                                        : _checkOut,
                        icon: (_isSubmitting ||
                                _isCheckingLocation)
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child:
                                    CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : Icon(
                                !state.checkedIn
                                    ? Icons.fingerprint
                                    : Icons.logout,
                              ),
                        label: Text(
                          _isCheckingLocation
                              ? 'Đang kiểm tra vị trí...'
                              : !state.hasShift
                              ? 'Chưa được xếp ca'
                              : !state.checkedIn
                                  ? _checkInButtonLabel
                                  : state.checkedOut
                                      ? 'Đã kết thúc ca'
                                      : 'Kết thúc ca',
                        ),
                        style:
                            ElevatedButton.styleFrom(
                          backgroundColor:
                              !state.checkedIn
                                  ? AppTheme.orangeColor
                                  : Colors.red.shade700,
                          foregroundColor:
                              Colors.white,
                          disabledBackgroundColor:
                              Colors.grey.shade300,
                          disabledForegroundColor:
                              Colors.grey.shade600,
                          elevation: 0,
                          shape:
                              RoundedRectangleBorder(
                            borderRadius:
                                BorderRadius.circular(10),
                          ),
                        ),
                      ),
                    ),

                    const SizedBox(height: 12),

                    SizedBox(
                      width: double.infinity,
                      height: 48,
                      child:
                          OutlinedButton.icon(
                        onPressed: () async {
                          await Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (context) =>
                                  ShiftHistoryScreen(
                                attendanceState:
                                    state,
                              ),
                            ),
                          );

                          if (!mounted) return;
                          setState(() {});
                        },
                        icon: const Icon(
                          Icons.history,
                          size: 21,
                        ),
                        label: const Text(
                          'Lịch sử điểm danh',
                        ),
                        style:
                            OutlinedButton.styleFrom(
                          foregroundColor:
                              AppTheme.primaryColor,
                          side: const BorderSide(
                            color:
                                AppTheme.primaryColor,
                          ),
                          shape:
                              RoundedRectangleBorder(
                            borderRadius:
                                BorderRadius.circular(10),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildEmployeeCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius:
            BorderRadius.circular(14),
        border: Border.all(
          color: Colors.grey.shade200,
        ),
      ),
      child: Row(
        children: [
          const CircleAvatar(
            radius: 28,
            backgroundColor:
                Color(0xFFFFE9DE),
            child: Icon(
              Icons.person,
              size: 34,
              color: AppTheme.orangeColor,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Text(
                  appEmployee.fullName,
                  style: const TextStyle(
                    fontSize: 17,
                    fontWeight:
                        FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  appEmployee.area.trim().isEmpty
                      ? appEmployee.role
                      : '${appEmployee.role} - ${appEmployee.area}',
                  style: const TextStyle(
                    fontSize: 13,
                    color: Colors.grey,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildShiftCard() {
    final AttendanceState state =
        widget.attendanceState;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius:
            BorderRadius.circular(14),
        border: Border.all(
          color: Colors.grey.shade200,
        ),
      ),
      child: Column(
        children: [
          _buildInfoRow(
            icon:
                Icons.calendar_today_outlined,
            title: 'Ngày',
            value:
                _formatDate(
              state.attendanceDate ??
                  _currentTime,
            ),
          ),
          const Divider(height: 28),
          _buildInfoRow(
            icon:
                Icons.access_time_outlined,
            title: 'Ca làm',
            value: state.hasShift
                ? '${state.shiftStartTime} - ${state.shiftEndTime}'
                : '--:--',
          ),
           const Divider(height: 28),
          _buildInfoRow(
            icon: Icons.place_outlined,
            title: 'Khu vực',
            value: state.hasShift &&
                    state.areaName.isNotEmpty
                ? state.areaName
                : 'Chưa phân khu vực',
          ),
          const Divider(height: 28),
          _buildInfoRow(
            icon: Icons.login_outlined,
            title: 'Giờ vào',
            value:
                _formatTime(state.checkInTime),
          ),
          const Divider(height: 28),
          _buildInfoRow(
            icon: Icons.logout_outlined,
            title: 'Giờ ra',
            value:
                _formatTime(state.checkOutTime),
          ),
          const Divider(height: 28),
          _buildStatusRow(
            icon: Icons.login,
            title: 'Trạng thái vào',
            value: _getCheckInStatus(),
            color:
                _getCheckInStatusColor(),
          ),
          const Divider(height: 28),
          _buildStatusRow(
            icon: Icons.logout,
            title: 'Trạng thái ra',
            value: _getCheckOutStatus(),
            color:
                _getCheckOutStatusColor(),
          ),
          const Divider(height: 28),
          _buildStatusRow(
            icon: Icons.fact_check_outlined,
            title: 'Trạng thái ca',
            value:
                _getAttendanceOverallStatus(),
            color:
                _getAttendanceOverallColor(),
          ),
        ],
      ),
    );
  }

  Widget _buildCurrentStatusCard() {
    final AttendanceState state =
        widget.attendanceState;

    String text;
    IconData icon;
    Color color;
    Color background;

    if (!state.hasShift) {
      text = 'Hôm nay chưa được xếp ca';
      icon = Icons.event_busy;
      color = Colors.grey;
      background = Colors.grey.shade100;
    } else if (state.status == 'VangMat') {
      text = 'Vắng mặt';
      icon = Icons.person_off_outlined;
      color = Colors.red;
      background = Colors.red.shade50;
    } else if (state.checkedOut) {
      text = _getAttendanceOverallStatus();
      icon = state.lateMinutes > 0 ||
              state.earlyLeaveMinutes > 0
          ? Icons.warning_amber_rounded
          : Icons.check_circle;
      color = _getAttendanceOverallColor();
      background = color.withValues(
        alpha: 0.08,
      );
    } else if (state.checkedIn) {
      text = _getAttendanceOverallStatus();
      icon = state.lateMinutes > 0
          ? Icons.warning_amber_rounded
          : Icons.check_circle_outline;
      color = _getAttendanceOverallColor();
      background = color.withValues(
        alpha: 0.08,
      );
    } else if (_isShiftEndedBeforeCheckIn) {
      text =
          'Ca đã kết thúc • Chưa điểm danh';
      icon = Icons.event_busy_outlined;
      color = Colors.red;
      background = Colors.red.shade50;
    } else if (_isTooEarlyToCheckIn) {
      text =
          'Có thể điểm danh từ '
          '${_formatTime(_checkInAllowedFrom)}';
      icon = Icons.schedule_outlined;
      color = AppTheme.orangeColor;
      background =
          const Color(0xFFFFF1E8);
    } else {
      text = 'Chưa điểm danh';
      icon = Icons.schedule_outlined;
      color = AppTheme.orangeColor;
      background =
          const Color(0xFFFFF1E8);
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: background,
        borderRadius:
            BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(icon, color: color),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: TextStyle(
                fontWeight:
                    FontWeight.w600,
                color: color,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLocationCard() {
    final LocationCheckResult? result =
        _locationResult;

    String statusText;
    Color statusColor;
    IconData statusIcon;
    String detailText;

    if (_isCheckingLocation) {
      statusText = 'Đang kiểm tra';
      statusColor = Colors.blueGrey;
      statusIcon = Icons.sync;
      detailText =
          'Đang lấy vị trí GPS hiện tại...';
    } else if (result == null) {
      statusText = 'Chưa kiểm tra';
      statusColor = Colors.grey;
      statusIcon = Icons.location_searching;
      detailText =
          'Bấm Kiểm tra vị trí để xác nhận phạm vi điểm danh.';
    } else if (!result.isConfigured &&
        result.permissionGranted &&
        result.latitude != null &&
        result.longitude != null) {
      statusText = 'GPS sẵn sàng';
      statusColor = Colors.green;
      statusIcon = Icons.my_location;
      detailText =
          'Đã lấy vị trí GPS hiện tại'
          ' • Sai số: '
          '${result.accuracyMeters?.round() ?? 0} m. '
          'Máy chủ sẽ kiểm tra phạm vi nhà hàng khi điểm danh.';
    } else if (!result.isConfigured) {
      statusText = 'Chưa cấu hình';
      statusColor = Colors.orange;
      statusIcon = Icons.settings_outlined;
      detailText =
          'Chưa lấy được vị trí GPS hiện tại.';
    } else if (!result.serviceEnabled) {
      statusText = 'GPS đang tắt';
      statusColor = Colors.red;
      statusIcon = Icons.location_disabled;
      detailText =
          'Hãy bật Dịch vụ vị trí trên thiết bị.';
    } else if (!result.permissionGranted) {
      statusText = 'Chưa cấp quyền';
      statusColor = Colors.red;
      statusIcon = Icons.location_off_outlined;
      detailText = result.message;
    } else if (result.isInside) {
      statusText = 'Trong phạm vi';
      statusColor = Colors.green;
      statusIcon = Icons.check_circle;
      detailText =
          'Khoảng cách: '
          '${result.distanceMeters?.round() ?? 0} m'
          ' • Sai số GPS: '
          '${result.accuracyMeters?.round() ?? 0} m'
          ' • Bán kính cho phép: '
          '${LocationService.geofenceRadiusMeters.round()} m.';
    } else {
      statusText = 'Ngoài phạm vi';
      statusColor = Colors.red;
      statusIcon = Icons.cancel_outlined;
      detailText =
          'Khoảng cách: '
          '${result.distanceMeters?.round() ?? 0} m'
          ' • Bán kính cho phép: '
          '${LocationService.geofenceRadiusMeters.round()} m.';
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius:
            BorderRadius.circular(14),
        border: Border.all(
          color: Colors.grey.shade200,
        ),
      ),
      child: Column(
        crossAxisAlignment:
            CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Vị trí điểm danh',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight:
                        FontWeight.w600,
                  ),
                ),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(
                  horizontal: 9,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: statusColor
                      .withValues(
                    alpha: 0.10,
                  ),
                  borderRadius:
                      BorderRadius.circular(14),
                ),
                child: Row(
                  mainAxisSize:
                      MainAxisSize.min,
                  children: [
                    Icon(
                      statusIcon,
                      size: 16,
                      color: statusColor,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      statusText,
                      style: TextStyle(
                        color: statusColor,
                        fontSize: 12,
                        fontWeight:
                            FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(height: 14),

          Row(
            crossAxisAlignment:
                CrossAxisAlignment.start,
            children: [
              Container(
                width: 44,
                height: 44,
                decoration:
                    const BoxDecoration(
                  color: Color(0xFFFFE9DE),
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.location_on,
                  color:
                      AppTheme.orangeColor,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  detailText,
                  style: TextStyle(
                    fontSize: 13,
                    height: 1.4,
                    color:
                        Colors.grey.shade700,
                  ),
                ),
              ),
            ],
          ),

          if (result?.isMocked == true &&
              kDebugMode) ...[
            const SizedBox(height: 8),
            Text(
              'Thiết bị đang dùng vị trí mô phỏng (được phép khi debug).',
              style: TextStyle(
                fontSize: 11,
                color:
                    Colors.orange.shade700,
              ),
            ),
          ],

          const SizedBox(height: 12),

          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: _isCheckingLocation
                  ? null
                  : () {
                      _refreshLocation(
                        showMessage: true,
                        requestPermission: true,
                      );
                    },
              icon: const Icon(
                Icons.my_location,
                size: 18,
              ),
              label: const Text(
                'Kiểm tra vị trí',
              ),
              style: OutlinedButton.styleFrom(
                foregroundColor:
                    AppTheme.primaryColor,
                side: const BorderSide(
                  color:
                      AppTheme.primaryColor,
                ),
              ),
            ),
          ),

          if (kDebugMode &&
              (result == null ||
                  !result.isConfigured)) ...[
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: TextButton.icon(
                onPressed: _isCheckingLocation
                    ? null
                    : _calibrateDebugLocation,
                icon: const Icon(
                  Icons.developer_mode,
                  size: 18,
                ),
                label: const Text(
                  'Dùng vị trí hiện tại làm mốc nhà hàng (DEBUG)',
                ),
              ),
            ),
          ],

          if (result != null &&
              result.isConfigured &&
              !result.serviceEnabled) ...[
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: TextButton(
                onPressed: () {
                  AppServices.locationService
                      .openLocationSettings();
                },
                child: const Text(
                  'Mở cài đặt vị trí',
                ),
              ),
            ),
          ],

          if (result != null &&
              result.isConfigured &&
              result.serviceEnabled &&
              !result.permissionGranted) ...[
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: TextButton(
                onPressed: () {
                  AppServices.locationService
                      .openAppSettings();
                },
                child: const Text(
                  'Mở cài đặt ứng dụng',
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildStatusRow({
    required IconData icon,
    required String title,
    required String value,
    required Color color,
  }) {
    return Row(
      children: [
        Icon(
          icon,
          size: 21,
          color: color,
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            title,
            style: TextStyle(
              color: Colors.grey.shade700,
              fontSize: 14,
            ),
          ),
        ),
        Container(
          padding:
              const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 5,
          ),
          decoration: BoxDecoration(
            color: color.withValues(
              alpha: 0.10,
            ),
            borderRadius:
                BorderRadius.circular(12),
          ),
          child: Text(
            value,
            style: TextStyle(
              color: color,
              fontSize: 12,
              fontWeight:
                  FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildInfoRow({
    required IconData icon,
    required String title,
    required String value,
  }) {
    return Row(
      children: [
        Icon(
          icon,
          size: 21,
          color:
              AppTheme.orangeColor,
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            title,
            style: TextStyle(
              color: Colors.grey.shade700,
              fontSize: 14,
            ),
          ),
        ),
        Text(
          value,
          style: const TextStyle(
            fontSize: 14,
            fontWeight:
                FontWeight.w600,
          ),
        ),
      ],
    );
  }
}
