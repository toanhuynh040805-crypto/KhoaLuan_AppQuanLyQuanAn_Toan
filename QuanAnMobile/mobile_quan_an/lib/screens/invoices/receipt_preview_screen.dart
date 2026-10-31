import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../widgets/api_qr_image.dart';

class ReceiptLineView {
  final String name;
  final int quantity;
  final int unitPrice;
  final int lineTotal;

  const ReceiptLineView({
    required this.name,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
}

class ReceiptPreviewScreen
    extends StatelessWidget {
  final String title;
  final int referenceId;
  final String tableName;
  final String waiterName;
  final String cashierName;
  final DateTime? orderTime;
  final DateTime? paymentTime;
  final List<ReceiptLineView> items;
  final int subTotal;
  final int discountAmount;
  final int totalAmount;

  final String shopName;
  final String shopAddress;
  final String bankName;
  final String bankAccountNo;
  final String bankAccountName;
  final String bankBranch;
  final String transferContent;
  final String qrImageUrl;

  const ReceiptPreviewScreen({
    super.key,
    required this.title,
    required this.referenceId,
    required this.tableName,
    required this.waiterName,
    this.cashierName = '',
    this.orderTime,
    this.paymentTime,
    required this.items,
    required this.subTotal,
    this.discountAmount = 0,
    required this.totalAmount,
    required this.shopName,
    required this.shopAddress,
    required this.bankName,
    required this.bankAccountNo,
    required this.bankAccountName,
    required this.bankBranch,
    required this.transferContent,
    required this.qrImageUrl,
  });

  String _money(int value) {
    final String raw = value.toString();
    final StringBuffer out =
        StringBuffer();

    for (int i = 0;
        i < raw.length;
        i++) {
      if (i > 0 &&
          (raw.length - i) % 3 ==
              0) {
        out.write('.');
      }
      out.write(raw[i]);
    }

    return '${out.toString()}đ';
  }

  String _date(DateTime? value) {
    if (value == null) return '-';

    return '${value.day.toString().padLeft(2, '0')}/'
        '${value.month.toString().padLeft(2, '0')}/'
        '${value.year}';
  }

  String _time(DateTime? value) {
    if (value == null) return '-';

    return '${value.hour.toString().padLeft(2, '0')}:'
        '${value.minute.toString().padLeft(2, '0')}';
  }

  Widget _metaRow(
    String left,
    String right,
  ) {
    return Row(
      crossAxisAlignment:
          CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            left,
            style: const TextStyle(
              fontSize: 12,
            ),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            right,
            textAlign: TextAlign.right,
            style: const TextStyle(
              fontSize: 12,
            ),
          ),
        ),
      ],
    );
  }

  @override
  Widget build(
    BuildContext context,
  ) {
    final DateTime displayedPaymentTime =
        paymentTime ?? DateTime.now();

    return Scaffold(
      backgroundColor:
          const Color(0xFFECECEC),
      appBar: AppBar(
        backgroundColor: Colors.white,
        foregroundColor:
            AppTheme.textColor,
        title: const Text(
          'Xem trước bill',
          style: TextStyle(
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding:
                const EdgeInsets.all(16),
            child: Container(
              width: 390,
              padding:
                  const EdgeInsets.fromLTRB(
                18,
                22,
                18,
                26,
              ),
              decoration:
                  const BoxDecoration(
                color: Colors.white,
              ),
              child: Column(
                crossAxisAlignment:
                    CrossAxisAlignment
                        .stretch,
                children: [
                  Text(
                    shopName,
                    textAlign:
                        TextAlign.center,
                    style:
                        const TextStyle(
                      fontSize: 19,
                      fontWeight:
                          FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    shopAddress,
                    textAlign:
                        TextAlign.center,
                    style: const TextStyle(
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text(
                    title,
                    textAlign:
                        TextAlign.center,
                    style:
                        const TextStyle(
                      fontSize: 21,
                      fontWeight:
                          FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    title == 'PHIẾU TẠM TÍNH'
                        ? 'Mã Order: #$referenceId'
                        : 'Số HĐ: #$referenceId',
                    textAlign:
                        TextAlign.center,
                    style:
                        const TextStyle(
                      fontWeight:
                          FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 14),
                  _metaRow(
                    'Bàn: $tableName',
                    'Ngày: ${_date(displayedPaymentTime)}',
                  ),
                  const SizedBox(height: 4),
                  _metaRow(
                    'Giờ vào: ${_time(orderTime)}',
                    'Giờ ra: ${_time(paymentTime)}',
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Phục vụ: '
                    '${waiterName.isEmpty ? '-' : waiterName}',
                    style:
                        const TextStyle(
                      fontSize: 12,
                    ),
                  ),
                  if (cashierName
                      .isNotEmpty) ...[
                    const SizedBox(
                      height: 4,
                    ),
                    Text(
                      'Thu ngân: $cashierName',
                      style:
                          const TextStyle(
                        fontSize: 12,
                      ),
                    ),
                  ],
                  const SizedBox(height: 12),
                  const Divider(
                    thickness: 1,
                    height: 1,
                  ),
                  const SizedBox(height: 8),
                  const Row(
                    children: [
                      SizedBox(
                        width: 28,
                        child: Text(
                          'STT',
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight:
                                FontWeight
                                    .w700,
                          ),
                        ),
                      ),
                      Expanded(
                        flex: 5,
                        child: Text(
                          'Tên món',
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight:
                                FontWeight
                                    .w700,
                          ),
                        ),
                      ),
                      SizedBox(
                        width: 28,
                        child: Text(
                          'SL',
                          textAlign:
                              TextAlign
                                  .center,
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight:
                                FontWeight
                                    .w700,
                          ),
                        ),
                      ),
                      SizedBox(
                        width: 68,
                        child: Text(
                          'Đơn giá',
                          textAlign:
                              TextAlign
                                  .right,
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight:
                                FontWeight
                                    .w700,
                          ),
                        ),
                      ),
                      SizedBox(
                        width: 72,
                        child: Text(
                          'T.Tiền',
                          textAlign:
                              TextAlign
                                  .right,
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight:
                                FontWeight
                                    .w700,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ...items.asMap().entries.map(
                    (entry) {
                      final item =
                          entry.value;
                      return Padding(
                        padding:
                            const EdgeInsets
                                .symmetric(
                          vertical: 4,
                        ),
                        child: Row(
                          crossAxisAlignment:
                              CrossAxisAlignment
                                  .start,
                          children: [
                            SizedBox(
                              width: 28,
                              child: Text(
                                '${entry.key + 1}',
                                style:
                                    const TextStyle(
                                  fontSize: 10,
                                ),
                              ),
                            ),
                            Expanded(
                              flex: 5,
                              child: Text(
                                item.name,
                                style:
                                    const TextStyle(
                                  fontSize: 10,
                                  fontWeight:
                                      FontWeight
                                          .w600,
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 28,
                              child: Text(
                                '${item.quantity}',
                                textAlign:
                                    TextAlign
                                        .center,
                                style:
                                    const TextStyle(
                                  fontSize: 10,
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 68,
                              child: Text(
                                _money(
                                  item.unitPrice,
                                ),
                                textAlign:
                                    TextAlign
                                        .right,
                                style:
                                    const TextStyle(
                                  fontSize: 10,
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 72,
                              child: Text(
                                _money(
                                  item.lineTotal,
                                ),
                                textAlign:
                                    TextAlign
                                        .right,
                                style:
                                    const TextStyle(
                                  fontSize: 10,
                                ),
                              ),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                  const SizedBox(height: 8),
                  const Divider(
                    thickness: 1,
                    height: 1,
                  ),
                  const SizedBox(height: 10),
                  Row(
                    mainAxisAlignment:
                        MainAxisAlignment
                            .spaceBetween,
                    children: [
                      Text(
                        'Tổng số món: '
                        '${items.fold<int>(0, (sum, item) => sum + item.quantity)}',
                        style:
                            const TextStyle(
                          fontSize: 12,
                        ),
                      ),
                      Text(
                        'Tạm tính: '
                        '${_money(subTotal)}',
                        style:
                            const TextStyle(
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                  if (discountAmount >
                      0) ...[
                    const SizedBox(
                      height: 6,
                    ),
                    Row(
                      mainAxisAlignment:
                          MainAxisAlignment
                              .spaceBetween,
                      children: [
                        const Text(
                          'Giảm giá',
                          style:
                              TextStyle(
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          '-${_money(discountAmount)}',
                          style:
                              const TextStyle(
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ],
                  const SizedBox(height: 9),
                  Row(
                    mainAxisAlignment:
                        MainAxisAlignment
                            .spaceBetween,
                    children: [
                      const Text(
                        'TỔNG TIỀN',
                        style:
                            TextStyle(
                          fontSize: 16,
                          fontWeight:
                              FontWeight
                                  .w900,
                        ),
                      ),
                      Text(
                        _money(totalAmount),
                        style:
                            const TextStyle(
                          fontSize: 17,
                          fontWeight:
                              FontWeight
                                  .w900,
                        ),
                      ),
                    ],
                  ),
                  if (qrImageUrl
                      .isNotEmpty) ...[
                    const SizedBox(
                      height: 18,
                    ),
                    Center(
                      child: SizedBox(
                        width: 220,
                        height: 220,
                        child: ApiQrImage(
                          imageUrl: qrImageUrl,
                          fit: BoxFit.contain,
                        ),
                      ),
                    ),
                    const SizedBox(
                      height: 8,
                    ),
                    Text(
                      bankAccountName,
                      textAlign:
                          TextAlign.center,
                      style:
                          const TextStyle(
                        fontWeight:
                            FontWeight.w800,
                      ),
                    ),
                    const SizedBox(
                      height: 2,
                    ),
                    Text(
                      '$bankName - '
                      '$bankAccountNo',
                      textAlign:
                          TextAlign.center,
                      style:
                          const TextStyle(
                        fontSize: 12,
                      ),
                    ),
                    if (bankBranch
                        .isNotEmpty)
                      Text(
                        bankBranch,
                        textAlign:
                            TextAlign.center,
                        style:
                            const TextStyle(
                          fontSize: 11,
                        ),
                      ),
                    if (transferContent
                        .isNotEmpty)
                      Padding(
                        padding:
                            const EdgeInsets
                                .only(
                          top: 4,
                        ),
                        child: Text(
                          'Nội dung: '
                          '$transferContent',
                          textAlign:
                              TextAlign.center,
                          style:
                              const TextStyle(
                            fontSize: 11,
                          ),
                        ),
                      ),
                  ],
                  const SizedBox(height: 20),
                  const Text(
                    'Cảm ơn Quý Khách!',
                    textAlign:
                        TextAlign.center,
                    style: TextStyle(
                      fontStyle:
                          FontStyle.italic,
                      fontWeight:
                          FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
