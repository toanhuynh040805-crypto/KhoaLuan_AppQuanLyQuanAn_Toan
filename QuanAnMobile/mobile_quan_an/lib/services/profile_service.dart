import '../models/employee_profile.dart';
import 'api_client.dart';

class ProfileService {
  final ApiClient _apiClient;

  const ProfileService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<EmployeeProfile> getProfile({
    required String employeeCode,
  }) async {
    final dynamic response =
        await _apiClient.get(
      '/profile/$employeeCode',
    );

    if (response is! Map) {
      throw const ApiException(
        message:
            'Không thể tải thông tin nhân viên',
      );
    }

    return EmployeeProfile.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
}
