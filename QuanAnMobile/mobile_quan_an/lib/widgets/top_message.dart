import 'dart:math' as math;

import 'package:flutter/material.dart';

enum TopMessageType { success, error, warning, info }

class TopMessage {
  static void show(
    BuildContext context, {
    required String message,
    TopMessageType type = TopMessageType.info,
    Duration? duration,
  }) {
    final ScaffoldMessengerState? messenger = ScaffoldMessenger.maybeOf(
      context,
    );
    if (messenger == null || !messenger.mounted) return;

    final String displayMessage = _safeMessage(message);
    final Duration resolvedDuration = duration ?? _durationFor(type);

    final MediaQueryData? media = MediaQuery.maybeOf(context);
    final double availableHeight = media == null
        ? 760
        : media.size.height - media.viewInsets.bottom;
    final double topPadding = media?.padding.top ?? 0;
    final double bottomMargin = math.max(
      20,
      availableHeight - topPadding - 150,
    );

    Color backgroundColor;
    IconData icon;
    switch (type) {
      case TopMessageType.success:
        backgroundColor = Colors.green.shade700;
        icon = Icons.check_circle_outline;
        break;
      case TopMessageType.error:
        backgroundColor = Colors.red.shade700;
        icon = Icons.error_outline;
        break;
      case TopMessageType.warning:
        backgroundColor = Colors.orange.shade700;
        icon = Icons.warning_amber_rounded;
        break;
      case TopMessageType.info:
        backgroundColor = const Color(0xFF333333);
        icon = Icons.info_outline;
        break;
    }

    messenger
      ..removeCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          duration: resolvedDuration,
          behavior: SnackBarBehavior.floating,
          dismissDirection: DismissDirection.up,
          backgroundColor: backgroundColor,
          margin: EdgeInsets.fromLTRB(16, 0, 16, bottomMargin),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          content: Row(
            children: [
              Icon(icon, color: Colors.white, size: 22),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  displayMessage,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),
        ),
      );
  }

  static Duration _durationFor(TopMessageType type) {
    switch (type) {
      case TopMessageType.error:
        return const Duration(milliseconds: 2600);
      case TopMessageType.warning:
        return const Duration(milliseconds: 2200);
      case TopMessageType.info:
        return const Duration(milliseconds: 1700);
      case TopMessageType.success:
        return const Duration(milliseconds: 1300);
    }
  }

  static String _safeMessage(String raw) {
    final String normalized = raw.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (normalized.isEmpty) {
      return 'Không thể thực hiện thao tác. Vui lòng thử lại.';
    }

    final String lower = normalized.toLowerCase();
    const List<String> technicalMarkers = [
      'package:flutter/',
      'framework.dart',
      'failed assertion',
      'stack trace',
      'sqlexception',
      'dbupdateexception',
      'system.',
      'microsoft.',
      'nullreferenceexception',
      'invalidoperationexception',
    ];

    if (technicalMarkers.any(lower.contains)) {
      return 'Ứng dụng gặp sự cố khi thực hiện thao tác. Vui lòng thử lại.';
    }

    if (normalized.length > 260) {
      return '${normalized.substring(0, 257)}...';
    }
    return normalized;
  }
}
