// Dialog thanh toán tự quản lý controller và state theo vòng đời route.
import 'dart:async';

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/payment_method_option.dart';
import '../../models/payment_preview.dart';
import '../../services/app_services.dart';
import '../../widgets/payment_qr_code.dart';

class PaymentDialogResult {
  final int method;
  final String? transactionRef;

  const PaymentDialogResult({
    required this.method,
    this.transactionRef,
  });
}

class PaymentDialog extends StatefulWidget {
  final PaymentPreview preview;

  const PaymentDialog({
    super.key,
    required this.preview,
  });

  @override
  State<PaymentDialog> createState() =>
      _PaymentDialogState();
}

class _PaymentDialogState
    extends State<PaymentDialog> {
  late final TextEditingController
      _receivedController;
  final List<PaymentMethodOption>
      _paymentMethods = [];
  PaymentMethodOption? _method;
  late int _received;
  bool _loadingMethods = true;
  bool _methodsLoadFailed = false;

  PaymentPreview get preview =>
      widget.preview;

  @override
  void initState() {
    super.initState();
    _received = preview.totalAmount;
    _receivedController =
        TextEditingController(
      text: preview.totalAmount.toString(),
    );
    unawaited(_loadPaymentMethods());
  }

  @override
  void dispose() {
    _receivedController.dispose();
    super.dispose();
  }

  Future<void> _loadPaymentMethods() async {
    if (mounted) {
      setState(() {
        _loadingMethods = true;
        _methodsLoadFailed = false;
      });
    }

    try {
      final List<PaymentMethodOption>
          methods = await AppServices
              .paymentService
              .getPaymentMethods();

      if (!mounted) return;

      setState(() {
        _paymentMethods
          ..clear()
          ..addAll(methods);
        _method =
            methods.isEmpty
                ? null
                : methods.first;
        _loadingMethods = false;
        _methodsLoadFailed =
            methods.isEmpty;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _paymentMethods.clear();
        _method = null;
        _loadingMethods = false;
        _methodsLoadFailed = true;
      });
    }
  }

  bool get _canConfirm {
    final PaymentMethodOption? method =
        _method;
    if (method == null) return false;

    return !method.isCash ||
        _received >=
            preview.totalAmount;
  }

  int get _change {
    final PaymentMethodOption? method =
        _method;

    return method != null &&
            method.isCash &&
            _received >
                preview.totalAmount
        ? _received -
            preview.totalAmount
        : 0;
  }

  String _money(int amount) {
    final value = amount.toString();
    final result = StringBuffer();

    for (int i = 0;
        i < value.length;
        i++) {
      if (i > 0 &&
          (value.length - i) % 3 ==
              0) {
        result.write('.');
      }
      result.write(value[i]);
    }

    return '${result}đ';
  }

  Widget _row(
    String label,
    String value, {
    bool bold = false,
  }) {
    return Padding(
      padding:
          const EdgeInsets.symmetric(
        vertical: 4,
      ),
      child: Row(
        mainAxisAlignment:
            MainAxisAlignment
                .spaceBetween,
        children: [
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                fontWeight:
                    bold
                        ? FontWeight.bold
                        : FontWeight.normal,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Text(
            value,
            style: TextStyle(
              fontWeight:
                  FontWeight.bold,
              color:
                  bold
                      ? AppTheme
                          .orangeColor
                      : AppTheme
                          .textColor,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPaymentMethods() {
    if (_loadingMethods) {
      return const Padding(
        padding:
            EdgeInsets.symmetric(
          vertical: 12,
        ),
        child: Center(
          child: SizedBox(
            width: 24,
            height: 24,
            child:
                CircularProgressIndicator(
              strokeWidth: 2.4,
            ),
          ),
        ),
      );
    }

    if (_methodsLoadFailed ||
        _paymentMethods.isEmpty ||
        _method == null) {
      return Column(
        crossAxisAlignment:
            CrossAxisAlignment.start,
        children: [
          const Text(
            'Không tải được hình thức thanh toán từ hệ thống.',
            style: TextStyle(
              color: Colors.redAccent,
            ),
          ),
          const SizedBox(height: 6),
          TextButton.icon(
            onPressed:
                _loadPaymentMethods,
            icon:
                const Icon(Icons.refresh),
            label:
                const Text('Tải lại'),
          ),
        ],
      );
    }

    return InputDecorator(
      decoration:
          const InputDecoration(
        labelText:
            'Hình thức thanh toán',
        border: OutlineInputBorder(),
        contentPadding:
            EdgeInsets.symmetric(
          horizontal: 12,
          vertical: 4,
        ),
      ),
      child:
          DropdownButtonHideUnderline(
        child: DropdownButton<int>(
          value: _method!.method,
          isExpanded: true,
          items: _paymentMethods
              .map(
                (option) =>
                    DropdownMenuItem<int>(
                  value:
                      option.method,
                  child: Text(
                    option.label,
                  ),
                ),
              )
              .toList(),
          onChanged: (value) {
            if (value == null) return;

            final PaymentMethodOption
                selected =
                _paymentMethods
                    .firstWhere(
              (option) =>
                  option.method ==
                  value,
            );

            setState(
              () => _method = selected,
            );
          },
        ),
      ),
    );
  }

  Widget _buildTransferQr() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color:
            const Color(0xFFFFF8F4),
        borderRadius:
            BorderRadius.circular(14),
        border: Border.all(
          color: AppTheme.orangeColor
              .withValues(alpha: 0.28),
        ),
      ),
      child: Column(
        children: [
          Text(
            'Quét QR để chuyển khoản',
            style: const TextStyle(
              fontSize: 16,
              fontWeight:
                  FontWeight.w700,
            ),
          ),
          const SizedBox(height: 10),
          if (preview.qrPayload.isNotEmpty ||
              preview.qrImageUrl.isNotEmpty)
            SizedBox(
              width: 230,
              height: 230,
              child: PaymentQrCode(
                payload: preview.qrPayload,
                fallbackImageUrl: preview.qrImageUrl,
                size: 218,
              ),
            )
          else
            const Padding(
              padding: EdgeInsets.all(24),
              child: Text('Chưa có mã QR.'),
            ),
          const SizedBox(height: 10),
          Text(
            _money(
              preview.totalAmount,
            ),
            style: const TextStyle(
              color:
                  AppTheme.orangeColor,
              fontSize: 21,
              fontWeight:
                  FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            preview.bankAccountName,
            style: const TextStyle(
              fontWeight:
                  FontWeight.w700,
            ),
          ),
          const SizedBox(height: 3),
          Text(
            '${preview.bankName} • '
            '${preview.bankAccountNo}',
            textAlign:
                TextAlign.center,
          ),
          if (preview
              .bankBranch
              .isNotEmpty) ...[
            const SizedBox(height: 3),
            Text(
              preview.bankBranch,
              textAlign:
                  TextAlign.center,
              style: TextStyle(
                color: Colors
                    .grey.shade700,
                fontSize: 12,
              ),
            ),
          ],
          if (preview
              .transferContent
              .isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              'Nội dung: '
              '${preview.transferContent}',
              textAlign:
                  TextAlign.center,
              style: TextStyle(
                color: Colors
                    .grey.shade700,
                fontSize: 12,
              ),
            ),
          ],
        ],
      ),
    );
  }

  @override
  Widget build(
    BuildContext context,
  ) {
    final PaymentMethodOption? method =
        _method;

    return AlertDialog(
      title:
          const Text(
        'Xác nhận thanh toán',
      ),
      content: SizedBox(
        width: 390,
        child:
            SingleChildScrollView(
          child: Column(
            mainAxisSize:
                MainAxisSize.min,
            crossAxisAlignment:
                CrossAxisAlignment
                    .start,
            children: [
              _row(
                'Tạm tính',
                _money(
                  preview.subTotal,
                ),
              ),
              if (preview
                      .discountAmount >
                  0)
                _row(
                  'Giảm giá',
                  '-${_money(preview.discountAmount)}',
                ),
              if (preview
                      .vipRoomFee >
                  0)
                _row(
                  'Phí phòng VIP',
                  _money(
                    preview.vipRoomFee,
                  ),
                ),
              if (preview
                      .vipMinimumGap >
                  0)
                _row(
                  'Bù hóa đơn tối thiểu',
                  _money(
                    preview
                        .vipMinimumGap,
                  ),
                ),
              const Divider(
                height: 24,
              ),
              _row(
                'Tổng thanh toán',
                _money(
                  preview.totalAmount,
                ),
                bold: true,
              ),
              const SizedBox(
                height: 16,
              ),
              _buildPaymentMethods(),
              if (method != null &&
                  method.isCash) ...[
                const SizedBox(
                  height: 14,
                ),
                TextField(
                  controller:
                      _receivedController,
                  keyboardType:
                      TextInputType
                          .number,
                  decoration:
                      InputDecoration(
                    labelText:
                        'Tiền khách đưa',
                    suffixText: 'đ',
                    border:
                        const OutlineInputBorder(),
                    helperText:
                        'Tối thiểu ${_money(preview.totalAmount)}',
                  ),
                  onChanged: (value) {
                    final digits = value
                        .replaceAll(
                      RegExp(
                        r'[^0-9]',
                      ),
                      '',
                    );

                    setState(
                      () => _received =
                          int.tryParse(
                                digits,
                              ) ??
                              0,
                    );
                  },
                ),
                const SizedBox(
                  height: 10,
                ),
                _row(
                  'Tiền thối',
                  _money(_change),
                  bold: true,
                ),
              ] else if (method !=
                  null) ...[
                const SizedBox(
                  height: 14,
                ),
                _buildTransferQr(),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () {
            FocusScope.of(context)
                .unfocus();
            Navigator.of(context).pop();
          },
          child:
              const Text('Hủy'),
        ),
        FilledButton.icon(
          onPressed:
              _canConfirm
                  ? () {
                      final selected =
                          _method!;
                      FocusScope.of(
                        context,
                      ).unfocus();

                      Navigator.of(
                        context,
                      ).pop(
                        PaymentDialogResult(
                          method:
                              selected.method,
                          transactionRef:
                              selected.isCash
                                  ? null
                                  : preview
                                      .transferContent,
                        ),
                      );
                    }
                  : null,
          icon: const Icon(
            Icons
                .check_circle_outline,
          ),
          label: const Text(
            'Xác nhận thanh toán',
          ),
        ),
      ],
    );
  }
}
