import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/employee_profile.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../widgets/top_message.dart';

class PersonalInfoScreen extends StatefulWidget {
  const PersonalInfoScreen({super.key});

  @override
  State<PersonalInfoScreen> createState() =>
      _PersonalInfoScreenState();
}

class _PersonalInfoScreenState
    extends State<PersonalInfoScreen> {
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    try {
      final EmployeeProfile profile =
          await AppServices.profileService
              .getProfile(
        employeeCode:
            appEmployee.employeeCode,
      );

      appEmployee = profile;

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

  String _formatHireDate(
    String value,
  ) {
    final DateTime? date =
        DateTime.tryParse(value);

    if (date == null) {
      return value.isEmpty
          ? 'Chưa có dữ liệu'
          : value;
    }

    final String day =
        date.day.toString().padLeft(2, '0');
    final String month =
        date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  String _displayStatus(
    String value,
  ) {
    switch (value) {
      case 'DangLamViec':
        return 'Đang làm việc';
      case 'TamNghi':
        return 'Tạm nghỉ';
      case 'NghiViec':
        return 'Đã nghỉ việc';
      default:
        return value.isEmpty
            ? 'Chưa có dữ liệu'
            : value;
    }
  }

  @override
  Widget build(BuildContext context) {
    final EmployeeProfile employee =
        appEmployee;

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
          'Thông tin cá nhân',
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
              onRefresh: _loadProfile,
              child: SingleChildScrollView(
                physics:
                    const AlwaysScrollableScrollPhysics(),
                padding:
                    const EdgeInsets.all(16),
                child: Column(
                  children: [
                    Container(
                      width: double.infinity,
                      padding:
                          const EdgeInsets.all(20),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius:
                            BorderRadius.circular(
                          14,
                        ),
                        border: Border.all(
                          color:
                              Colors.grey.shade200,
                        ),
                      ),
                      child: Column(
                        children: [
                          const CircleAvatar(
                            radius: 42,
                            backgroundColor:
                                Color(0xFFFFE9DE),
                            child: Icon(
                              Icons.person,
                              size: 52,
                              color:
                                  AppTheme.orangeColor,
                            ),
                          ),

                          const SizedBox(
                            height: 12,
                          ),

                          Text(
                            employee.fullName,
                            style:
                                const TextStyle(
                              fontSize: 20,
                              fontWeight:
                                  FontWeight.bold,
                            ),
                          ),

                          const SizedBox(
                            height: 5,
                          ),

                          Text(
                            employee.role,
                            style:
                                const TextStyle(
                              fontSize: 13,
                              color: Colors.grey,
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 16),

                    Container(
                      padding:
                          const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius:
                            BorderRadius.circular(
                          14,
                        ),
                        border: Border.all(
                          color:
                              Colors.grey.shade200,
                        ),
                      ),
                      child: Column(
                        children: [
                          _buildInfoRow(
                            icon:
                                Icons.badge_outlined,
                            title:
                                'Mã nhân viên',
                            value: employee
                                .employeeCode,
                          ),

                          if (employee
                              .username
                              .isNotEmpty) ...[
                            const Divider(
                              height: 28,
                            ),
                            _buildInfoRow(
                              icon: Icons
                                  .account_circle_outlined,
                              title:
                                  'Tài khoản',
                              value: employee
                                  .username,
                            ),
                          ],

                          const Divider(
                            height: 28,
                          ),

                          _buildInfoRow(
                            icon:
                                Icons.work_outline,
                            title: 'Chức vụ',
                            value: employee.role,
                          ),

                          const Divider(
                            height: 28,
                          ),

                          _buildInfoRow(
                            icon: Icons
                                .calendar_month_outlined,
                            title:
                                'Ngày vào làm',
                            value:
                                _formatHireDate(
                              employee.hireDate,
                            ),
                          ),

                          const Divider(
                            height: 28,
                          ),

                          _buildInfoRow(
                            icon: Icons
                                .verified_user_outlined,
                            title:
                                'Trạng thái',
                            value:
                                _displayStatus(
                              employee
                                  .employeeStatus,
                            ),
                          ),

                          if (employee
                              .area
                              .trim()
                              .isNotEmpty) ...[
                            const Divider(
                              height: 28,
                            ),
                            _buildInfoRow(
                              icon: Icons
                                  .location_on_outlined,
                              title: 'Khu vực',
                              value:
                                  employee.area,
                            ),
                          ],

                          const Divider(
                            height: 28,
                          ),

                          _buildInfoRow(
                            icon:
                                Icons.phone_outlined,
                            title:
                                'Số điện thoại',
                            value: employee.phone
                                    .isEmpty
                                ? 'Chưa cập nhật'
                                : employee.phone,
                          ),

                          const Divider(
                            height: 28,
                          ),

                          _buildInfoRow(
                            icon:
                                Icons.email_outlined,
                            title: 'Email',
                            value: employee.email
                                    .isEmpty
                                ? 'Chưa cập nhật'
                                : employee.email,
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 12),

                    Text(
                      'Thông tin được đồng bộ trực tiếp từ tài khoản nhân viên trong hệ thống.',
                      style: TextStyle(
                        fontSize: 12,
                        color:
                            Colors.grey.shade600,
                      ),
                      textAlign:
                          TextAlign.center,
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  static Widget _buildInfoRow({
    required IconData icon,
    required String title,
    required String value,
  }) {
    return Row(
      children: [
        Icon(
          icon,
          size: 21,
          color: AppTheme.orangeColor,
        ),

        const SizedBox(width: 12),

        Expanded(
          child: Text(
            title,
            style: TextStyle(
              fontSize: 14,
              color:
                  Colors.grey.shade700,
            ),
          ),
        ),

        Flexible(
          child: Text(
            value,
            textAlign:
                TextAlign.right,
            style: const TextStyle(
              fontSize: 14,
              fontWeight:
                  FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }
}
