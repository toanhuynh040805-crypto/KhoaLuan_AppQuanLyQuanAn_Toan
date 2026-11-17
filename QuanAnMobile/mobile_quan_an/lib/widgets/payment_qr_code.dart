import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import 'api_qr_image.dart';

class PaymentQrCode extends StatelessWidget {
  final String payload;
  final String fallbackImageUrl;
  final double size;

  const PaymentQrCode({
    super.key,
    required this.payload,
    required this.fallbackImageUrl,
    this.size = 220,
  });

  @override
  Widget build(BuildContext context) {
    final String normalizedPayload = payload.trim();

    if (normalizedPayload.isNotEmpty) {
      return ColoredBox(
        color: Colors.white,
        child: Center(
          child: QrImageView(
            data: normalizedPayload,
            version: QrVersions.auto,
            size: size,
            padding: const EdgeInsets.all(8),
            backgroundColor: Colors.white,
            eyeStyle: const QrEyeStyle(
              eyeShape: QrEyeShape.square,
              color: Colors.black,
            ),
            dataModuleStyle: const QrDataModuleStyle(
              dataModuleShape: QrDataModuleShape.square,
              color: Colors.black,
            ),
            errorCorrectionLevel: QrErrorCorrectLevel.M,
            errorStateBuilder: (context, error) {
              return _fallback();
            },
          ),
        ),
      );
    }

    return _fallback();
  }

  Widget _fallback() {
    if (fallbackImageUrl.trim().isEmpty) {
      return const Center(
        child: Text(
          'Chưa có mã QR.',
          textAlign: TextAlign.center,
        ),
      );
    }

    return ApiQrImage(
      imageUrl: fallbackImageUrl,
      fit: BoxFit.contain,
    );
  }
}
