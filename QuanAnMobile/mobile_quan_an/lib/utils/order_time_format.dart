String formatElapsedDuration(DateTime? value) {
  if (value == null) return '';

  final DateTime now = DateTime.now();
  Duration diff = now.difference(value);

  if (diff.isNegative) {
    diff = Duration.zero;
  }

  if (diff.inMinutes < 1) {
    return 'chưa đến 1 phút';
  }

  if (diff.inHours < 1) {
    return '${diff.inMinutes} phút';
  }

  if (diff.inDays < 1) {
    final int hours = diff.inHours;
    final int minutes = diff.inMinutes.remainder(60);
    return minutes > 0
        ? '$hours giờ $minutes phút'
        : '$hours giờ';
  }

  final int days = diff.inDays;
  final int hours = diff.inHours.remainder(24);
  return hours > 0
      ? '$days ngày $hours giờ'
      : '$days ngày';
}

String formatTimeAgo(DateTime? value) {
  if (value == null) return '';

  final DateTime now = DateTime.now();
  final Duration diff = now.difference(value);

  if (!diff.isNegative && diff.inMinutes < 1) {
    return 'vừa xong';
  }

  return '${formatElapsedDuration(value)} trước';
}

String formatClockTime(DateTime? value) {
  if (value == null) return '';

  final String hour = value.hour.toString().padLeft(2, '0');
  final String minute = value.minute.toString().padLeft(2, '0');
  return '$hour:$minute';
}
