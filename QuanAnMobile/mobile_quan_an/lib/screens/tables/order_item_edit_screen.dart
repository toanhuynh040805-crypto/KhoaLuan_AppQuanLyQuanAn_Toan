import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/order_item.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class OrderItemEditScreen extends StatefulWidget {
  final OrderItem item;

  const OrderItemEditScreen({
    super.key,
    required this.item,
  });

  @override
  State<OrderItemEditScreen> createState() =>
      _OrderItemEditScreenState();
}

class _OrderItemEditScreenState
    extends State<OrderItemEditScreen> {
  late int _quantity;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _quantity = widget.item.quantity;
  }

  String _formatMoney(int amount) {
    final String value = amount.toString();
    final StringBuffer buffer = StringBuffer();

    for (int i = 0; i < value.length; i++) {
      if (i > 0 &&
          (value.length - i) % 3 == 0) {
        buffer.write('.');
      }
      buffer.write(value[i]);
    }

    return '${buffer.toString()}đ';
  }

  Future<void> _save() async {
    if (_saving ||
        _quantity == widget.item.quantity ||
        widget.item.id == null) {
      if (_quantity == widget.item.quantity) {
        Navigator.pop(context, _quantity);
      }
      return;
    }

    setState(() {
      _saving = true;
    });

    try {
      final int saved =
          await AppServices.orderService
              .updateItemQuantity(
        orderItemId: widget.item.id!,
        quantity: _quantity,
      );

      if (!mounted) return;

      Navigator.pop(context, saved);
    } on ApiException catch (error) {
      if (!mounted) return;

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() {
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final int total =
        widget.item.unitPrice * _quantity;

    return Scaffold(
      backgroundColor:
          const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        foregroundColor:
            AppTheme.textColor,
        title: Text(
          '#${widget.item.id ?? ''}: '
          '${widget.item.name.toUpperCase()}',
          maxLines: 1,
          overflow:
              TextOverflow.ellipsis,
          style: const TextStyle(
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding:
                    const EdgeInsets.all(
                  16,
                ),
                children: [
                  Container(
                    padding:
                        const EdgeInsets.all(
                      18,
                    ),
                    decoration:
                        BoxDecoration(
                      color: Colors.white,
                      borderRadius:
                          BorderRadius.circular(
                        16,
                      ),
                    ),
                    child: Column(
                      crossAxisAlignment:
                          CrossAxisAlignment
                              .start,
                      children: [
                        Row(
                          children: [
                            Container(
                              width: 68,
                              height: 68,
                              decoration:
                                  BoxDecoration(
                                color: AppTheme
                                    .orangeColor
                                    .withValues(
                                  alpha: 0.10,
                                ),
                                borderRadius:
                                    BorderRadius
                                        .circular(
                                  12,
                                ),
                              ),
                              child: const Icon(
                                Icons
                                    .restaurant_menu,
                                color: AppTheme
                                    .orangeColor,
                                size: 30,
                              ),
                            ),
                            const SizedBox(
                              width: 14,
                            ),
                            Expanded(
                              child: Column(
                                crossAxisAlignment:
                                    CrossAxisAlignment
                                        .start,
                                children: [
                                  Text(
                                    widget
                                        .item
                                        .name,
                                    style:
                                        const TextStyle(
                                      fontSize:
                                          20,
                                      fontWeight:
                                          FontWeight
                                              .w800,
                                    ),
                                  ),
                                  const SizedBox(
                                    height: 6,
                                  ),
                                  Text(
                                    _formatMoney(
                                      widget
                                          .item
                                          .unitPrice,
                                    ),
                                    style:
                                        TextStyle(
                                      color: Colors
                                          .grey
                                          .shade700,
                                      fontSize:
                                          14,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(
                          height: 28,
                        ),
                        Row(
                          children: [
                            const Expanded(
                              child: Text(
                                'Số lượng',
                                style:
                                    TextStyle(
                                  fontSize: 17,
                                  fontWeight:
                                      FontWeight
                                          .w600,
                                ),
                              ),
                            ),
                            Container(
                              height: 54,
                              decoration:
                                  BoxDecoration(
                                border:
                                    Border.all(
                                  color: Colors
                                      .grey
                                      .shade300,
                                ),
                                borderRadius:
                                    BorderRadius
                                        .circular(
                                  12,
                                ),
                              ),
                              child: Row(
                                children: [
                                  IconButton(
                                    onPressed:
                                        _saving ||
                                                _quantity <=
                                                    1
                                            ? null
                                            : () {
                                                setState(
                                                  () {
                                                    _quantity--;
                                                  },
                                                );
                                              },
                                    icon:
                                        const Icon(
                                      Icons
                                          .remove,
                                    ),
                                  ),
                                  SizedBox(
                                    width: 48,
                                    child:
                                        Text(
                                      '$_quantity',
                                      textAlign:
                                          TextAlign
                                              .center,
                                      style:
                                          const TextStyle(
                                        fontSize:
                                            20,
                                        fontWeight:
                                            FontWeight
                                                .w700,
                                      ),
                                    ),
                                  ),
                                  IconButton(
                                    onPressed:
                                        _saving ||
                                                _quantity >=
                                                    99
                                            ? null
                                            : () {
                                                setState(
                                                  () {
                                                    _quantity++;
                                                  },
                                                );
                                              },
                                    icon:
                                        const Icon(
                                      Icons.add,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(
                          height: 24,
                        ),
                        Divider(
                          color: Colors
                              .grey.shade200,
                        ),
                        const SizedBox(
                          height: 12,
                        ),
                        Row(
                          children: [
                            Text(
                              'Thành tiền',
                              style:
                                  TextStyle(
                                color: Colors
                                    .grey
                                    .shade700,
                                fontSize: 14,
                              ),
                            ),
                            const Spacer(),
                            Text(
                              _formatMoney(
                                total,
                              ),
                              style:
                                  const TextStyle(
                                color: AppTheme
                                    .orangeColor,
                                fontSize: 19,
                                fontWeight:
                                    FontWeight
                                        .w800,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  if ((widget.item.note ??
                          '')
                      .trim()
                      .isNotEmpty) ...[
                    const SizedBox(
                      height: 14,
                    ),
                    Container(
                      padding:
                          const EdgeInsets
                              .all(16),
                      decoration:
                          BoxDecoration(
                        color: Colors.white,
                        borderRadius:
                            BorderRadius
                                .circular(
                          16,
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment:
                            CrossAxisAlignment
                                .start,
                        children: [
                          const Text(
                            'Ghi chú',
                            style:
                                TextStyle(
                              fontSize: 16,
                              fontWeight:
                                  FontWeight
                                      .w700,
                            ),
                          ),
                          const SizedBox(
                            height: 8,
                          ),
                          Text(
                            widget.item
                                .note!,
                            style:
                                TextStyle(
                              color: Colors
                                  .grey
                                  .shade700,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ),
            Container(
              padding:
                  const EdgeInsets.fromLTRB(
                16,
                12,
                16,
                16,
              ),
              decoration:
                  BoxDecoration(
                color: Colors.white,
                border: Border(
                  top: BorderSide(
                    color: Colors
                        .grey.shade200,
                  ),
                ),
              ),
              child: SafeArea(
                top: false,
                child: SizedBox(
                  width:
                      double.infinity,
                  height: 50,
                  child:
                      FilledButton(
                    onPressed:
                        _saving
                            ? null
                            : _save,
                    style:
                        FilledButton
                            .styleFrom(
                      backgroundColor:
                          AppTheme
                              .orangeColor,
                    ),
                    child: _saving
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child:
                                CircularProgressIndicator(
                              strokeWidth:
                                  2,
                              color:
                                  Colors.white,
                            ),
                          )
                        : const Text(
                            'Lưu',
                            style:
                                TextStyle(
                              fontSize: 17,
                              fontWeight:
                                  FontWeight
                                      .w700,
                            ),
                          ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
