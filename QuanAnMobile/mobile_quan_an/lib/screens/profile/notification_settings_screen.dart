import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/employee_profile.dart';
import '../../models/notification_settings_state.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class NotificationSettingsScreen
    extends StatefulWidget {
  final NotificationSettingsState settings;

  const NotificationSettingsScreen({
    super.key,
    required this.settings,
  });

  @override
  State<NotificationSettingsScreen>
      createState() =>
          _NotificationSettingsScreenState();
}

class _NotificationSettingsScreenState
    extends State<NotificationSettingsScreen> {
  bool _isLoading = true;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    try {
      final NotificationSettingsState loaded =
          await AppServices
              .notificationSettingsService
              .getSettings(
        employeeCode:
            appEmployee.employeeCode,
      );

      widget.settings.copyFrom(loaded);

      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });

      TopMessage.show(
        context,
        message:
            'Không thể tải cài đặt thông báo trên thiết bị',
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _saveChange(
    void Function() applyChange,
  ) async {
    if (_isSaving) {
      return;
    }

    final NotificationSettingsState before =
        NotificationSettingsState.fromJson(
      widget.settings.toJson(),
    );

    setState(() {
      applyChange();
      _isSaving = true;
    });

    try {
      await AppServices
          .notificationSettingsService
          .updateSettings(
        employeeCode:
            appEmployee.employeeCode,
        settings: widget.settings,
      );

      if (!mounted) return;

      setState(() {
        _isSaving = false;
      });
    } catch (_) {
      widget.settings.copyFrom(before);

      if (!mounted) return;

      setState(() {
        _isSaving = false;
      });

      TopMessage.show(
        context,
        message:
            'Không thể lưu cài đặt trên thiết bị',
        type: TopMessageType.error,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
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
          'Cài đặt thông báo',
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
          : SingleChildScrollView(
              padding:
                  const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment:
                    CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Loại thông báo',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight:
                          FontWeight.bold,
                    ),
                  ),

                  const SizedBox(height: 10),

                  Container(
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
                        _buildSwitchItem(
                          icon: Icons
                              .restaurant_outlined,
                          title:
                              'Món đã xong',
                          subtitle:
                              'Nhận thông báo khi bếp hoàn thành món',
                          value: widget.settings
                              .kitchenFinished,
                          onChanged: (value) {
                            _saveChange(() {
                              widget.settings
                                      .kitchenFinished =
                                  value;
                            });
                          },
                        ),

                        _buildDivider(),

                        _buildSwitchItem(
                          icon: Icons
                              .payments_outlined,
                          title:
                              'Yêu cầu thanh toán',
                          subtitle:
                              'Thông báo liên quan đến thanh toán của bàn',
                          value: widget.settings
                              .paymentRequest,
                          onChanged: (value) {
                            _saveChange(() {
                              widget.settings
                                      .paymentRequest =
                                  value;
                            });
                          },
                        ),

                        _buildDivider(),

                        _buildSwitchItem(
                          icon: Icons
                              .table_restaurant_outlined,
                          title:
                              'Bàn và đặt bàn',
                          subtitle:
                              'Nhận thông báo liên quan đến bàn',
                          value: widget.settings
                              .tableNotification,
                          onChanged: (value) {
                            _saveChange(() {
                              widget.settings
                                      .tableNotification =
                                  value;
                            });
                          },
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 22),

                  const Text(
                    'Cách thông báo',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight:
                          FontWeight.bold,
                    ),
                  ),

                  const SizedBox(height: 10),

                  Container(
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
                        _buildSwitchItem(
                          icon:
                              Icons.volume_up_outlined,
                          title: 'Âm thanh',
                          subtitle:
                              'Phát âm thanh khi có thông báo realtime lúc ứng dụng đang mở',
                          value: widget.settings
                              .soundEnabled,
                          onChanged: (value) {
                            _saveChange(() {
                              widget.settings
                                      .soundEnabled =
                                  value;
                            });
                          },
                        ),

                        _buildDivider(),

                        _buildSwitchItem(
                          icon:
                              Icons.vibration_outlined,
                          title: 'Rung',
                          subtitle:
                              'Rung nhẹ khi có thông báo realtime lúc ứng dụng đang mở',
                          value: widget.settings
                              .vibrationEnabled,
                          onChanged: (value) {
                            _saveChange(() {
                              widget.settings
                                      .vibrationEnabled =
                                  value;
                            });
                          },
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 18),

                  Container(
                    width: double.infinity,
                    padding:
                        const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color:
                          const Color(0xFFFFF1E8),
                      borderRadius:
                          BorderRadius.circular(
                        10,
                      ),
                    ),
                    child: Row(
                      crossAxisAlignment:
                          CrossAxisAlignment.start,
                      children: [
                        const Icon(
                          Icons.phone_android_outlined,
                          color:
                              AppTheme.orangeColor,
                          size: 20,
                        ),

                        const SizedBox(width: 10),

                        Expanded(
                          child: Text(
                            _isSaving
                                ? 'Đang lưu cài đặt trên thiết bị...'
                                : 'Cài đặt được lưu cục bộ trên thiết bị theo mã nhân viên và không thay đổi database dùng chung.',
                            style:
                                const TextStyle(
                              fontSize: 12,
                              height: 1.4,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _buildSwitchItem({
    required IconData icon,
    required String title,
    required String subtitle,
    required bool value,
    required ValueChanged<bool> onChanged,
  }) {
    return SwitchListTile(
      value: value,
      onChanged:
          _isSaving ? null : onChanged,
      activeThumbColor:
          AppTheme.orangeColor,
      secondary: Container(
        width: 42,
        height: 42,
        decoration:
            const BoxDecoration(
          color: Color(0xFFFFE9DE),
          shape: BoxShape.circle,
        ),
        child: Icon(
          icon,
          color:
              AppTheme.orangeColor,
          size: 21,
        ),
      ),
      title: Text(
        title,
        style: const TextStyle(
          fontSize: 14,
          fontWeight:
              FontWeight.w600,
        ),
      ),
      subtitle: Padding(
        padding:
            const EdgeInsets.only(
          top: 3,
        ),
        child: Text(
          subtitle,
          style: TextStyle(
            fontSize: 12,
            color:
                Colors.grey.shade600,
          ),
        ),
      ),
      contentPadding:
          const EdgeInsets.symmetric(
        horizontal: 14,
        vertical: 5,
      ),
    );
  }

  Widget _buildDivider() {
    return Padding(
      padding:
          const EdgeInsets.symmetric(
        horizontal: 16,
      ),
      child: Divider(
        height: 1,
        color:
            Colors.grey.shade200,
      ),
    );
  }
}
