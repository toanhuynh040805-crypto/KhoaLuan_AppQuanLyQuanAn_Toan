import 'dart:async';
import 'dart:ui';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'app/app.dart';

void _logFlutterError(FlutterErrorDetails details) {
  debugPrint('[FlutterError] ${details.exceptionAsString()}');
  final StackTrace? stack = details.stack;
  if (stack != null) {
    debugPrintStack(stackTrace: stack);
  }
}

Widget _buildFriendlyErrorWidget(FlutterErrorDetails details) {
  _logFlutterError(details);

  return const Material(
    color: Color(0xFFF6F7F9),
    child: SafeArea(
      child: Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.error_outline, size: 46, color: Color(0xFF8A3B12)),
              SizedBox(height: 14),
              Text(
                'Giao diện vừa gặp sự cố',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
              ),
              SizedBox(height: 8),
              Text(
                'Vui lòng quay lại màn trước và thử lại. Chi tiết lỗi đã được ghi ở console để kiểm tra.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    ),
  );
}

void main() {
  runZonedGuarded(
    () {
      // Binding và runApp phải được tạo trong cùng Zone để không phát sinh
      // Zone mismatch ở các bản Flutter mới.
      WidgetsFlutterBinding.ensureInitialized();

      FlutterError.onError = _logFlutterError;
      ErrorWidget.builder = _buildFriendlyErrorWidget;

      PlatformDispatcher.instance.onError = (Object error, StackTrace stack) {
        debugPrint('[PlatformError] $error');
        debugPrintStack(stackTrace: stack);
        return true;
      };

      runApp(const RestaurantApp());
    },
    (Object error, StackTrace stack) {
      debugPrint('[UncaughtAsyncError] $error');
      debugPrintStack(stackTrace: stack);
    },
  );
}
