import 'employee_profile.dart';

class ShiftRecord {
  final DateTime date;
  final DateTime checkInTime;
  final DateTime checkOutTime;
  final String shiftName;
  final String startTime;
  final String endTime;

  const ShiftRecord({
    required this.date,
    required this.checkInTime,
    required this.checkOutTime,
    this.shiftName = 'Ca làm',
    this.startTime = '08:00',
    this.endTime = '17:00',
  });

  factory ShiftRecord.fromJson(
    Map<String, dynamic> json,
  ) {
    final DateTime checkIn =
        DateTime.parse(
      json['checkInTime'].toString(),
    );

    final DateTime checkOut =
        DateTime.parse(
      json['checkOutTime'].toString(),
    );

    final String workDate =
        json['workDate']?.toString() ?? '';

    final DateTime date =
        DateTime.tryParse(workDate) ??
            DateTime(
              checkIn.year,
              checkIn.month,
              checkIn.day,
            );

    return ShiftRecord(
      date: date,
      checkInTime: checkIn,
      checkOutTime: checkOut,
      shiftName:
          json['shiftName']?.toString() ??
              'Ca làm',
      startTime:
          json['startTime']?.toString() ??
              '08:00',
      endTime:
          json['endTime']?.toString() ??
              '17:00',
    );
  }
}

class AttendanceState {
  bool checkedIn;
  bool checkedOut;

  DateTime? checkInTime;
  DateTime? checkOutTime;
  DateTime? attendanceDate;

  bool hasShift;
  int? shiftAssignmentId;
  String shiftName;
  String shiftStartTime;
  String shiftEndTime;
  String status;
  String areaName;

  final List<ShiftRecord> history;

  AttendanceState({
    this.checkedIn = false,
    this.checkedOut = false,
    this.checkInTime,
    this.checkOutTime,
    this.attendanceDate,
    this.hasShift = false,
    this.shiftAssignmentId,
    this.shiftName = 'Chưa xếp ca',
    this.shiftStartTime = '--:--',
    this.shiftEndTime = '--:--',
    this.status = 'ChuaXepCa',
    this.areaName = '',
    List<ShiftRecord>? history,
  }) : history = history ?? [];

  bool _isSameDay(
    DateTime first,
    DateTime second,
  ) {
    return first.year == second.year &&
        first.month == second.month &&
        first.day == second.day;
  }

  void ensureCurrentDay() {
    final DateTime now = DateTime.now();

    if (attendanceDate == null) {
      return;
    }

    if (!_isSameDay(
      attendanceDate!,
      now,
    )) {
      checkedIn = false;
      checkedOut = false;
      checkInTime = null;
      checkOutTime = null;
      attendanceDate = null;

      hasShift = false;
      shiftAssignmentId = null;
      shiftName = 'Chưa xếp ca';
      shiftStartTime = '--:--';
      shiftEndTime = '--:--';
      status = 'ChuaXepCa';
      areaName = '';
    }
  }

  void applyTodayJson(
    Map<String, dynamic> json,
  ) {
    hasShift =
        json['hasShift'] == true;

    shiftAssignmentId =
        json['shiftAssignmentId'] == null
            ? null
            : _parseInt(
                json['shiftAssignmentId'],
              );

    shiftName =
        json['shiftName']?.toString() ??
            'Chưa xếp ca';

    shiftStartTime =
        json['startTime']?.toString() ??
            '--:--';

    shiftEndTime =
        json['endTime']?.toString() ??
            '--:--';

    status =
        json['status']?.toString() ??
            'ChuaXepCa';

    areaName =
        json['areaName']?.toString() ??
            '';

    checkInTime =
        _parseDateTime(
      json['checkInTime'],
    );

    checkOutTime =
        _parseDateTime(
      json['checkOutTime'],
    );

    final String? workDate =
        json['workDate']?.toString();

    attendanceDate =
        workDate == null
            ? (checkInTime != null
                ? DateTime(
                    checkInTime!.year,
                    checkInTime!.month,
                    checkInTime!.day,
                  )
                : null)
            : DateTime.tryParse(workDate);

    checkedIn =
        checkInTime != null;

    checkedOut =
        checkOutTime != null ||
        status == 'DaHoanThanh';
  }

  void replaceHistory(
    List<ShiftRecord> records,
  ) {
    history
      ..clear()
      ..addAll(records);
  }

  static int _parseInt(dynamic value) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }

  static DateTime? _parseDateTime(
    dynamic value,
  ) {
    if (value == null) {
      return null;
    }

    return DateTime.tryParse(
      value.toString(),
    );
  }
}

final Map<String, AttendanceState>
    _attendanceStates = {};

AttendanceState get appAttendanceState {
  final AttendanceState state =
      _attendanceStates.putIfAbsent(
    appEmployee.employeeCode,
    () => AttendanceState(),
  );

  state.ensureCurrentDay();

  return state;
}
