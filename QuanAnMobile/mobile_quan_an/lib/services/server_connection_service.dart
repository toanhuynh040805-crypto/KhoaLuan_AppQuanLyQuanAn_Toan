import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'api_config.dart';

class ServerTarget {
  final String host;
  final int port;

  const ServerTarget({
    required this.host,
    this.port = 5000,
  });

  String get authority => '$host:$port';
  String get origin => 'http://$authority';
  String get apiBaseUrl => '$origin/api';
  String get healthUrl => '$apiBaseUrl/health';

  static ServerTarget? tryParse(String raw) {
    String value = raw.trim();
    if (value.isEmpty) return null;

    if (!value.contains('://')) {
      value = 'http://$value';
    }

    final Uri? uri = Uri.tryParse(value);
    if (uri == null || uri.host.trim().isEmpty) {
      return null;
    }

    final int port = uri.hasPort ? uri.port : 5000;
    if (port <= 0 || port > 65535) {
      return null;
    }

    return ServerTarget(
      host: uri.host.trim(),
      port: port,
    );
  }

  @override
  bool operator ==(Object other) =>
      other is ServerTarget &&
      other.host == host &&
      other.port == port;

  @override
  int get hashCode => Object.hash(host, port);
}

class ServerConnectionService {
  const ServerConnectionService();

  static const String _hostKey = 'server_host';
  static const String _portKey = 'server_port';
  static const Duration _quickTimeout =
      Duration(milliseconds: 700);
  static const Duration _scanTimeout =
      Duration(milliseconds: 550);
  static const int _scanBatchSize = 32;

  Future<ServerTarget?> loadSavedTarget() async {
    final SharedPreferences preferences =
        await SharedPreferences.getInstance();
    final String host =
        preferences.getString(_hostKey)?.trim() ?? '';
    final int port = preferences.getInt(_portKey) ?? 5000;

    if (host.isEmpty || port <= 0 || port > 65535) {
      return null;
    }

    return ServerTarget(host: host, port: port);
  }

  Future<void> saveTarget(ServerTarget target) async {
    final SharedPreferences preferences =
        await SharedPreferences.getInstance();
    await preferences.setString(_hostKey, target.host);
    await preferences.setInt(_portKey, target.port);
  }

  Future<void> applyTarget(
    ServerTarget target, {
    bool persist = true,
  }) async {
    ApiConfig.configureServer(
      host: target.host,
      port: target.port,
    );

    if (persist) {
      await saveTarget(target);
    }
  }

  Future<bool> isReachable(
    ServerTarget target, {
    Duration timeout = const Duration(seconds: 2),
  }) async {
    final http.Client client = http.Client();
    try {
      return await _probe(
        client,
        target,
        timeout: timeout,
      );
    } finally {
      client.close();
    }
  }

  Future<bool> connectTo(ServerTarget target) async {
    final bool reachable = await isReachable(target);
    if (!reachable) return false;

    await applyTarget(target);
    return true;
  }

  Future<ServerTarget?> findAvailableServer({
    void Function(String message)? onProgress,
  }) async {
    final LinkedHashSet<ServerTarget> preferred =
        LinkedHashSet<ServerTarget>();

    final ServerTarget? saved = await loadSavedTarget();
    if (saved != null) {
      preferred.add(saved);
    }

    // Android Emulator truy cập máy Windows qua 10.0.2.2.
    preferred.add(
      const ServerTarget(host: '10.0.2.2', port: 5000),
    );

    final http.Client client = http.Client();
    try {
      for (final ServerTarget target in preferred) {
        onProgress?.call(
          'Đang thử máy chủ ${target.authority}...',
        );
        final bool ok = await _probe(
          client,
          target,
          timeout: _quickTimeout,
        );
        if (ok) {
          await applyTarget(target);
          return target;
        }
      }

      onProgress?.call(
        'Đang tự tìm máy chủ trong Wi-Fi hiện tại...',
      );

      final List<String> prefixes =
          await _privateIpv4Prefixes();

      for (final String prefix in prefixes) {
        final ServerTarget? target =
            await _scanPrefix(
          client,
          prefix,
          attempted: preferred,
          onProgress: onProgress,
        );

        if (target != null) {
          await applyTarget(target);
          return target;
        }
      }
    } finally {
      client.close();
    }

    return null;
  }

  Future<ServerTarget?> _scanPrefix(
    http.Client client,
    String prefix, {
    required Set<ServerTarget> attempted,
    void Function(String message)? onProgress,
  }) async {
    for (int start = 1; start <= 254; start += _scanBatchSize) {
      final int end =
          (start + _scanBatchSize - 1).clamp(1, 254).toInt();

      onProgress?.call(
        'Đang dò $prefix.$start - $prefix.$end...',
      );

      final List<ServerTarget> targets = <ServerTarget>[];
      for (int host = start; host <= end; host++) {
        final ServerTarget target = ServerTarget(
          host: '$prefix.$host',
          port: 5000,
        );
        if (!attempted.contains(target)) {
          targets.add(target);
          attempted.add(target);
        }
      }

      final List<bool> results = await Future.wait(
        targets.map(
          (ServerTarget target) => _probe(
            client,
            target,
            timeout: _scanTimeout,
          ),
        ),
      );

      for (int i = 0; i < results.length; i++) {
        if (results[i]) {
          return targets[i];
        }
      }
    }

    return null;
  }

  Future<bool> _probe(
    http.Client client,
    ServerTarget target, {
    required Duration timeout,
  }) async {
    try {
      final http.Response response = await client
          .get(Uri.parse(target.healthUrl))
          .timeout(timeout);

      if (response.statusCode != 200) {
        return false;
      }

      final dynamic body = jsonDecode(response.body);
      if (body is! Map) return false;

      final String service =
          body['service']?.toString().trim() ?? '';
      final String status =
          body['status']?.toString().trim().toLowerCase() ?? '';

      return service == 'QuanAnBackend' && status == 'ok';
    } catch (_) {
      return false;
    }
  }

  Future<List<String>> _privateIpv4Prefixes() async {
    final Set<String> prefixes = <String>{};

    try {
      final List<NetworkInterface> interfaces =
          await NetworkInterface.list(
        type: InternetAddressType.IPv4,
        includeLoopback: false,
        includeLinkLocal: false,
      );

      for (final NetworkInterface interface in interfaces) {
        for (final InternetAddress address
            in interface.addresses) {
          final List<String> parts =
              address.address.split('.');
          if (parts.length != 4) continue;

          final int? first = int.tryParse(parts[0]);
          final int? second = int.tryParse(parts[1]);
          if (first == null || second == null) continue;

          final bool isPrivate =
              first == 10 ||
              (first == 172 && second >= 16 && second <= 31) ||
              (first == 192 && second == 168);

          if (!isPrivate) continue;

          prefixes.add(
            '${parts[0]}.${parts[1]}.${parts[2]}',
          );
        }
      }
    } catch (_) {
      // Nếu hệ điều hành không cho đọc interface, người dùng vẫn có thể
      // nhập IP máy chủ thủ công ở màn cấu hình.
    }

    return prefixes.toList();
  }
}
