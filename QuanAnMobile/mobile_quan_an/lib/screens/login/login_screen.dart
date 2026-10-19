import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/employee_profile.dart';
import '../tables/table_screen.dart';
import '../kitchen/kitchen_screen.dart';
import '../../widgets/top_message.dart';
import '../../services/auth_service.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/mobile_access_policy.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() =>
      _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {

  final AuthService _authService =
    AppServices.authService;

  final TextEditingController employeeController =
      TextEditingController();

  final TextEditingController passwordController =
      TextEditingController();

  bool obscurePassword = true;
  bool _isLoggingIn = false;

  @override
  void dispose() {
    employeeController.dispose();
    passwordController.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    final String login =
        employeeController.text.trim();

    final String password =
        passwordController.text;

    if (login.isEmpty || password.isEmpty) {
      TopMessage.show(
        context,
        message: 'Vui lòng nhập đầy đủ thông tin',
        type: TopMessageType.warning,
      );
      return;
    }

    if (_isLoggingIn) return;

    setState(() {
      _isLoggingIn = true;
    });

    try {
      final EmployeeProfile? employee =
          await _authService.login(
        login: login,
        password: password,
      );

      if (!mounted) return;

      if (employee == null) {
        TopMessage.show(
          context,
          message: 'Tài khoản hoặc mật khẩu không đúng',
          type: TopMessageType.error,
        );
        return;
      }

      if (!MobileAccessPolicy.canUseMobile(employee.role)) {
        TopMessage.show(
          context,
          message: 'Tài khoản chưa được cấp quyền sử dụng ứng dụng Mobile.',
          type: TopMessageType.warning,
        );
        return;
      }

      appEmployee = employee;

      final bool canOpenTables = MobileAccessPolicy.canOpenTables();
      final bool canOpenKitchen = MobileAccessPolicy.canOpenKitchen();

      // Tài khoản chế biến phải thuộc đúng một Station trong Employees.
      // Bếp món và Quầy nước dùng cùng Role Bep nhưng không dùng chung hàng đợi.
      if (!canOpenTables &&
          canOpenKitchen &&
          (employee.stationId == null || employee.stationName.trim().isEmpty)) {
        TopMessage.show(
          context,
          message:
              'Tài khoản chế biến chưa được phân công Bếp hoặc Quầy nước.',
          type: TopMessageType.warning,
        );
        return;
      }

      if (!canOpenTables && !canOpenKitchen) {
        TopMessage.show(
          context,
          message: 'Tài khoản chưa được cấp màn hình làm việc trên Mobile.',
          type: TopMessageType.warning,
        );
        return;
      }

      Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (context) => canOpenTables
              ? const TableScreen()
              : const KitchenScreen(),
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } catch (_) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Không thể đăng nhập lúc này',
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() {
          _isLoggingIn = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: Column(
              children: [
                Container(
                  width: 72,
                  height: 72,
                  decoration: BoxDecoration(
                    color: AppTheme.orangeColor,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: const Icon(
                    Icons.restaurant,
                    color: Colors.white,
                    size: 38,
                  ),
                ),
                const SizedBox(height: 20),
                const Text(
                  'Đăng nhập hệ thống',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    color: AppTheme.textColor,
                  ),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Dành cho nhân viên quán',
                  style: TextStyle(
                    fontSize: 14,
                    color: Colors.grey,
                  ),
                ),
                const SizedBox(height: 32),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'Tài khoản',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: Colors.grey.shade800,
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: employeeController,
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(
                    hintText: 'Nhập tài khoản',
                    prefixIcon: Icon(Icons.badge_outlined),
                  ),
                ),
                const SizedBox(height: 18),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'Mật khẩu',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: Colors.grey.shade800,
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: passwordController,
                  obscureText: obscurePassword,
                  textInputAction: TextInputAction.done,
                  onSubmitted: (_) => _login(),
                  decoration: InputDecoration(
                    hintText: 'Nhập mật khẩu',
                    prefixIcon: const Icon(Icons.lock_outline),
                    suffixIcon: IconButton(
                      onPressed: () {
                        setState(() {
                          obscurePassword = !obscurePassword;
                        });
                      },
                      icon: Icon(
                        obscurePassword
                            ? Icons.visibility_off_outlined
                            : Icons.visibility_outlined,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton(
                    onPressed: () {
                      TopMessage.show(
                        context,
                        message: 'Vui lòng liên hệ quản lý để cấp lại mật khẩu',
                        type: TopMessageType.info,
                      );
                    },
                    child: const Text('Quên mật khẩu?'),
                  ),
                ),
                const SizedBox(height: 10),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: ElevatedButton(
                    onPressed: _isLoggingIn ? null : _login,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.primaryColor,
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                    child: _isLoggingIn
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.4,
                              color: Colors.white,
                            ),
                          )
                        : const Text(
                            'Đăng nhập →',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                  ),
                ),
                const SizedBox(height: 22),
                const Text(
                  'Cần hỗ trợ? Liên hệ quản lý',
                  style: TextStyle(
                    fontSize: 13,
                    color: Colors.grey,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
