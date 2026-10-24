enum MobileRole {
  phucVu,
  thuNgan,
  bep,
  quanLy,
  unknown,
}

/// Quyền Mobile được Backend trả về từ cấu hình DB.
///
/// Các hàm vẫn giữ tham số role để không phá API của các màn hình cũ, nhưng
/// quyết định quyền KHÔNG còn dựa vào tên role hay switch/case hard-code.
class MobileAccessPolicy {
  MobileAccessPolicy._();

  static final Set<String> _permissions = <String>{};

  static void configurePermissions(Iterable<String> permissions) {
    _permissions
      ..clear()
      ..addAll(
        permissions
            .map((value) => value.trim().toLowerCase())
            .where((value) => value.isNotEmpty),
      );
  }

  static void clear() => _permissions.clear();

  static bool has(String permissionKey) {
    return _permissions.contains(permissionKey.trim().toLowerCase());
  }

  static Set<String> get permissions => Set<String>.unmodifiable(_permissions);

  static bool canUseMobile(String _) => has('mobile.use');
  static bool canManageTables(String _) => has('table.manage');
  static bool canOrder(String _) => has('order.create');
  static bool canCook(String _) => has('kitchen.manage');
  static bool canCancelOrderItem(String _) => has('order.cancel');
  static bool canMarkServed(String _) => has('order.serve');
  static bool canRequestPayment(String _) => has('payment.request');
  static bool canCompletePayment(String _) => has('payment.complete');
  static bool canManageCustomers(String _) => has('customer.manage');
  static bool canOpenTables() => has('screen.tables');
  static bool canOpenKitchen() => has('screen.kitchen');
  static bool canAssignAreas() => has('area.assign');
  static bool hasAllAreas() => has('area.all');
  static bool canConfigureAccess() => has('access.configure');
  static bool canViewStatistics() => has('statistics.view');
  static bool canManageStaff() => has('staff.manage');

  /// Giữ tương thích cho code cũ còn tham chiếu MobileRole, nhưng suy ra từ
  /// capability thay vì từ chuỗi tên vai trò.
  static MobileRole roleOf(String _) {
    if (canOpenKitchen() && !canOpenTables()) {
      return MobileRole.bep;
    }
    if (canConfigureAccess()) {
      return MobileRole.quanLy;
    }
    if (canCompletePayment('')) {
      return MobileRole.thuNgan;
    }
    if (canOpenTables()) {
      return MobileRole.phucVu;
    }
    return MobileRole.unknown;
  }

  /// Backend hiện vẫn cần RoleName cho một số endpoint WinForms cũ. Không tự
  /// đổi/chuẩn hóa tên vai trò ở Mobile nữa; dùng nguyên dữ liệu từ Backend.
  static String backendRoleKey(String role) => role.trim();

  static String roleSummary(String _) {
    if (_permissions.isEmpty) {
      return 'Tài khoản chưa được cấp quyền Mobile.';
    }

    final List<String> capabilities = <String>[];
    if (canManageTables('')) capabilities.add('thao tác bàn');
    if (canOrder('')) capabilities.add('gọi món');
    if (canCook('')) capabilities.add('nghiệp vụ bếp');
    if (canCompletePayment('')) capabilities.add('thanh toán');
    if (canAssignAreas()) capabilities.add('phân công khu vực');
    if (canViewStatistics()) capabilities.add('xem thống kê');
    if (canManageStaff()) capabilities.add('quản lý nhân viên');

    return capabilities.isEmpty
        ? 'Quyền của tài khoản được cấu hình từ hệ thống.'
        : 'Được phép: ${capabilities.join(', ')}.';
  }
}
