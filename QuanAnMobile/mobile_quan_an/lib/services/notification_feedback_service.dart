import 'package:flutter/services.dart';

import '../models/notification_settings_state.dart';

class NotificationFeedbackService {
  const NotificationFeedbackService();

  Future<void> play(
    NotificationSettingsState settings,
  ) async {
    if (settings.soundEnabled) {
      try {
        await SystemSound.play(
          SystemSoundType.alert,
        );
      } catch (_) {
        // Một số thiết bị/emulator không có system alert sound.
      }
    }

    if (settings.vibrationEnabled) {
      try {
        await HapticFeedback.mediumImpact();
      } catch (_) {
        // Không làm hỏng luồng realtime nếu thiết bị không hỗ trợ rung.
      }
    }
  }
}
