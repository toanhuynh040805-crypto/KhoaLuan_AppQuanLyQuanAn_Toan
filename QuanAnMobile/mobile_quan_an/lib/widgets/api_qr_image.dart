import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../services/app_services.dart';

class ApiQrImage extends StatefulWidget {
  final String imageUrl;
  final BoxFit fit;

  const ApiQrImage({
    super.key,
    required this.imageUrl,
    this.fit = BoxFit.contain,
  });

  @override
  State<ApiQrImage> createState() => _ApiQrImageState();
}

class _ApiQrImageState extends State<ApiQrImage> {
  late Future<Uint8List> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void didUpdateWidget(covariant ApiQrImage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.imageUrl != widget.imageUrl) {
      _future = _load();
    }
  }

  Future<Uint8List> _load() {
    return AppServices.apiClient.getBytes(widget.imageUrl);
  }

  void _retry() {
    setState(() {
      _future = _load();
    });
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Uint8List>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(
            child: SizedBox(
              width: 28,
              height: 28,
              child: CircularProgressIndicator(strokeWidth: 2.5),
            ),
          );
        }

        final Uint8List? bytes = snapshot.data;
        if (snapshot.hasError || bytes == null || bytes.isEmpty) {
          return InkWell(
            onTap: _retry,
            child: const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.refresh, size: 26),
                  SizedBox(height: 6),
                  Text(
                    'Không tải được QR\nChạm để thử lại',
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          );
        }

        return Image.memory(
          bytes,
          fit: widget.fit,
          gaplessPlayback: true,
        );
      },
    );
  }
}
