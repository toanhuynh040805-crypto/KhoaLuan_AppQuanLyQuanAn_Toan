class EmployeeProfile {
  final String employeeCode;
  final int? userId;
  final String password;
  final String fullName;
  final String username;
  final String role;
  final List<String> roles;
  final Set<String> permissions;
  final String area;
  final String email;
  final String phone;
  final String hireDate;
  final String employeeStatus;
  final int? stationId;
  final String stationName;

  const EmployeeProfile({
    required this.employeeCode,
    this.userId,
    this.password = '',
    required this.fullName,
    this.username = '',
    required this.role,
    this.roles = const <String>[],
    this.permissions = const <String>{},
    this.area = '',
    this.email = '',
    this.phone = '',
    this.hireDate = '',
    this.employeeStatus = '',
    this.stationId,
    this.stationName = '',
  });

  factory EmployeeProfile.fromJson(Map<String, dynamic> json) {
    final List<String> parsedRoles = (json['roles'] is List)
        ? (json['roles'] as List)
            .map((value) => value?.toString().trim() ?? '')
            .where((value) => value.isNotEmpty)
            .toList()
        : <String>[];

    final String primaryRole =
        json['roleName']?.toString().trim() ??
            json['position']?.toString().trim() ??
            json['role']?.toString().trim() ??
            '';

    if (parsedRoles.isEmpty && primaryRole.isNotEmpty) {
      parsedRoles.add(primaryRole);
    }

    final Set<String> parsedPermissions = (json['permissions'] is List)
        ? (json['permissions'] as List)
            .map((value) => value?.toString().trim() ?? '')
            .where((value) => value.isNotEmpty)
            .toSet()
        : <String>{};

    return EmployeeProfile(
      employeeCode:
          json['employeeCode']?.toString() ??
              json['username']?.toString() ??
              '',
      userId: int.tryParse(json['userId']?.toString() ?? ''),
      password: '',
      fullName: json['fullName']?.toString() ?? '',
      username: json['username']?.toString() ?? '',
      role: primaryRole,
      roles: parsedRoles,
      permissions: parsedPermissions,
      area: json['area']?.toString() ?? '',
      email: json['email']?.toString() ?? '',
      phone: json['phone']?.toString() ?? '',
      hireDate: json['hireDate']?.toString() ?? '',
      employeeStatus:
          json['employeeStatus']?.toString() ??
              json['status']?.toString() ??
              '',
      stationId: int.tryParse(json['stationId']?.toString() ?? ''),
      stationName: json['stationName']?.toString() ?? '',
    );
  }

  EmployeeProfile copyWith({
    String? employeeCode,
    int? userId,
    String? password,
    String? fullName,
    String? username,
    String? role,
    List<String>? roles,
    Set<String>? permissions,
    String? area,
    String? email,
    String? phone,
    String? hireDate,
    String? employeeStatus,
    int? stationId,
    String? stationName,
  }) {
    return EmployeeProfile(
      employeeCode: employeeCode ?? this.employeeCode,
      userId: userId ?? this.userId,
      password: password ?? this.password,
      fullName: fullName ?? this.fullName,
      username: username ?? this.username,
      role: role ?? this.role,
      roles: roles ?? this.roles,
      permissions: permissions ?? this.permissions,
      area: area ?? this.area,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      hireDate: hireDate ?? this.hireDate,
      employeeStatus: employeeStatus ?? this.employeeStatus,
      stationId: stationId ?? this.stationId,
      stationName: stationName ?? this.stationName,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'employeeCode': employeeCode,
      'userId': userId,
      'fullName': fullName,
      'username': username,
      'role': role,
      'roles': roles,
      'permissions': permissions.toList(),
      'area': area,
      'email': email,
      'phone': phone,
      'hireDate': hireDate,
      'employeeStatus': employeeStatus,
      'stationId': stationId,
      'stationName': stationName,
    };
  }
}

EmployeeProfile appEmployee = const EmployeeProfile(
  employeeCode: '',
  fullName: '',
  role: '',
);
