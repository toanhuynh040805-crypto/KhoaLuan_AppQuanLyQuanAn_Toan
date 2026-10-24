import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_config.dart';

class ApiException implements Exception {
  final String message;
  final int? statusCode;
  final bool isNetworkError;

  const ApiException({
    required this.message,
    this.statusCode,
    this.isNetworkError = false,
  });

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;
  String? _accessToken;

  String? get accessToken => _accessToken;

  void setAccessToken(String? token) {
    final String normalized = token?.trim() ?? '';
    _accessToken = normalized.isEmpty ? null : normalized;
  }

  void clearAccessToken() => _accessToken = null;

  Map<String, String> get _headers {
    final Map<String, String> headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    final String? token = _accessToken;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  Future<dynamic> get(String endpoint) =>
      _execute(() => _client.get(_uri(endpoint), headers: _headers));

  Future<dynamic> post(String endpoint, {Map<String, dynamic>? body}) =>
      _execute(
        () => _client.post(
          _uri(endpoint),
          headers: _headers,
          body: jsonEncode(body ?? <String, dynamic>{}),
        ),
      );

  Future<dynamic> put(String endpoint, {Map<String, dynamic>? body}) =>
      _execute(
        () => _client.put(
          _uri(endpoint),
          headers: _headers,
          body: jsonEncode(body ?? <String, dynamic>{}),
        ),
      );

  Future<dynamic> putValue(String endpoint, Object? value) =>
      _execute(
        () => _client.put(
          _uri(endpoint),
          headers: _headers,
          body: jsonEncode(value),
        ),
      );

  Future<dynamic> delete(String endpoint) =>
      _execute(() => _client.delete(_uri(endpoint), headers: _headers));

  Uri _uri(String endpoint) {
    final String normalizedEndpoint = endpoint.startsWith('/')
        ? endpoint
        : '/$endpoint';
    return Uri.parse('${ApiConfig.baseUrl}$normalizedEndpoint');
  }

  Future<dynamic> _execute(Future<http.Response> Function() request) async {
    try {
      final http.Response response = await request().timeout(
        ApiConfig.receiveTimeout,
      );
      return _handleResponse(response);
    } on ApiException {
      rethrow;
    } on TimeoutException {
      throw const ApiException(
        message: 'Máy chủ phản hồi quá chậm. Vui lòng thử lại.',
        isNetworkError: true,
      );
    } on http.ClientException {
      throw const ApiException(
        message:
            'Không thể kết nối đến máy chủ. Hãy kiểm tra mạng hoặc backend.',
        isNetworkError: true,
      );
    } catch (_) {
      throw const ApiException(
        message: 'Không thể kết nối đến máy chủ. Vui lòng thử lại.',
        isNetworkError: true,
      );
    }
  }

  dynamic _handleResponse(http.Response response) {
    final int statusCode = response.statusCode;
    dynamic data;

    if (response.bodyBytes.isNotEmpty) {
      final String body = utf8.decode(response.bodyBytes, allowMalformed: true);
      try {
        data = jsonDecode(body);
      } catch (_) {
        data = body;
      }
    }

    if (statusCode >= 200 && statusCode < 300) return data;

    final String? rawServerMessage = _extractServerMessage(data);
    final String message = _friendlyMessage(statusCode, rawServerMessage);

    throw ApiException(message: message, statusCode: statusCode);
  }

  String? _extractServerMessage(dynamic data) {
    if (data is Map) {
      for (final String key in const ['message', 'title', 'error']) {
        final dynamic value = data[key];
        if (value is String && value.trim().isNotEmpty) {
          return value;
        }
      }

      final dynamic errors = data['errors'];
      if (errors is Map) {
        for (final dynamic value in errors.values) {
          if (value is List && value.isNotEmpty) {
            final dynamic first = value.first;
            if (first is String && first.trim().isNotEmpty) return first;
          }
          if (value is String && value.trim().isNotEmpty) return value;
        }
      }
    }

    if (data is String && data.trim().isNotEmpty) return data;
    return null;
  }

  String _friendlyMessage(int statusCode, String? rawMessage) {
    if (statusCode == 401) {
      return 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn';
    }
    if (statusCode == 403) {
      return 'Bạn không có quyền thực hiện chức năng này';
    }

    final String? safe = _sanitizeServerMessage(rawMessage);
    if (safe != null) return safe;

    if (statusCode == 400 || statusCode == 422) {
      return 'Thông tin chưa hợp lệ. Vui lòng kiểm tra lại.';
    }
    if (statusCode == 404) {
      return 'Không tìm thấy dữ liệu cần thao tác. Hãy tải lại và thử lại.';
    }
    if (statusCode == 409) {
      return 'Dữ liệu đã thay đổi hoặc đang được sử dụng. Hãy tải lại và thử lại.';
    }
    if (statusCode == 429) {
      return 'Bạn thao tác quá nhanh. Vui lòng thử lại sau một lúc.';
    }
    if (statusCode == 502 || statusCode == 503 || statusCode == 504) {
      return 'Dịch vụ tạm thời chưa sẵn sàng. Vui lòng thử lại sau.';
    }
    if (statusCode >= 500) {
      return 'Máy chủ đang gặp lỗi. Vui lòng thử lại sau.';
    }

    return 'Không thể thực hiện thao tác. Vui lòng thử lại.';
  }

  String? _sanitizeServerMessage(String? rawMessage) {
    if (rawMessage == null) return null;

    final String normalized = rawMessage.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (normalized.isEmpty) return null;

    final String lower = normalized.toLowerCase();
    const List<String> technicalMarkers = [
      'package:flutter/',
      'framework.dart',
      'failed assertion',
      'stack trace',
      'system.',
      'microsoft.',
      'sqlexception',
      'dbupdateexception',
      'invalidoperationexception',
      'nullreferenceexception',
      'failed executing dbcommand',
      '<!doctype',
      '<html',
      ' at system.',
      ' at microsoft.',
    ];

    if (technicalMarkers.any(lower.contains)) return null;
    if (normalized.length > 260) return null;
    return normalized;
  }

  void dispose() => _client.close();
}
