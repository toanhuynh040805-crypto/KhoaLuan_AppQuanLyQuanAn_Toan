import '../models/employee_profile.dart';
import 'api_client.dart';
import 'mobile_access_policy.dart';

class AuthService {
  final ApiClient _apiClient;

  const AuthService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<EmployeeProfile?> login({
    required String login,
    required String password,
  }) async {
    final dynamic response = await _apiClient.post(
      '/auth/login',
      body: {
        'username': login,
        'password': password,
      },
    );

    if (response is! Map) {
      return null;
    }

    final Map<String, dynamic> data = Map<String, dynamic>.from(response);
    final String token = data['token']?.toString().trim() ?? '';

    if (token.isEmpty) {
      throw const ApiException(
        message: 'Máy chủ không trả JWT token sau khi đăng nhập',
      );
    }

    _apiClient.setAccessToken(token);

    final EmployeeProfile profile = EmployeeProfile.fromJson(data);
    MobileAccessPolicy.configurePermissions(profile.permissions);
    return profile;
  }
}
