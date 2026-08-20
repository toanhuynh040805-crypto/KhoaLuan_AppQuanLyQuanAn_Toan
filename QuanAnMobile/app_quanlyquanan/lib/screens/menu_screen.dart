import 'package:flutter/material.dart';
import '../models/restaurant_table.dart';
import '../models/menu_item.dart';
import '../models/cart_item.dart';
import '../services/api_service.dart';
import '../theme/app_colors.dart';
import 'notifications_screen.dart';

// table = null khi mo tu tab "Thuc don" duoi cung (xem menu chung, chua gan ban)
// table != null khi mo tu 1 ban cu the (goi mon cho dung ban do)
class MenuScreen extends StatefulWidget {
  final RestaurantTable? table;
  const MenuScreen({super.key, this.table});

  @override
  State<MenuScreen> createState() => _MenuScreenState();
}

class _MenuScreenState extends State<MenuScreen> {
  final ApiService _api = ApiService();
  late Future<List<MenuItem>> _menuFuture;
  final TextEditingController _searchController = TextEditingController();

  String _searchQuery = '';
  int? _selectedCategoryId; // null = tat ca

  final Map<int, CartItem> _cart = {};

  @override
  void initState() {
    super.initState();
    _menuFuture = _api.getMenu();
  }

  void _addToCart(MenuItem item) {
    setState(() {
      if (_cart.containsKey(item.id)) {
        _cart[item.id]!.quantity++;
      } else {
        _cart[item.id] = CartItem(menuItem: item);
      }
    });
  }

  double get _cartTotal => _cart.values.fold(0, (sum, c) => sum + c.subtotal);
  int get _cartCount => _cart.values.fold(0, (sum, c) => sum + c.quantity);

  Future<void> _submitOrder() async {
    if (_cart.isEmpty || widget.table == null) return;
    try {
      // TODO: thay 1 bang userId that cua nhan vien dang dang nhap
      await _api.submitOrder(tableId: widget.table!.id, userId: 1, items: _cart.values.toList());
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã gửi order xuống bếp/quầy nước')),
      );
      Navigator.pop(context);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Lỗi: $e')));
    }
  }

  List<MenuItem> _applyFilters(List<MenuItem> items) {
    return items.where((item) {
      final matchCategory = _selectedCategoryId == null || item.categoryId == _selectedCategoryId;
      final matchSearch = _searchQuery.isEmpty || item.name.toLowerCase().contains(_searchQuery.toLowerCase());
      return matchCategory && matchSearch;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final isEmbedded = widget.table == null; // true khi la 1 trong 4 tab duoi

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.table != null ? 'Gọi món - Bàn #${widget.table!.id}' : 'Thực đơn'),
        automaticallyImplyLeading: !isEmbedded,
        actions: isEmbedded
            ? [
                IconButton(
                  icon: const Icon(Icons.notifications_none_rounded),
                  onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const NotificationsScreen())),
                ),
              ]
            : null,
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchController,
              onChanged: (v) => setState(() => _searchQuery = v),
              decoration: const InputDecoration(
                hintText: 'Tìm kiếm món ăn...',
                prefixIcon: Icon(Icons.search, color: AppColors.onSurfaceVariant),
              ),
            ),
          ),
          SizedBox(
            height: 44,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              children: [
                _categoryChip('Tất cả', null),
                const SizedBox(width: 8),
                _categoryChip('Món chính', 1),
                const SizedBox(width: 8),
                _categoryChip('Khai vị', 2),
                const SizedBox(width: 8),
                _categoryChip('Đồ uống', 3),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Expanded(
            child: FutureBuilder<List<MenuItem>>(
              future: _menuFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator());
                }
                if (snapshot.hasError) {
                  return Center(child: Text('Lỗi: ${snapshot.error}'));
                }
                final items = _applyFilters(snapshot.data ?? []);
                if (items.isEmpty) {
                  return const Center(child: Text('Không tìm thấy món phù hợp'));
                }
                return ListView.separated(
                  padding: EdgeInsets.fromLTRB(16, 4, 16, widget.table != null ? 90 : 16),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (context, index) => _menuItemCard(items[index]),
                );
              },
            ),
          ),
        ],
      ),
      // Thanh gio hang noi phia duoi - chi hien khi da co mon trong gio VA dang goi cho 1 ban cu the
      bottomNavigationBar: (widget.table != null && _cart.isNotEmpty)
          ? SafeArea(
              child: Container(
                margin: const EdgeInsets.all(16),
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerLowest,
                  borderRadius: BorderRadius.circular(14),
                  boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.08), blurRadius: 16, offset: const Offset(0, 4))],
                ),
                child: Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(color: AppColors.primaryContainer, borderRadius: BorderRadius.circular(10)),
                      child: Stack(clipBehavior: Clip.none, children: [
                        const Icon(Icons.shopping_cart_outlined, color: Colors.white),
                        Positioned(
                          right: -6, top: -6,
                          child: CircleAvatar(radius: 8, backgroundColor: AppColors.error,
                              child: Text('$_cartCount', style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.w700))),
                        ),
                      ]),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Đã chọn $_cartCount món', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                          Text('${_cartTotal.toStringAsFixed(0)} đ',
                              style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.primary)),
                        ],
                      ),
                    ),
                    ElevatedButton(
                      onPressed: _submitOrder,
                      style: ElevatedButton.styleFrom(minimumSize: const Size(120, 44)),
                      child: const Text('Gửi order'),
                    ),
                  ],
                ),
              ),
            )
          : null,
    );
  }

  Widget _categoryChip(String label, int? categoryId) {
    final isSelected = _selectedCategoryId == categoryId;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (_) => setState(() => _selectedCategoryId = categoryId),
      selectedColor: AppColors.primaryContainer,
      backgroundColor: AppColors.surfaceContainerLowest,
      side: BorderSide(color: isSelected ? Colors.transparent : AppColors.outlineVariant),
      labelStyle: TextStyle(color: isSelected ? Colors.white : AppColors.onSurfaceVariant, fontWeight: FontWeight.w600),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
    );
  }

  Widget _menuItemCard(MenuItem item) {
    final qty = _cart[item.id]?.quantity ?? 0;
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE9ECEF)),
      ),
      padding: const EdgeInsets.all(10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: item.imageUrl != null
                ? Image.network(item.imageUrl!, width: 76, height: 76, fit: BoxFit.cover)
                : Container(width: 76, height: 76, color: AppColors.surfaceContainerHigh,
                    child: const Icon(Icons.restaurant, color: AppColors.onSurfaceVariant)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.name, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(height: 6),
                Text('${item.price.toStringAsFixed(0)} đ',
                    style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700)),
              ],
            ),
          ),
          if (widget.table != null)
            GestureDetector(
              onTap: () => _addToCart(item),
              child: Container(
                width: 32, height: 32,
                decoration: BoxDecoration(color: AppColors.primaryContainer, shape: BoxShape.circle),
                child: Icon(qty > 0 ? Icons.check : Icons.add, color: Colors.white, size: 18),
              ),
            ),
        ],
      ),
    );
  }
}
