import 'package:flutter/material.dart';
import '../../app/theme.dart';
import '../../models/cart_item.dart';
import '../../widgets/top_message.dart';
import '../../services/app_services.dart';

class CartScreen extends StatefulWidget {
  final String? tableName;
  final List<CartItem> cartItems;

  // Cho phép đánh dấu từng phần của món là "mang về" (dùng khi khách ăn tại bàn).
  // Đặt false với đơn mang về (cả đơn đã là mang về).
  final bool showTakeawayToggle;

  CartScreen({
    super.key,
    this.tableName,
    this.showTakeawayToggle = true,
    List<CartItem>? cartItems,
    List<CartItem>? items,
  })  : assert(cartItems != null || items != null),
        cartItems = cartItems ?? items!;

  @override
  State<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends State<CartScreen> {
  final List<String> _recentNotes = <String>[];
  bool _loadingRecentNotes = true;

  @override
  void initState() {
    super.initState();
    _loadRecentNotes();
  }

  Future<void> _loadRecentNotes() async {
    try {
      final List<String> notes =
          await AppServices.referenceOpsService.getRecentNotes();
      if (!mounted) return;
      setState(() {
        _recentNotes
          ..clear()
          ..addAll(notes);
        _loadingRecentNotes = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _recentNotes
          ..clear()
          ..addAll(const <String>[
            'Ít cay',
            'Không hành',
            'Không đá',
            'Để riêng',
          ]);
        _loadingRecentNotes = false;
      });
    }
  }

  void _applyRecentNote(CartItem item, String note) {
    setState(() {
      item.note = note;
    });
  }

  String formatPrice(num price) {
    final value = price.toInt().toString();
    final result = StringBuffer();

    for (int i = 0; i < value.length; i++) {
      if (i > 0 && (value.length - i) % 3 == 0) {
        result.write('.');
      }
      result.write(value[i]);
    }

    return '$resultđ';
  }

  num get totalPrice {
    return widget.cartItems.fold(
      0,
      (sum, item) => sum + item.totalPrice,
    );
  }

  int get totalQuantity {
    return widget.cartItems.fold(
      0,
      (sum, item) => sum + item.quantity,
    );
  }

  void increaseQuantity(CartItem item) {
    setState(() {
      item.quantity++;
    });
  }

  int get totalTakeaway {
    return widget.cartItems.fold(
      0,
      (sum, item) => sum + item.takeawayQuantity,
    );
  }

  void increaseTakeaway(CartItem item) {
    if (item.takeawayQuantity >= item.quantity) return;
    setState(() {
      item.takeawayQuantity++;
    });
  }

  void decreaseTakeaway(CartItem item) {
    if (item.takeawayQuantity <= 0) return;
    setState(() {
      item.takeawayQuantity--;
    });
  }

  void decreaseQuantity(CartItem item) {
    setState(() {
      if (item.quantity > 1) {
        item.quantity--;
        item.clampTakeaway();
      } else {
        widget.cartItems.remove(item);
      }
    });
  }

  void removeItem(CartItem item) {
    setState(() {
      widget.cartItems.remove(item);
    });
  }

  void confirmOrder() {
    if (widget.cartItems.isEmpty) {
      TopMessage.show(
        context,
        message: 'Giỏ hàng đang trống',
        type: TopMessageType.warning,
      );
      return;
    }

    // Nút "Xác nhận gọi món" đã là hành động xác nhận cuối cùng.
    // Không mở thêm AlertDialog xác nhận lần hai.
    Navigator.pop(
      context,
      List<CartItem>.from(widget.cartItems),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: Text(
          (widget.tableName?.trim().isNotEmpty ?? false)
              ? 'Giỏ hàng - ${widget.tableName}'
              : 'Giỏ hàng',
          style: const TextStyle(
            fontWeight: FontWeight.bold,
            color: AppTheme.textColor,
          ),
        ),
      ),

      body: widget.cartItems.isEmpty
          ? const Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    Icons.shopping_cart_outlined,
                    size: 70,
                    color: Colors.grey,
                  ),
                  SizedBox(height: 16),
                  Text(
                    'Giỏ hàng đang trống',
                    style: TextStyle(
                      fontSize: 17,
                      color: Colors.grey,
                    ),
                  ),
                ],
              ),
            )
          : ListView.separated(
              padding: const EdgeInsets.all(18),
              itemCount: widget.cartItems.length,
              separatorBuilder: (_, _) =>
                const SizedBox(height: 12),
              itemBuilder: (context, index) {
                final item = widget.cartItems[index];

                return Container(
                  key: ValueKey(
                    'cart-item-${item.menuItem.id}',
                  ),
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(
                      color: Colors.grey.shade200,
                    ),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 70,
                            height: 70,
                            decoration: BoxDecoration(
                              color: const Color(0xFFFFF0E8),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Icon(
                              Icons.restaurant,
                              color: AppTheme.orangeColor,
                              size: 34,
                            ),
                          ),

                          const SizedBox(height: 14, width: 14),

                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  item.menuItem.name,
                                  style: const TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),

                                const SizedBox(height: 6),

                                Text(
                                  formatPrice(item.menuItem.price),
                                  style: const TextStyle(
                                    color: AppTheme.orangeColor,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),

                                const SizedBox(height: 10),

                                Row(
                                  children: [
                                    InkWell(
                                      onTap: () => decreaseQuantity(item),
                                      child: const Icon(
                                        Icons.remove_circle_outline,
                                        size: 26,
                                      ),
                                    ),

                                    Padding(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 12,
                                      ),
                                      child: Text(
                                        '${item.quantity}',
                                        style: const TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),

                                    InkWell(
                                      onTap: () => increaseQuantity(item),
                                      child: const Icon(
                                        Icons.add_circle,
                                        color: AppTheme.orangeColor,
                                        size: 27,
                                      ),
                                    ),
                                  ],
                                ),

                                if (widget.showTakeawayToggle) ...[
                                  const SizedBox(height: 10),
                                  Row(
                                    children: [
                                      const Icon(
                                        Icons.shopping_bag_outlined,
                                        size: 20,
                                        color: AppTheme.orangeColor,
                                      ),
                                      const SizedBox(width: 6),
                                      const Text(
                                        'Mang về',
                                        style: TextStyle(
                                          fontSize: 13,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                      const SizedBox(width: 10),
                                      InkWell(
                                        onTap: item.takeawayQuantity > 0
                                            ? () => decreaseTakeaway(item)
                                            : null,
                                        child: Icon(
                                          Icons.remove_circle_outline,
                                          size: 24,
                                          color: item.takeawayQuantity > 0
                                              ? null
                                              : Colors.grey.shade300,
                                        ),
                                      ),
                                      Padding(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 8,
                                        ),
                                        child: Text(
                                          '${item.takeawayQuantity}/${item.quantity}',
                                          style: const TextStyle(
                                            fontSize: 14,
                                            fontWeight: FontWeight.w600,
                                          ),
                                        ),
                                      ),
                                      InkWell(
                                        onTap: item.takeawayQuantity <
                                                item.quantity
                                            ? () => increaseTakeaway(item)
                                            : null,
                                        child: Icon(
                                          Icons.add_circle_outline,
                                          size: 24,
                                          color: item.takeawayQuantity <
                                                  item.quantity
                                              ? AppTheme.orangeColor
                                              : Colors.grey.shade300,
                                        ),
                                      ),
                                    ],
                                  ),
                                ],
                              ],
                            ),
                          ),

                          IconButton(
                            onPressed: () => removeItem(item),
                            icon: const Icon(
                              Icons.delete_outline,
                              color: Colors.grey,
                            ),
                          ),
                        ],
                      ),

                      const SizedBox(height: 12),

                      TextFormField(
                        key: ValueKey(
                          'cart-note-${item.menuItem.id}-${item.note ?? ''}',
                        ),
                        initialValue: item.note ?? '',
                        maxLines: 2,
                        minLines: 1,

                        decoration: InputDecoration(
                          hintText: 'Ghi chú món (VD: ít cay, không hành...)',
                          hintStyle: TextStyle(
                            fontSize: 13,
                            color: Colors.grey.shade500,
                          ),

                          prefixIcon: const Icon(
                            Icons.edit_note,
                            color: AppTheme.orangeColor,
                          ),

                          filled: true,
                          fillColor: const Color(0xFFFAFAFA),

                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 10,
                          ),

                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide: BorderSide(
                              color: Colors.grey.shade300,
                            ),
                          ),

                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide: BorderSide(
                              color: Colors.grey.shade300,
                            ),
                          ),

                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide: const BorderSide(
                              color: AppTheme.orangeColor,
                            ),
                          ),
                        ),

                        onChanged: (value) {
                          item.note =
                              value.trim().isEmpty ? null : value.trim();
                        },
                      ),

                      if (_loadingRecentNotes || _recentNotes.isNotEmpty) ...[
                        const SizedBox(height: 10),
                        Text(
                          'Ghi chú gần đây',
                          style: TextStyle(
                            color: Colors.grey.shade700,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 6),
                        if (_loadingRecentNotes)
                          const LinearProgressIndicator(minHeight: 2)
                        else
                          Wrap(
                            spacing: 7,
                            runSpacing: 6,
                            children: _recentNotes.take(6).map((note) {
                              return ActionChip(
                                label: Text(note),
                                visualDensity: VisualDensity.compact,
                                onPressed: () => _applyRecentNote(item, note),
                              );
                            }).toList(),
                          ),
                      ],
                    ],
                  ),
                );
              },
            ),

      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(
            18,
            12,
            18,
            14,
          ),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(
              top: BorderSide(
                color: Color(0xFFEEEEEE),
              ),
            ),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                mainAxisAlignment:
                    MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    totalTakeaway > 0
                        ? 'Tổng ($totalQuantity món, $totalTakeaway mang về)'
                        : 'Tổng ($totalQuantity món)',
                    style: const TextStyle(
                      fontSize: 15,
                    ),
                  ),
                  Text(
                    formatPrice(totalPrice),
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                      color: AppTheme.orangeColor,
                    ),
                  ),
                ],
              ),

              const SizedBox(height: 12),

              SizedBox(
                width: double.infinity,
                height: 54,
                child: ElevatedButton(
                  onPressed:
                      widget.cartItems.isEmpty ? null : confirmOrder,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppTheme.primaryColor,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  child: const Text(
                    'Xác nhận gọi món',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}