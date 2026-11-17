import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../services/api_config.dart';
import '../../services/server_connection_service.dart';

class ServerSetupScreen extends StatefulWidget {
  final bool autoDiscover;
  final bool allowBack;
  final VoidCallback onConnected;

  const ServerSetupScreen({
    super.key,
    required this.onConnected,
    this.autoDiscover = false,
    this.allowBack = true,
  });

  @override
  State<ServerSetupScreen> createState() =>
      _ServerSetupScreenState();
}

class _ServerSetupScreenState
    extends State<ServerSetupScreen> {
  static const ServerConnectionService _service =
      ServerConnectionService();

  final TextEditingController _serverController =
      TextEditingController();

  bool _working = false;
  String _status =
      'Nhập IPv4 của máy đang chạy Backend hoặc dùng tự tìm.';

  @override
  void initState() {
    super.initState();
    unawaited(_initialize());
  }

  @override
  void dispose() {
    _serverController.dispose();
    super.dispose();
  }

  Future<void> _initialize() async {
    final ServerTarget? saved =
        await _service.loadSavedTarget();

    if (!mounted) return;

    if (saved != null) {
      _serverController.text = saved.authority;
    }

    if (widget.autoDiscover) {
      await _autoDiscover();
    }
  }

  Future<void> _autoDiscover() async {
    if (_working) return;

    setState(() {
      _working = true;
      _status = 'Đang kiểm tra máy chủ...';
    });

    final ServerTarget? target =
        await _service.findAvailableServer(
      onProgress: (String message) {
        if (!mounted) return;
        setState(() {
          _status = message;
        });
      },
    );

    if (!mounted) return;

    if (target == null) {
      setState(() {
        _working = false;
        _status =
            'Không tự tìm thấy Backend. Hãy xem IPv4 trên máy chủ bằng ipconfig rồi nhập bên dưới.';
      });
      return;
    }

    _serverController.text = target.authority;
    setState(() {
      _working = false;
      _status = 'Đã kết nối ${target.authority}';
    });

    widget.onConnected();
  }

  Future<void> _connectManual() async {
    if (_working) return;

    final ServerTarget? target =
        ServerTarget.tryParse(_serverController.text);

    if (target == null) {
      setState(() {
        _status =
            'Địa chỉ không hợp lệ. Ví dụ: 192.168.1.10 hoặc 192.168.1.10:5000';
      });
      return;
    }

    setState(() {
      _working = true;
      _status =
          'Đang kết nối ${target.authority}...';
    });

    final bool connected =
        await _service.connectTo(target);

    if (!mounted) return;

    if (!connected) {
      setState(() {
        _working = false;
        _status =
            'Không kết nối được ${target.authority}. Kiểm tra Backend đang chạy 0.0.0.0:5000 và hai thiết bị cùng Wi-Fi.';
      });
      return;
    }

    setState(() {
      _working = false;
      _status = 'Đã kết nối ${target.authority}';
    });

    widget.onConnected();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: widget.allowBack
          ? AppBar(
              title: const Text('Cấu hình máy chủ'),
            )
          : null,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(
                maxWidth: 460,
              ),
              child: Column(
                crossAxisAlignment:
                    CrossAxisAlignment.stretch,
                children: [
                  Container(
                    width: 72,
                    height: 72,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: AppTheme.orangeColor,
                      borderRadius:
                          BorderRadius.circular(18),
                    ),
                    child: const Icon(
                      Icons.dns_outlined,
                      size: 38,
                      color: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Kết nối máy chủ quán',
                    style: TextStyle(
                      fontSize: 23,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'APK không còn khóa cứng IP. Chỉ cần điện thoại và máy chạy Backend cùng mạng Wi-Fi/LAN.',
                    style: TextStyle(
                      color: Colors.grey.shade700,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: 24),
                  TextField(
                    controller: _serverController,
                    enabled: !_working,
                    keyboardType:
                        TextInputType.url,
                    textInputAction:
                        TextInputAction.done,
                    onSubmitted:
                        (_) => _connectManual(),
                    decoration: const InputDecoration(
                      labelText: 'IP máy chủ',
                      hintText: '192.168.1.10',
                      prefixIcon:
                          Icon(Icons.lan_outlined),
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    'API sẽ dùng http://IP:5000/api và SignalR dùng http://IP:5000/hubs/order.',
                    style: TextStyle(
                      fontSize: 12,
                      color: Colors.grey.shade600,
                    ),
                  ),
                  const SizedBox(height: 18),
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: const Color(0xFFF5F6F8),
                      borderRadius:
                          BorderRadius.circular(12),
                    ),
                    child: Row(
                      crossAxisAlignment:
                          CrossAxisAlignment.start,
                      children: [
                        if (_working)
                          const Padding(
                            padding:
                                EdgeInsets.only(top: 2),
                            child: SizedBox(
                              width: 18,
                              height: 18,
                              child:
                                  CircularProgressIndicator(
                                strokeWidth: 2,
                              ),
                            ),
                          )
                        else
                          const Icon(
                            Icons.info_outline,
                            size: 20,
                          ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            _status,
                            style: const TextStyle(
                              height: 1.35,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 18),
                  SizedBox(
                    height: 50,
                    child: ElevatedButton.icon(
                      onPressed:
                          _working ? null : _connectManual,
                      icon: const Icon(Icons.link),
                      label: const Text(
                        'Kết nối và lưu',
                      ),
                    ),
                  ),
                  const SizedBox(height: 10),
                  SizedBox(
                    height: 48,
                    child: OutlinedButton.icon(
                      onPressed:
                          _working ? null : _autoDiscover,
                      icon: const Icon(Icons.wifi_find),
                      label: const Text(
                        'Tự tìm trong Wi-Fi',
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'Backend trên máy chủ:',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: Colors.grey.shade800,
                    ),
                  ),
                  const SizedBox(height: 6),
                  SelectableText(
                    'dotnet run --urls "http://0.0.0.0:5000"',
                    style: TextStyle(
                      fontFamily: 'monospace',
                      color: Colors.grey.shade800,
                    ),
                  ),
                  if (ApiConfig.serverHost.isNotEmpty) ...[
                    const SizedBox(height: 14),
                    Text(
                      'Đang cấu hình: ${ApiConfig.serverHost}:${ApiConfig.serverPort}',
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.grey.shade600,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
