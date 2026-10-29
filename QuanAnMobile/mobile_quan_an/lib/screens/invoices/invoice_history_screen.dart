import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/mobile_access_policy.dart';
import '../../widgets/top_message.dart';
import 'receipt_preview_screen.dart';

class InvoiceHistoryScreen
    extends StatefulWidget {
  const InvoiceHistoryScreen({
    super.key,
  });

  @override
  State<InvoiceHistoryScreen>
      createState() =>
          _InvoiceHistoryScreenState();
}

class _InvoiceHistoryScreenState
    extends State<InvoiceHistoryScreen> {
  DateTime _selectedDate =
      DateTime.now();
  List<Map<String, dynamic>> _items =
      <Map<String, dynamic>>[];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  int _asInt(dynamic value) {
    if (value is int) return value;
    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }

  String _money(dynamic value) {
    final String raw =
        _asInt(value).toString();
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

  String _dateLabel(DateTime date) {
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }

  DateTime? _dateTime(dynamic value) {
    return DateTime.tryParse(
      value?.toString() ?? '',
    );
  }

  String _time(dynamic value) {
    final DateTime? parsed =
        _dateTime(value);

    if (parsed == null) {
      return '--:--';
    }

    return '${parsed.hour.toString().padLeft(2, '0')}:'
        '${parsed.minute.toString().padLeft(2, '0')}';
  }

  String _paymentMethodLabel(
    dynamic value,
  ) {
    switch (_asInt(value)) {
      case 0:
        return 'Tiền mặt';
      case 1:
        return 'Thẻ ngân hàng';
      case 2:
        return 'Ví điện tử';
      case 3:
        return 'Chuyển khoản / QR';
      default:
        return 'Khác';
    }
  }

  Future<void> _load() async {
    if (mounted) {
      setState(
        () => _loading = true,
      );
    }

    try {
      final result =
          await AppServices
              .referenceOpsService
              .getInvoices(
        date: _selectedDate,
      );

      if (!mounted) return;

      setState(() {
        _items = result;
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;

      setState(
        () => _loading = false,
      );

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  Future<void> _pickDate() async {
    final DateTime? value =
        await showDatePicker(
      context: context,
      initialDate: _selectedDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(
        const Duration(days: 1),
      ),
    );

    if (!mounted || value == null) {
      return;
    }

    setState(
      () => _selectedDate = value,
    );

    await _load();
  }

  Future<void> _showReceipt(
    Map<String, dynamic> detail,
    List<dynamic> rawItems,
    List<dynamic> payments,
  ) async {
    final List<ReceiptLineView> lines =
        rawItems.map((raw) {
      final Map<String, dynamic> item =
          raw is Map
              ? Map<String, dynamic>.from(
                  raw,
                )
              : <String, dynamic>{};

      return ReceiptLineView(
        name:
            item['name']?.toString() ??
            'Món',
        quantity:
            _asInt(item['quantity']),
        unitPrice:
            _asInt(item['unitPrice']),
        lineTotal:
            _asInt(item['lineTotal']),
      );
    }).toList();

    DateTime? paymentTime;
    if (payments.isNotEmpty &&
        payments.first is Map) {
      final Map<String, dynamic> first =
          Map<String, dynamic>.from(
        payments.first as Map,
      );

      paymentTime = _dateTime(
        first['paymentTime'],
      );
    }

    await Navigator.of(context).push<void>(
      MaterialPageRoute(
        builder: (_) =>
            ReceiptPreviewScreen(
          title:
              'HÓA ĐƠN THANH TOÁN',
          referenceId:
              _asInt(detail['id']),
          tableName:
              detail['tableName']
                      ?.toString() ??
                  '',
          waiterName:
              detail['waiterName']
                      ?.toString() ??
                  '',
          cashierName:
              detail['cashierName']
                      ?.toString() ??
                  '',
          orderTime:
              _dateTime(
            detail['orderTime'],
          ),
          paymentTime:
              paymentTime ??
              _dateTime(
                detail['invoiceDate'],
              ),
          items: lines,
          subTotal:
              _asInt(
            detail['subTotal'],
          ),
          discountAmount:
              _asInt(
            detail['discountAmount'],
          ),
          totalAmount:
              _asInt(
            detail['totalAmount'],
          ),
          shopName:
              detail['shopName']
                      ?.toString() ??
                  'Quán Ăn GustoPOS',
          shopAddress:
              detail['shopAddress']
                      ?.toString() ??
                  '',
          bankName:
              detail['bankName']
                      ?.toString() ??
                  '',
          bankAccountNo:
              detail['bankAccountNo']
                      ?.toString() ??
                  '',
          bankAccountName:
              detail['bankAccountName']
                      ?.toString() ??
                  '',
          bankBranch:
              detail['bankBranch']
                      ?.toString() ??
                  '',
          transferContent:
              detail['transferContent']
                      ?.toString() ??
                  '',
          qrImageUrl:
              detail['qrImageUrl']
                      ?.toString() ??
                  '',
        ),
      ),
    );
  }

  Future<void> _openDetail(
    Map<String, dynamic> invoice,
  ) async {
    final int id =
        _asInt(invoice['id']);

    if (id <= 0) return;

    try {
      final Map<String, dynamic> detail =
          await AppServices
              .referenceOpsService
              .getInvoiceDetail(id);

      if (!mounted) return;

      final List<dynamic> rawItems =
          detail['items'] is List
              ? List<dynamic>.from(
                  detail['items'] as List,
                )
              : <dynamic>[];

      final List<dynamic> payments =
          detail['payments'] is List
              ? List<dynamic>.from(
                  detail['payments']
                      as List,
                )
              : <dynamic>[];

      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (sheetContext) =>
            FractionallySizedBox(
          heightFactor: 0.84,
          child: Padding(
            padding:
                const EdgeInsets
                    .fromLTRB(
              18,
              4,
              18,
              20,
            ),
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment
                      .start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Hóa đơn #$id • '
                        '${detail['tableName'] ?? ''}',
                        style:
                            const TextStyle(
                          fontSize: 20,
                          fontWeight:
                              FontWeight
                                  .bold,
                        ),
                      ),
                    ),
                    if (MobileAccessPolicy
                        .canCompletePayment(''))
                      IconButton(
                        tooltip:
                            'Xem / in bill',
                        onPressed: () =>
                            _showReceipt(
                          detail,
                          rawItems,
                          payments,
                        ),
                        icon: const Icon(
                          Icons
                              .print_outlined,
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  'Phục vụ: '
                  '${detail['waiterName'] ?? '-'}',
                  style: TextStyle(
                    color: Colors
                        .grey.shade700,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  'Thu ngân: '
                  '${detail['cashierName'] ?? '-'}',
                  style: TextStyle(
                    color: Colors
                        .grey.shade700,
                  ),
                ),
                const Divider(height: 24),
                Expanded(
                  child:
                      ListView.separated(
                    itemCount:
                        rawItems.length,
                    separatorBuilder:
                        (_, _) =>
                            const Divider(
                      height: 18,
                    ),
                    itemBuilder:
                        (
                          context,
                          index,
                        ) {
                      final raw =
                          rawItems[index];

                      final item =
                          raw is Map
                              ? Map<
                                  String,
                                  dynamic>.from(
                                  raw,
                                )
                              : <String,
                                  dynamic>{};

                      return Row(
                        children: [
                          Expanded(
                            child: Text(
                              '${item['name'] ?? 'Món'}  '
                              'x${_asInt(item['quantity'])}',
                              style:
                                  const TextStyle(
                                fontWeight:
                                    FontWeight
                                        .w600,
                              ),
                            ),
                          ),
                          Text(
                            _money(
                              item[
                                  'lineTotal'],
                            ),
                          ),
                        ],
                      );
                    },
                  ),
                ),
                const Divider(height: 20),
                Row(
                  mainAxisAlignment:
                      MainAxisAlignment
                          .spaceBetween,
                  children: [
                    const Text(
                      'Tổng thanh toán',
                    ),
                    Text(
                      _money(
                        detail[
                            'totalAmount'],
                      ),
                      style:
                          const TextStyle(
                        fontSize: 20,
                        fontWeight:
                            FontWeight.bold,
                        color: AppTheme
                            .orangeColor,
                      ),
                    ),
                  ],
                ),
                if (payments
                    .isNotEmpty) ...[
                  const SizedBox(
                    height: 8,
                  ),
                  Text(
                    'Thanh toán: '
                    '${payments.map((p) {
                      if (p is! Map) {
                        return '';
                      }
                      return _paymentMethodLabel(
                        p['method'],
                      );
                    }).where((e) => e.isNotEmpty).join(', ')}',
                    style: TextStyle(
                      color: Colors
                          .grey.shade700,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  @override
  Widget build(
    BuildContext context,
  ) {
    return Scaffold(
      backgroundColor:
          const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Hóa đơn đã thanh toán',
          style: TextStyle(
            fontWeight: FontWeight.bold,
          ),
        ),
        actions: [
          TextButton.icon(
            onPressed: _pickDate,
            icon: const Icon(
              Icons
                  .calendar_month_outlined,
            ),
            label: Text(
              _dateLabel(
                _selectedDate,
              ),
            ),
          ),
        ],
      ),
      body:
          _loading
              ? const Center(
                  child:
                      CircularProgressIndicator(),
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child:
                      _items.isEmpty
                          ? ListView(
                              physics:
                                  const AlwaysScrollableScrollPhysics(),
                              children:
                                  const [
                                SizedBox(
                                  height:
                                      180,
                                ),
                                Icon(
                                  Icons
                                      .receipt_long_outlined,
                                  size: 54,
                                  color:
                                      Colors.grey,
                                ),
                                SizedBox(
                                  height:
                                      10,
                                ),
                                Center(
                                  child: Text(
                                    'Ngày này chưa có hóa đơn',
                                  ),
                                ),
                              ],
                            )
                          : ListView
                              .separated(
                              padding:
                                  const EdgeInsets
                                      .all(
                                14,
                              ),
                              itemCount:
                                  _items
                                      .length,
                              separatorBuilder:
                                  (_, _) =>
                                      const SizedBox(
                                height:
                                    10,
                              ),
                              itemBuilder:
                                  (
                                    context,
                                    index,
                                  ) {
                                final item =
                                    _items[
                                        index];

                                final waiter =
                                    item['waiterName']
                                            ?.toString() ??
                                        '';

                                final cashier =
                                    item['cashierName']
                                            ?.toString() ??
                                        '';

                                return InkWell(
                                  onTap: () =>
                                      _openDetail(
                                    item,
                                  ),
                                  borderRadius:
                                      BorderRadius
                                          .circular(
                                    14,
                                  ),
                                  child:
                                      Container(
                                    padding:
                                        const EdgeInsets
                                            .all(
                                      15,
                                    ),
                                    decoration:
                                        BoxDecoration(
                                      color:
                                          Colors.white,
                                      borderRadius:
                                          BorderRadius
                                              .circular(
                                        14,
                                      ),
                                      border:
                                          Border.all(
                                        color: Colors
                                            .grey
                                            .shade200,
                                      ),
                                    ),
                                    child:
                                        Row(
                                      children: [
                                        Container(
                                          width:
                                              46,
                                          height:
                                              46,
                                          decoration:
                                              BoxDecoration(
                                            color:
                                                const Color(
                                              0xFFFFEFE6,
                                            ),
                                            borderRadius:
                                                BorderRadius.circular(
                                              12,
                                            ),
                                          ),
                                          child:
                                              const Icon(
                                            Icons
                                                .receipt_long_outlined,
                                            color:
                                                AppTheme.orangeColor,
                                          ),
                                        ),
                                        const SizedBox(
                                          width:
                                              12,
                                        ),
                                        Expanded(
                                          child:
                                              Column(
                                            crossAxisAlignment:
                                                CrossAxisAlignment.start,
                                            children: [
                                              Text(
                                                '#${item['id']} • '
                                                '${item['tableName'] ?? 'Bàn'}',
                                                style:
                                                    const TextStyle(
                                                  fontWeight: FontWeight.bold,
                                                ),
                                              ),
                                              const SizedBox(
                                                height:
                                                    4,
                                              ),
                                              Text(
                                                '${_time(item['invoiceDate'])}'
                                                '${waiter.isNotEmpty ? ' • PV: $waiter' : ''}',
                                                maxLines:
                                                    1,
                                                overflow:
                                                    TextOverflow.ellipsis,
                                                style:
                                                    TextStyle(
                                                  color: Colors.grey.shade600,
                                                  fontSize: 12,
                                                ),
                                              ),
                                              if (cashier
                                                  .isNotEmpty)
                                                Text(
                                                  'Thu ngân: $cashier',
                                                  maxLines:
                                                      1,
                                                  overflow:
                                                      TextOverflow.ellipsis,
                                                  style:
                                                      TextStyle(
                                                    color: Colors.grey.shade600,
                                                    fontSize: 11,
                                                  ),
                                                ),
                                            ],
                                          ),
                                        ),
                                        Text(
                                          _money(
                                            item[
                                                'totalAmount'],
                                          ),
                                          style:
                                              const TextStyle(
                                            fontWeight:
                                                FontWeight.bold,
                                            color:
                                                AppTheme.orangeColor,
                                          ),
                                        ),
                                        const SizedBox(
                                          width:
                                              4,
                                        ),
                                        const Icon(
                                          Icons
                                              .chevron_right,
                                        ),
                                      ],
                                    ),
                                  ),
                                );
                              },
                            ),
                ),
    );
  }
}
