class ManagedEmployee {
  final int id;
  final int userId;
  final String fullName;
  final String employeeCode;
  final String roleName;
  final bool isActive;

  const ManagedEmployee({
    required this.id,
    required this.userId,
    required this.fullName,
    required this.employeeCode,
    required this.roleName,
    required this.isActive,
  });

  factory ManagedEmployee.fromJson(Map<String, dynamic> json) {
    return ManagedEmployee(
      id: _asInt(json['id'] ?? json['Id']),
      userId: _asInt(json['userId'] ?? json['UserId']),
      fullName: (json['fullName'] ?? json['FullName'] ?? '').toString(),
      employeeCode: (json['employeeCode'] ?? json['EmployeeCode'] ?? '').toString(),
      roleName: (json['roleName'] ?? json['RoleName'] ?? '').toString(),
      isActive: json['isActive'] == true || json['IsActive'] == true,
    );
  }

  @override
  String toString() => fullName;
}

class ManagedShift {
  final int id;
  final String name;
  final String startTime;
  final String endTime;

  const ManagedShift({
    required this.id,
    required this.name,
    required this.startTime,
    required this.endTime,
  });

  factory ManagedShift.fromJson(Map<String, dynamic> json) {
    return ManagedShift(
      id: _asInt(json['id'] ?? json['Id']),
      name: (json['name'] ?? json['Name'] ?? '').toString(),
      startTime: (json['startTime'] ?? json['StartTime'] ?? '').toString(),
      endTime: (json['endTime'] ?? json['EndTime'] ?? '').toString(),
    );
  }

  String get timeLabel => '${_shortTime(startTime)} - ${_shortTime(endTime)}';

  @override
  String toString() => '$name ($timeLabel)';
}

class ManagedArea {
  final int id;
  final String name;

  const ManagedArea({required this.id, required this.name});

  factory ManagedArea.fromJson(Map<String, dynamic> json) {
    return ManagedArea(
      id: _asInt(json['id'] ?? json['Id']),
      name: (json['name'] ?? json['Name'] ?? '').toString(),
    );
  }

  @override
  String toString() => name;
}

class ManagedShiftAssignment {
  final int id;
  final int userId;
  final String userName;
  final int shiftId;
  final String shiftName;
  final String shiftStart;
  final String shiftEnd;
  final DateTime workDate;
  final int? areaId;
  final String? areaName;
  final int status;
  final DateTime? checkInTime;
  final DateTime? checkOutTime;

  const ManagedShiftAssignment({
    required this.id,
    required this.userId,
    required this.userName,
    required this.shiftId,
    required this.shiftName,
    required this.shiftStart,
    required this.shiftEnd,
    required this.workDate,
    required this.areaId,
    required this.areaName,
    required this.status,
    required this.checkInTime,
    required this.checkOutTime,
  });

  factory ManagedShiftAssignment.fromJson(Map<String, dynamic> json) {
    return ManagedShiftAssignment(
      id: _asInt(json['id'] ?? json['Id']),
      userId: _asInt(json['userId'] ?? json['UserId']),
      userName: (json['userName'] ?? json['UserName'] ?? '').toString(),
      shiftId: _asInt(json['shiftId'] ?? json['ShiftId']),
      shiftName: (json['shiftName'] ?? json['ShiftName'] ?? '').toString(),
      shiftStart: (json['shiftStart'] ?? json['ShiftStart'] ?? '').toString(),
      shiftEnd: (json['shiftEnd'] ?? json['ShiftEnd'] ?? '').toString(),
      workDate: _asDate(json['workDate'] ?? json['WorkDate']) ?? DateTime.now(),
      areaId: _asNullableInt(json['areaId'] ?? json['AreaId']),
      areaName: (json['areaName'] ?? json['AreaName'])?.toString(),
      status: _asInt(json['status'] ?? json['Status']),
      checkInTime: _asDate(json['checkInTime'] ?? json['CheckInTime']),
      checkOutTime: _asDate(json['checkOutTime'] ?? json['CheckOutTime']),
    );
  }

  String get shiftTimeLabel => '${_shortTime(shiftStart)} - ${_shortTime(shiftEnd)}';

  String get statusLabel => switch (status) {
    1 => 'Đã điểm danh',
    2 => 'Vắng mặt',
    3 => 'Đã hoàn thành',
    _ => 'Đã xếp lịch',
  };
}

class ManagedStaffTask {
  final int id;
  final String title;
  final String? description;
  final int assignedToUserId;
  final String assignedToName;
  final DateTime createdAt;
  final DateTime? dueAt;
  final int status;

  const ManagedStaffTask({
    required this.id,
    required this.title,
    required this.description,
    required this.assignedToUserId,
    required this.assignedToName,
    required this.createdAt,
    required this.dueAt,
    required this.status,
  });

  factory ManagedStaffTask.fromJson(Map<String, dynamic> json) {
    return ManagedStaffTask(
      id: _asInt(json['id'] ?? json['Id']),
      title: (json['title'] ?? json['Title'] ?? '').toString(),
      description: (json['description'] ?? json['Description'])?.toString(),
      assignedToUserId: _asInt(json['assignedToUserId'] ?? json['AssignedToUserId']),
      assignedToName: (json['assignedToName'] ?? json['AssignedToName'] ?? '').toString(),
      createdAt: _asDate(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
      dueAt: _asDate(json['dueAt'] ?? json['DueAt']),
      status: _asInt(json['status'] ?? json['Status']),
    );
  }

  String get statusLabel => switch (status) {
    1 => 'Đang làm',
    2 => 'Hoàn thành',
    _ => 'Chưa làm',
  };
}

int _asInt(dynamic value) {
  if (value is int) return value;
  if (value is num) return value.toInt();
  return int.tryParse(value?.toString() ?? '') ?? 0;
}

int? _asNullableInt(dynamic value) {
  if (value == null) return null;
  final int parsed = _asInt(value);
  return parsed == 0 ? null : parsed;
}

DateTime? _asDate(dynamic value) {
  if (value is DateTime) return value;
  final String text = value?.toString() ?? '';
  if (text.isEmpty) return null;
  return DateTime.tryParse(text);
}

String _shortTime(String value) {
  final List<String> parts = value.split(':');
  if (parts.length >= 2) {
    return '${parts[0].padLeft(2, '0')}:${parts[1].padLeft(2, '0')}';
  }
  return value;
}
