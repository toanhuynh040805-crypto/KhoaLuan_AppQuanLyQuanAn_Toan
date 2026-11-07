class AppNotification {
  final int? id;
  final String title;
  final String message;
  final String time;
  final bool isRead;
  final String notificationType;
  final String? relatedEntityType;
  final int? relatedEntityId;
  final String? eventKey;

  const AppNotification({
    this.id,
    required this.title,
    required this.message,
    required this.time,
    this.isRead = false,
    this.notificationType = '',
    this.relatedEntityType,
    this.relatedEntityId,
    this.eventKey,
  });

  factory AppNotification.fromJson(
    Map<String, dynamic> json,
  ) {
    return AppNotification(
      id: json['id'] == null
          ? null
          : _parseInt(json['id']),
      title:
          json['title']?.toString() ?? '',
      message:
          json['message']?.toString() ??
              json['content']?.toString() ??
              '',
      time:
          json['time']?.toString() ??
              'Vừa xong',
      isRead:
          _parseBool(json['isRead']),
      notificationType:
          json['notificationType']?.toString() ??
              '',
      relatedEntityType:
          json['relatedEntityType']?.toString(),
      relatedEntityId:
          json['relatedEntityId'] == null
              ? null
              : _parseInt(
                  json['relatedEntityId'],
                ),
      eventKey:
          json['eventKey']?.toString(),
    );
  }

  AppNotification copyWith({
    int? id,
    String? title,
    String? message,
    String? time,
    bool? isRead,
    String? notificationType,
    String? relatedEntityType,
    int? relatedEntityId,
    String? eventKey,
  }) {
    return AppNotification(
      id: id ?? this.id,
      title: title ?? this.title,
      message: message ?? this.message,
      time: time ?? this.time,
      isRead: isRead ?? this.isRead,
      notificationType:
          notificationType ??
              this.notificationType,
      relatedEntityType:
          relatedEntityType ??
              this.relatedEntityType,
      relatedEntityId:
          relatedEntityId ??
              this.relatedEntityId,
      eventKey:
          eventKey ?? this.eventKey,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'title': title,
      'message': message,
      'time': time,
      'isRead': isRead,
      'notificationType':
          notificationType,
      'relatedEntityType':
          relatedEntityType,
      'relatedEntityId':
          relatedEntityId,
      'eventKey':
          eventKey,
    };
  }

  static int _parseInt(dynamic value) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }

  static bool _parseBool(dynamic value) {
    if (value is bool) {
      return value;
    }

    if (value is num) {
      return value != 0;
    }

    return value?.toString().toLowerCase() ==
        'true';
  }
}
