import 'api_client.dart';
import 'auth_service.dart';
import 'table_service.dart';
import 'menu_service.dart';
import 'notification_service.dart';
import 'order_service.dart';
import 'payment_service.dart';
import 'realtime_service.dart';
import 'attendance_service.dart';
import 'profile_service.dart';
import 'notification_settings_service.dart';
import 'location_service.dart';
import 'notification_feedback_service.dart';
import 'area_assignment_service.dart';
import 'reference_ops_service.dart';
import 'table_lock_service.dart';
import 'customer_service.dart';
import 'statistics_service.dart';
import 'staff_management_service.dart';

class AppServices {
  AppServices._();

  static final ApiClient apiClient =
      ApiClient();

  // Auth da chuyen sang API that.
  static final AuthService authService =
      AuthService(
    apiClient: apiClient,
  );

  // Ban da chuyen sang API that.
  static final TableService tableService =
      TableService(
    apiClient: apiClient,
  );

  static final AreaAssignmentService areaAssignmentService =
      AreaAssignmentService(
    apiClient: apiClient,
  );

  static final ReferenceOpsService referenceOpsService =
      ReferenceOpsService(
    apiClient: apiClient,
  );

  static final TableLockService tableLockService =
      TableLockService(
    apiClient: apiClient,
  );

  static final CustomerService customerService =
      CustomerService(
    apiClient: apiClient,
  );

  static final StatisticsService statisticsService =
      StatisticsService(
    apiClient: apiClient,
  );

  static final StaffManagementService staffManagementService =
      StaffManagementService(
    apiClient: apiClient,
  );

  // Menu da chuyen sang API that.
  static final MenuService menuService =
      MenuService(
    apiClient: apiClient,
  );

  // Order va trang thai mon dung API that.
  static final OrderService orderService =
      OrderService(
    apiClient: apiClient,
  );

  // Thanh toan va hoa don dung API that.
  static final PaymentService paymentService =
      PaymentService(
    apiClient: apiClient,
  );

  static final NotificationService
      notificationService =
      NotificationService(
    apiClient: apiClient,
  );

  static final RealtimeService
      realtimeService =
      RealtimeService();

  static final AttendanceService
      attendanceService =
      AttendanceService(
    apiClient: apiClient,
  );

  static final ProfileService
      profileService =
      ProfileService(
    apiClient: apiClient,
  );

  static const NotificationSettingsService
      notificationSettingsService =
      NotificationSettingsService();

  static const LocationService
      locationService =
      LocationService();

  static const NotificationFeedbackService
      notificationFeedbackService =
      NotificationFeedbackService();
}
