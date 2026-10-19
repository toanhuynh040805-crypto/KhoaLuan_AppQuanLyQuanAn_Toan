import 'employee_profile.dart';

class NotificationSettingsState {
  bool kitchenFinished;
  bool paymentRequest;
  bool tableNotification;
  bool soundEnabled;
  bool vibrationEnabled;

  NotificationSettingsState({
    this.kitchenFinished = true,
    this.paymentRequest = true,
    this.tableNotification = true,
    this.soundEnabled = true,
    this.vibrationEnabled = true,
  });

  factory NotificationSettingsState.fromJson(
    Map<String, dynamic> json,
  ) {
    return NotificationSettingsState(
      kitchenFinished:
          _parseBool(
        json['kitchenFinished'],
        fallback: true,
      ),
      paymentRequest:
          _parseBool(
        json['paymentRequest'],
        fallback: true,
      ),
      tableNotification:
          _parseBool(
        json['tableNotification'],
        fallback: true,
      ),
      soundEnabled:
          _parseBool(
        json['soundEnabled'],
        fallback: true,
      ),
      vibrationEnabled:
          _parseBool(
        json['vibrationEnabled'],
        fallback: true,
      ),
    );
  }

  void copyFrom(
    NotificationSettingsState other,
  ) {
    kitchenFinished =
        other.kitchenFinished;
    paymentRequest =
        other.paymentRequest;
    tableNotification =
        other.tableNotification;
    soundEnabled =
        other.soundEnabled;
    vibrationEnabled =
        other.vibrationEnabled;
  }

  Map<String, dynamic> toJson() {
    return {
      'kitchenFinished':
          kitchenFinished,
      'paymentRequest':
          paymentRequest,
      'tableNotification':
          tableNotification,
      'soundEnabled':
          soundEnabled,
      'vibrationEnabled':
          vibrationEnabled,
    };
  }

  static bool _parseBool(
    dynamic value, {
    required bool fallback,
  }) {
    if (value == null) {
      return fallback;
    }

    if (value is bool) {
      return value;
    }

    if (value is num) {
      return value != 0;
    }

    return value
            .toString()
            .toLowerCase() ==
        'true';
  }
}

final Map<String, NotificationSettingsState>
    _notificationSettingsStates = {};

NotificationSettingsState
    get appNotificationSettingsState {
  return _notificationSettingsStates.putIfAbsent(
    appEmployee.employeeCode,
    () => NotificationSettingsState(),
  );
}
