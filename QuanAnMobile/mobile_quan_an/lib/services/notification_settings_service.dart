import 'package:shared_preferences/shared_preferences.dart';

import '../models/notification_settings_state.dart';

class NotificationSettingsService {
  const NotificationSettingsService();

  Future<NotificationSettingsState> getSettings({
    required String employeeCode,
  }) async {
    final SharedPreferences prefs =
        await SharedPreferences.getInstance();

    final String prefix =
        'notification_settings_$employeeCode';

    return NotificationSettingsState(
      kitchenFinished:
          prefs.getBool(
            '${prefix}_kitchenFinished',
          ) ??
          true,
      paymentRequest:
          prefs.getBool(
            '${prefix}_paymentRequest',
          ) ??
          true,
      tableNotification:
          prefs.getBool(
            '${prefix}_tableNotification',
          ) ??
          true,
      soundEnabled:
          prefs.getBool(
            '${prefix}_soundEnabled',
          ) ??
          true,
      vibrationEnabled:
          prefs.getBool(
            '${prefix}_vibrationEnabled',
          ) ??
          true,
    );
  }

  Future<void> updateSettings({
    required String employeeCode,
    required NotificationSettingsState settings,
  }) async {
    final SharedPreferences prefs =
        await SharedPreferences.getInstance();

    final String prefix =
        'notification_settings_$employeeCode';

    await prefs.setBool(
      '${prefix}_kitchenFinished',
      settings.kitchenFinished,
    );

    await prefs.setBool(
      '${prefix}_paymentRequest',
      settings.paymentRequest,
    );

    await prefs.setBool(
      '${prefix}_tableNotification',
      settings.tableNotification,
    );

    await prefs.setBool(
      '${prefix}_soundEnabled',
      settings.soundEnabled,
    );

    await prefs.setBool(
      '${prefix}_vibrationEnabled',
      settings.vibrationEnabled,
    );
  }
}
