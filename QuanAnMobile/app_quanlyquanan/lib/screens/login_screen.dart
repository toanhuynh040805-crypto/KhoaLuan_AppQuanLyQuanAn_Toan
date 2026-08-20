import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../services/api_service.dart';
import 'main_shell.dart';

// Khop voi thiet ke "dang_nhap_nang_cap" tu Stitch: icon dao-thia gradient cam,
// card trang bo goc giua nen xam nhat, nut "Vao ca" gradient.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final _apiService = ApiService();

  bool _obscurePassword = true;
  bool _isLoading = false;
  String? _errorText;

  Future<void> _handleLogin() async {
    if (_usernameController.text.trim().isEmpty || _passwordController.text.isEmpty) {
      setState(() => _errorText = 'Vui lòng nhập đủ mã nhân viên và mật khẩu');
      return;
    }

    setState(() {
      _isLoading = true;
      _errorText = null;
    });

    try {
      final user = await _apiService.login(_usernameController.text.trim(), _passwordController.text);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => MainShell(user: user)),
      );
    } catch (e) {
      setState(() => _errorText = 'Sai mã nhân viên hoặc mật khẩu');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: Container(
              padding: const EdgeInsets.all(24),
              decoration: BoxDecoration(
                color: AppColors.surfaceContainerLowest,
                borderRadius: BorderRadius.circular(20),
                boxShadow: [
                  BoxShadow(color: Colors.black.withOpacity(0.06), blurRadius: 24, offset: const Offset(0, 8)),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  // Icon logo gradient cam
                  Container(
                    width: 88,
                    height: 88,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(20),
                      gradient: const LinearGradient(
                        colors: [AppColors.primary, AppColors.primaryContainer],
                        begin: Alignment.topLeft,
                        end: Alignment.bottomRight,
                      ),
                      boxShadow: [
                        BoxShadow(color: AppColors.primaryContainer.withOpacity(0.4), blurRadius: 16, offset: const Offset(0, 6)),
                      ],
                    ),
                    child: const Icon(Icons.restaurant, color: Colors.white, size: 40),
                  ),
                  const SizedBox(height: 20),
                  Text('Đăng nhập Hệ thống',
                      style: Theme.of(context).textTheme.headlineLarge, textAlign: TextAlign.center),
                  const SizedBox(height: 4),
                  Text('Dành cho nhân viên phục vụ',
                      style: Theme.of(context).textTheme.bodyMedium, textAlign: TextAlign.center),
                  const SizedBox(height: 24),

                  // O Ma nhan vien
                  _buildLabel('Mã nhân viên'),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _usernameController,
                    decoration: const InputDecoration(
                      hintText: 'VD: NV001',
                      prefixIcon: Icon(Icons.badge_outlined, color: AppColors.onSurfaceVariant),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // O Mat khau + label + "Quen mat khau"
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      _buildLabel('Mật khẩu'),
                      GestureDetector(
                        // TODO: noi man hinh "Quen mat khau" that khi co endpoint tuong ung
                        onTap: () {},
                        child: const Text('Quên mật khẩu?',
                            style: TextStyle(color: AppColors.primary, fontSize: 13, fontWeight: FontWeight.w600)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _passwordController,
                    obscureText: _obscurePassword,
                    decoration: InputDecoration(
                      hintText: '••••••••',
                      prefixIcon: const Icon(Icons.lock_outline, color: AppColors.onSurfaceVariant),
                      suffixIcon: IconButton(
                        icon: Icon(_obscurePassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                            color: AppColors.onSurfaceVariant),
                        onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                  ),

                  if (_errorText != null) ...[
                    const SizedBox(height: 10),
                    Text(_errorText!, style: const TextStyle(color: AppColors.error, fontSize: 13)),
                  ],

                  const SizedBox(height: 22),

                  // Nut Vao ca - gradient cam
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        gradient: const LinearGradient(
                          colors: [AppColors.primary, AppColors.primaryContainer],
                          begin: Alignment.centerLeft,
                          end: Alignment.centerRight,
                        ),
                      ),
                      child: Material(
                        color: Colors.transparent,
                        child: InkWell(
                          borderRadius: BorderRadius.circular(12),
                          onTap: _isLoading ? null : _handleLogin,
                          child: Center(
                            child: _isLoading
                                ? const SizedBox(
                                    width: 22, height: 22,
                                    child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2.5))
                                : const Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Text('Vào ca', style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.w700)),
                                      SizedBox(width: 8),
                                      Icon(Icons.arrow_forward, color: Colors.white, size: 18),
                                    ],
                                  ),
                          ),
                        ),
                      ),
                    ),
                  ),

                  const SizedBox(height: 18),
                  const Divider(),
                  const SizedBox(height: 10),
                  RichText(
                    text: const TextSpan(
                      style: TextStyle(fontSize: 13, color: AppColors.onSurfaceVariant),
                      children: [
                        TextSpan(text: 'Cần hỗ trợ? '),
                        TextSpan(text: 'Liên hệ Quản lý', style: TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLabel(String text) => Text(text, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600));
}
