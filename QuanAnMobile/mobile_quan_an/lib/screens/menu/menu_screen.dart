import 'dart:async';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/app_notification.dart';
import '../../models/cart_item.dart';
import '../../models/employee_profile.dart';
import '../../models/menu_item.dart';
import '../../models/notification_settings_state.dart';
import '../../models/restaurant_table.dart';
import '../../services/api_config.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../services/menu_service.dart';
import '../../services/mobile_access_policy.dart';
import '../../widgets/top_message.dart';
import '../cart/cart_screen.dart';
import '../notifications/notification_screen.dart';
import '../profile/profile_screen.dart';

class MenuScreen extends StatefulWidget {
  // Khi mở từ Chi tiết bàn, table đã được xác định. Khi mở từ tab
  // Thực đơn, table = null và nhân viên chọn bàn/đơn ở bước xác nhận sau cùng.
  final RestaurantTable? table;
  // true: khách đang ăn tại bàn mua thêm MANG VỀ -> mọi món chọn ở đây đều là
  // món mang về và được gộp vào hóa đơn của bàn.
  final bool takeawayOnly;
  final List<AppNotification> notifications;

  const MenuScreen({
    super.key,
    this.table,
    this.takeawayOnly = false,
    required this.notifications,
  });

  @override
  State<MenuScreen> createState() => _MenuScreenState();
}

class _MenuScreenState extends State<MenuScreen> {
  final MenuService _menuService = AppServices.menuService;
  final TextEditingController searchController = TextEditingController();
  final GlobalKey _cartTargetKey = GlobalKey();

  StreamSubscription<AppNotification>? _notificationSubscription;

  bool _isLoadingMenu = true;
  bool _hasMenuLoadError = false;
  // Không mặc định "Món chính" hay bất kỳ nhóm Mobile riêng nào.
  // Nhóm đang chọn luôn được lấy từ danh mục đang bán của backend dùng chung.
  String selectedCategory = '';

  final Map<int, int> quantities = {};
  final Map<int, String?> notes = {};
  // Số phần của mỗi món mà khách yêu cầu MANG VỀ (0..quantity)
  final Map<int, int> takeawayQuantities = {};
  final List<MenuItem> menuItems = [];
  final List<String> categories = [];

  bool get _canOrder => MobileAccessPolicy.canOrder(appEmployee.role);

  @override
  void initState() {
    super.initState();
    _loadMenuData();

    _notificationSubscription = AppServices.realtimeService.notifications
        .listen((notification) {
          if (!mounted || !_notificationEnabled(notification)) {
            return;
          }

          final int existingIndex = notification.id == null
              ? -1
              : widget.notifications.indexWhere(
                  (item) => item.id == notification.id,
                );

          setState(() {
            if (existingIndex >= 0) {
              widget.notifications[existingIndex] = notification;
            } else {
              widget.notifications.insert(0, notification);
            }
          });
        });
  }

  @override
  void dispose() {
    _notificationSubscription?.cancel();
    searchController.dispose();
    super.dispose();
  }

  Future<void> _loadMenuData() async {
    try {
      final List<MenuItem> loadedItems = await _menuService.getMenuItems();
      final List<String> loadedCategories = await _menuService.getCategories();

      if (!mounted) return;

      setState(() {
        menuItems
          ..clear()
          ..addAll(loadedItems);
        categories
          ..clear()
          ..addAll(loadedCategories);

        if (categories.isEmpty) {
          selectedCategory = '';
        } else if (!categories.contains(selectedCategory)) {
          selectedCategory = categories.first;
        }

        _hasMenuLoadError = false;
        _isLoadingMenu = false;
      });
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _hasMenuLoadError = true;
        _isLoadingMenu = false;
      });

      TopMessage.show(
        context,
        message: 'Không thể tải thực đơn',
        type: TopMessageType.error,
      );
    }
  }

  void _retryLoadMenu() {
    setState(() {
      _isLoadingMenu = true;
      _hasMenuLoadError = false;
    });
    _loadMenuData();
  }

  String normalizeVietnamese(String text) {
    return text
        .toLowerCase()
        .replaceAll(RegExp(r'[àáạảãâầấậẩẫăằắặẳẵ]'), 'a')
        .replaceAll(RegExp(r'[èéẹẻẽêềếệểễ]'), 'e')
        .replaceAll(RegExp(r'[ìíịỉĩ]'), 'i')
        .replaceAll(RegExp(r'[òóọỏõôồốộổỗơờớợởỡ]'), 'o')
        .replaceAll(RegExp(r'[ùúụủũưừứựửữ]'), 'u')
        .replaceAll(RegExp(r'[ỳýỵỷỹ]'), 'y')
        .replaceAll('đ', 'd');
  }

  List<MenuItem> get filteredItems {
    final String keyword = normalizeVietnamese(searchController.text.trim());

    if (keyword.isNotEmpty) {
      return menuItems.where((item) {
        final String normalizedName = normalizeVietnamese(item.name);
        final String normalizedDescription = normalizeVietnamese(
          item.description,
        );

        return normalizedName.contains(keyword) ||
            normalizedDescription.contains(keyword);
      }).toList();
    }

    return menuItems
        .where((item) => item.category == selectedCategory)
        .toList();
  }

  bool _notificationEnabled(AppNotification notification) {
    final NotificationSettingsState settings = appNotificationSettingsState;
    final String title = notification.title.toLowerCase();

    if (title.contains('món đã xong')) {
      return settings.kitchenFinished;
    }
    if (title.contains('thanh toán')) {
      return settings.paymentRequest;
    }
    if (notification.notificationType == 'DatBan' ||
        title.contains('đặt bàn') ||
        title.contains('bàn')) {
      return settings.tableNotification;
    }

    return true;
  }

  int get totalQuantity =>
      quantities.values.fold(0, (sum, quantity) => sum + quantity);

  int get totalPrice {
    int total = 0;
    for (final MenuItem item in menuItems) {
      total += item.price * (quantities[item.id] ?? 0);
    }
    return total;
  }

  List<CartItem> buildCartItems() {
    if (!_canOrder) return <CartItem>[];

    final List<CartItem> result = [];
    for (final MenuItem item in menuItems) {
      final int quantity = quantities[item.id] ?? 0;
      if (quantity <= 0) continue;

      result.add(
        CartItem(
          menuItem: item,
          quantity: quantity,
          note: notes[item.id],
          takeawayQuantity: widget.takeawayOnly
              ? quantity
              : (takeawayQuantities[item.id] ?? 0).clamp(0, quantity),
        ),
      );
    }
    return result;
  }

  void syncCartItems(List<CartItem> cartItems) {
    setState(() {
      quantities.clear();
      notes.clear();
      takeawayQuantities.clear();

      for (final CartItem cartItem in cartItems) {
        quantities[cartItem.menuItem.id] = cartItem.quantity;
        notes[cartItem.menuItem.id] = cartItem.note;
        if (cartItem.takeawayQuantity > 0) {
          takeawayQuantities[cartItem.menuItem.id] = cartItem.takeawayQuantity;
        }
      }
    });
  }

  void addItem(MenuItem item) {
    if (!_canOrder) return;

    setState(() {
      quantities[item.id] = (quantities[item.id] ?? 0) + 1;
    });
  }

  void removeItem(MenuItem item) {
    if (!_canOrder) return;

    final int current = quantities[item.id] ?? 0;
    if (current <= 0) return;

    setState(() {
      if (current == 1) {
        quantities.remove(item.id);
        notes.remove(item.id);
        takeawayQuantities.remove(item.id);
      } else {
        quantities[item.id] = current - 1;
        final int takeaway = takeawayQuantities[item.id] ?? 0;
        if (takeaway > current - 1) {
          takeawayQuantities[item.id] = current - 1;
        }
      }
    });
  }

  void _selectItem(MenuItem item, Offset sourcePosition) {
    if (!_canOrder) return;

    addItem(item);

    // Chờ cart bar xuất hiện ở frame mới rồi mới lấy chính xác vị trí đích.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _animateItemToCart(sourcePosition);
      }
    });
  }

  Offset _cartTargetPosition() {
    final BuildContext? targetContext = _cartTargetKey.currentContext;
    final RenderObject? renderObject = targetContext?.findRenderObject();

    if (renderObject is RenderBox && renderObject.hasSize) {
      return renderObject.localToGlobal(renderObject.size.center(Offset.zero));
    }

    final Size size = MediaQuery.sizeOf(context);
    final double bottomInset = MediaQuery.paddingOf(context).bottom;
    return Offset(
      40,
      size.height - bottomInset - kBottomNavigationBarHeight - 40,
    );
  }

  void _animateItemToCart(Offset start) {
    if (!mounted) return;

    final OverlayState overlay = Overlay.of(context);
    final Offset end = _cartTargetPosition();

    late OverlayEntry entry;
    entry = OverlayEntry(
      builder: (overlayContext) {
        return Positioned.fill(
          child: IgnorePointer(
            child: TweenAnimationBuilder<double>(
              tween: Tween<double>(begin: 0, end: 1),
              duration: const Duration(milliseconds: 520),
              curve: Curves.easeInOutCubic,
              onEnd: () {
                if (entry.mounted) {
                  entry.remove();
                }
              },
              builder: (context, value, child) {
                final double x = start.dx + ((end.dx - start.dx) * value);
                final double linearY = start.dy + ((end.dy - start.dy) * value);
                final double jump = math.sin(value * math.pi) * 72;
                final double y = linearY - jump;
                final double scale = 1 - (0.32 * value);
                final double opacity = 1 - (0.35 * value);

                return Stack(
                  children: [
                    Positioned(
                      left: x - 20,
                      top: y - 20,
                      child: Opacity(
                        opacity: opacity.clamp(0.0, 1.0),
                        child: Transform.scale(
                          scale: scale,
                          child: Container(
                            width: 40,
                            height: 40,
                            decoration: BoxDecoration(
                              color: AppTheme.orangeColor,
                              shape: BoxShape.circle,
                              boxShadow: [
                                BoxShadow(
                                  color: Colors.black.withValues(alpha: 0.18),
                                  blurRadius: 10,
                                  offset: const Offset(0, 4),
                                ),
                              ],
                            ),
                            child: const Icon(
                              Icons.restaurant,
                              color: Colors.white,
                              size: 22,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        );
      },
    );

    overlay.insert(entry);
  }

  int get unreadNotificationCount =>
      widget.notifications.where((item) => !item.isRead).length;

  Widget _notificationIcon({Color? color}) {
    final int count = unreadNotificationCount;

    return Stack(
      clipBehavior: Clip.none,
      children: [
        Icon(Icons.notifications_none, color: color),
        if (count > 0)
          Positioned(
            right: -7,
            top: -6,
            child: Container(
              constraints: const BoxConstraints(minWidth: 17, minHeight: 17),
              padding: const EdgeInsets.symmetric(horizontal: 4),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: Colors.red,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                count > 99 ? '99+' : '$count',
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 9,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
      ],
    );
  }

  String formatMoney(int amount) {
    final String value = amount.toString();
    final StringBuffer buffer = StringBuffer();

    for (int i = 0; i < value.length; i++) {
      if (i > 0 && (value.length - i) % 3 == 0) {
        buffer.write('.');
      }
      buffer.write(value[i]);
    }

    return '${buffer.toString()}đ';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8F8F8),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: false,
        leading: IconButton(
          onPressed: () => Navigator.pop(context),
          icon: const Icon(Icons.arrow_back, color: AppTheme.textColor),
        ),
        titleSpacing: 0,
        title: const Text(
          'Thực đơn',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w600,
            color: AppTheme.textColor,
          ),
        ),
        actions: [
          IconButton(
            onPressed: () => _openUtilityTab(2),
            icon: _notificationIcon(color: AppTheme.textColor),
          ),
        ],
      ),
      body: _buildBody(),
      bottomNavigationBar: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (_canOrder && totalQuantity > 0) _buildCartBar(),
          _buildBottomNavigation(),
        ],
      ),
    );
  }

  Widget _buildBody() {
    if (_isLoadingMenu) {
      return const Center(
        child: CircularProgressIndicator(color: AppTheme.orangeColor),
      );
    }

    if (_hasMenuLoadError) {
      return _buildMenuLoadError();
    }

    if (menuItems.isEmpty) {
      return _buildEmptyMenu();
    }

    return Column(
      children: [
        _buildSearch(),
        if (categories.isNotEmpty) _buildCategories(),
        Expanded(
          child: filteredItems.isEmpty
              ? const Center(child: Text('Không tìm thấy món ăn'))
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: filteredItems.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, index) =>
                      _buildMenuItem(filteredItems[index]),
                ),
        ),
      ],
    );
  }

  Widget _buildMenuLoadError() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.cloud_off_outlined,
              size: 56,
              color: Colors.grey.shade500,
            ),
            const SizedBox(height: 14),
            const Text(
              'Không thể tải thực đơn',
              style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 6),
            Text(
              'Vui lòng kiểm tra kết nối và thử lại.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: Colors.grey.shade600),
            ),
            const SizedBox(height: 18),
            OutlinedButton.icon(
              onPressed: _retryLoadMenu,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử lại'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildEmptyMenu() {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.restaurant_menu, size: 56, color: Colors.grey.shade400),
          const SizedBox(height: 14),
          const Text(
            'Chưa có món trong thực đơn',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 5),
          Text(
            'Danh sách món sẽ được cập nhật sau.',
            style: TextStyle(fontSize: 13, color: Colors.grey.shade600),
          ),
        ],
      ),
    );
  }

  Widget _buildSearch() {
    return Container(
      color: Colors.white,
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
      child: TextField(
        controller: searchController,
        onChanged: (_) => setState(() {}),
        decoration: InputDecoration(
          hintText: 'Tìm kiếm món ăn...',
          hintStyle: TextStyle(fontSize: 13, color: Colors.grey.shade500),
          prefixIcon: const Icon(Icons.search, size: 21),
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 12,
            vertical: 12,
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: BorderSide(
              color: AppTheme.orangeColor.withValues(alpha: 0.45),
            ),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: const BorderSide(
              color: AppTheme.orangeColor,
              width: 1.5,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildCategories() {
    return Container(
      color: Colors.white,
      width: double.infinity,
      padding: const EdgeInsets.only(left: 16, right: 16, bottom: 12),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: categories.map((category) {
            final bool selected = category == selectedCategory;

            return Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: Text(category),
                selected: selected,
                onSelected: (_) {
                  setState(() {
                    selectedCategory = category;
                  });
                },
                showCheckmark: false,
                selectedColor: AppTheme.orangeColor,
                backgroundColor: Colors.white,
                side: BorderSide(
                  color: selected ? AppTheme.orangeColor : Colors.grey.shade300,
                ),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20),
                ),
                labelStyle: TextStyle(
                  fontSize: 13,
                  color: selected ? Colors.white : AppTheme.textColor,
                  fontWeight: selected ? FontWeight.w600 : FontWeight.normal,
                ),
              ),
            );
          }).toList(),
        ),
      ),
    );
  }

  Widget _buildMenuItem(MenuItem item) {
    final int quantity = quantities[item.id] ?? 0;
    Offset? cardTapPosition;
    Offset? addTapPosition;

    return Material(
      key: ValueKey('menu-item-${item.id}'),
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTapDown: _canOrder
            ? (details) {
                cardTapPosition = details.globalPosition;
              }
            : null,
        onTap: _canOrder
            ? () {
                final Size size = MediaQuery.sizeOf(context);
                _selectItem(
                  item,
                  cardTapPosition ?? Offset(size.width / 2, size.height / 2),
                );
              }
            : null,
        child: Container(
          height: 112,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: quantity > 0
                  ? AppTheme.orangeColor.withValues(alpha: 0.65)
                  : Colors.grey.shade200,
              width: quantity > 0 ? 1.2 : 1,
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 102,
                height: double.infinity,
                color: Colors.white,
                child: item.imageUrl.trim().isEmpty
                    ? const _MenuImagePlaceholder()
                    : _MenuDishImage(
                        url: ApiConfig.resolveAssetUrl(item.imageUrl),
                      ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        item.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: AppTheme.textColor,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Expanded(
                        child: Text(
                          item.description,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 12,
                            height: 1.3,
                            color: Colors.grey.shade600,
                          ),
                        ),
                      ),
                      Text(
                        formatMoney(item.price),
                        style: const TextStyle(
                          fontSize: 14,
                          color: AppTheme.orangeColor,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(right: 8),
                child: !_canOrder
                    ? const SizedBox(
                        width: 40,
                        height: 40,
                        child: Icon(
                          Icons.visibility_outlined,
                          color: Colors.grey,
                        ),
                      )
                    : quantity == 0
                    ? GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onTapDown: (details) {
                          addTapPosition = details.globalPosition;
                        },
                        onTap: () {
                          final Size size = MediaQuery.sizeOf(context);
                          _selectItem(
                            item,
                            addTapPosition ??
                                Offset(size.width - 36, size.height / 2),
                          );
                        },
                        child: const SizedBox(
                          width: 46,
                          height: 64,
                          child: Icon(
                            Icons.add_circle,
                            color: AppTheme.orangeColor,
                            size: 34,
                          ),
                        ),
                      )
                    : Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          GestureDetector(
                            behavior: HitTestBehavior.opaque,
                            onTapDown: (details) {
                              addTapPosition = details.globalPosition;
                            },
                            onTap: () {
                              final Size size = MediaQuery.sizeOf(context);
                              _selectItem(
                                item,
                                addTapPosition ??
                                    Offset(size.width - 36, size.height / 2),
                              );
                            },
                            child: const SizedBox(
                              width: 40,
                              height: 36,
                              child: Icon(
                                Icons.add_circle,
                                color: AppTheme.orangeColor,
                                size: 28,
                              ),
                            ),
                          ),
                          Text(
                            '$quantity',
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 14,
                            ),
                          ),
                          GestureDetector(
                            behavior: HitTestBehavior.opaque,
                            onTap: () => removeItem(item),
                            child: const SizedBox(
                              width: 40,
                              height: 36,
                              child: Icon(
                                Icons.remove_circle_outline,
                                size: 26,
                              ),
                            ),
                          ),
                        ],
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Route<T> _tabRoute<T>(Widget page) {
    return PageRouteBuilder<T>(
      pageBuilder: (_, _, _) => page,
      transitionDuration: Duration.zero,
      reverseTransitionDuration: Duration.zero,
    );
  }

  Future<void> _openUtilityTab(int startIndex) async {
    int tabIndex = startIndex;

    while (mounted && (tabIndex == 2 || tabIndex == 3)) {
      final int? targetIndex = await Navigator.push<int>(
        context,
        _tabRoute<int>(
          tabIndex == 2
              ? NotificationScreen(
                  notifications: widget.notifications,
                  notificationService: AppServices.notificationService,
                  employeeCode: appEmployee.employeeCode,
                  onReadStateChanged: () {
                    if (mounted) setState(() {});
                  },
                )
              : ProfileScreen(notifications: widget.notifications),
        ),
      );

      if (mounted) setState(() {});
      if (!mounted || targetIndex == null) return;

      if (targetIndex == 0) {
        Navigator.pop(context);
        return;
      }
      if (targetIndex == 1) return;

      tabIndex = targetIndex;
    }
  }

  Future<RestaurantTable?> _pickOrderTarget() async {
    List<RestaurantTable> tables;
    try {
      tables = await AppServices.tableService.getTables();
    } on ApiException catch (error) {
      if (mounted) {
        TopMessage.show(
          context,
          message: error.message,
          type: TopMessageType.error,
        );
      }
      return null;
    } catch (_) {
      if (mounted) {
        TopMessage.show(
          context,
          message: 'Không thể tải danh sách bàn / đơn',
          type: TopMessageType.error,
        );
      }
      return null;
    }

    final List<RestaurantTable> options =
        tables.where((table) => table.status == TableStatus.occupied).toList()
          ..sort((a, b) {
            if (a.isTakeaway != b.isTakeaway) return a.isTakeaway ? -1 : 1;
            final String areaA = a.areaName ?? '';
            final String areaB = b.areaName ?? '';
            final int areaCompare = areaA.compareTo(areaB);
            if (areaCompare != 0) return areaCompare;
            return a.name.compareTo(b.name);
          });

    if (!mounted) return null;

    if (options.isEmpty) {
      TopMessage.show(
        context,
        message: 'Chưa có bàn / đơn đang mở. Hãy mở bàn trước khi gọi món.',
        type: TopMessageType.warning,
      );
      return null;
    }

    return showModalBottomSheet<RestaurantTable>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) {
        return SafeArea(
          child: SizedBox(
            height: MediaQuery.sizeOf(sheetContext).height * 0.68,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.fromLTRB(20, 4, 20, 12),
                  child: Text(
                    'Chọn bàn / đơn để gọi món',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                  ),
                ),
                Expanded(
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(12, 0, 12, 18),
                    itemCount: options.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (_, index) {
                      final RestaurantTable table = options[index];
                      return ListTile(
                        leading: Icon(
                          table.isTakeaway
                              ? Icons.shopping_bag_outlined
                              : Icons.table_restaurant_outlined,
                        ),
                        title: Text(
                          table.name,
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        subtitle: Text(
                          table.isTakeaway
                              ? 'Mang về'
                              : '${table.areaName ?? 'Tại quán'}${table.canOperate ? '' : ' • Ngoài phân công'}',
                        ),
                        trailing: !table.canOperate
                            ? const Icon(
                                Icons.info_outline,
                                color: Color(0xFFD97706),
                              )
                            : const Icon(Icons.chevron_right),
                        onTap: () => Navigator.pop(sheetContext, table),
                      );
                    },
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _submitStandaloneOrder(List<CartItem> cartItems) async {
    final RestaurantTable? target = await _pickOrderTarget();
    if (!mounted || target == null) return;

    if (!target.canOperate) {
      TopMessage.show(
        context,
        message:
            '${target.areaName ?? 'Khu vực này'} không nằm trong phân công của bạn. Bạn vẫn có thể gọi món; hệ thống sẽ lưu lịch sử.',
        type: TopMessageType.warning,
        duration: const Duration(milliseconds: 2600),
      );
    }

    try {
      await AppServices.orderService.addItems(
        tableId: target.id,
        employeeCode: appEmployee.employeeCode,
        items: cartItems,
      );

      if (!mounted) return;
      setState(() {
        quantities.clear();
        notes.clear();
        takeawayQuantities.clear();
      });

      TopMessage.show(
        context,
        message: 'Đã gọi món vào ${target.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } catch (_) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: 'Không thể gọi món. Vui lòng thử lại.',
        type: TopMessageType.error,
      );
    }
  }

  Widget _buildCartBar() {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: Colors.grey.shade200)),
      ),
      child: Row(
        children: [
          Stack(
            clipBehavior: Clip.none,
            children: [
              Container(
                key: _cartTargetKey,
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: AppTheme.orangeColor,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(
                  Icons.shopping_cart_outlined,
                  color: Colors.white,
                  size: 25,
                ),
              ),
              Positioned(
                top: -6,
                right: -6,
                child: Container(
                  constraints: const BoxConstraints(
                    minWidth: 20,
                    minHeight: 20,
                  ),
                  padding: const EdgeInsets.symmetric(horizontal: 5),
                  decoration: const BoxDecoration(
                    color: Colors.red,
                    shape: BoxShape.circle,
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    '$totalQuantity',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 10,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Đã chọn $totalQuantity món',
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  formatMoney(totalPrice),
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    color: AppTheme.orangeColor,
                  ),
                ),
              ],
            ),
          ),
          ElevatedButton.icon(
            onPressed: () async {
              final List<CartItem> cartItems = buildCartItems();
              if (cartItems.isEmpty) return;

              final List<CartItem>?
              result = await Navigator.push<List<CartItem>>(
                context,
                MaterialPageRoute(
                  builder: (context) => CartScreen(
                    tableName: widget.table?.name,
                    // Đơn mang về: cả đơn đã là mang về nên không cần chọn từng món
                    showTakeawayToggle:
                        !(widget.table?.isTakeaway ?? false) &&
                        !widget.takeawayOnly,
                    cartItems: cartItems,
                  ),
                ),
              );

              if (!mounted) return;

              if (result == null) {
                // CartItem là object mutable; đồng bộ lại cả trường hợp người
                // dùng sửa số lượng/ghi chú rồi bấm Back.
                syncCartItems(cartItems);
                return;
              }

              if (result.isNotEmpty) {
                syncCartItems(result);

                if (widget.table != null) {
                  // Khôi phục luồng Mobile cũ: xác nhận giỏ hàng phải trả danh
                  // sách món về TableDetailV2 để _addItems() lưu vào backend.
                  if (widget.takeawayOnly) {
                    // Giỏ có thể đổi số lượng -> luôn ép toàn bộ là mang về.
                    for (final CartItem cartItem in result) {
                      cartItem.takeawayQuantity = cartItem.quantity;
                    }
                  }
                  Navigator.pop(context, result);
                } else {
                  // Mở từ tab Thực đơn: chọn món trước rồi chọn bàn đang mở.
                  await _submitStandaloneOrder(result);
                }
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: AppTheme.orangeColor,
              foregroundColor: Colors.white,
              elevation: 0,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
            icon: const Icon(Icons.shopping_cart_checkout, size: 18),
            label: const Text(
              'Xem giỏ hàng',
              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomNavigation() {
    return BottomNavigationBar(
      currentIndex: 1,
      type: BottomNavigationBarType.fixed,
      selectedItemColor: AppTheme.orangeColor,
      unselectedItemColor: Colors.grey,
      backgroundColor: Colors.white,
      selectedFontSize: 12,
      unselectedFontSize: 12,
      onTap: (index) {
        if (index == 0) {
          Navigator.pop(context);
          return;
        }
        if (index == 1) return;
        _openUtilityTab(index);
      },
      items: const [
        BottomNavigationBarItem(
          icon: Icon(Icons.grid_view_rounded),
          label: 'Sơ đồ',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.restaurant_menu),
          label: 'Thực đơn',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.notifications_none),
          label: 'Thông báo',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.person_outline),
          label: 'Cá nhân',
        ),
      ],
    );
  }
}

class _MenuDishImage extends StatefulWidget {
  final String url;

  const _MenuDishImage({required this.url});

  @override
  State<_MenuDishImage> createState() => _MenuDishImageState();
}

class _MenuDishImageState extends State<_MenuDishImage> {
  ImageStream? _stream;
  ImageStreamListener? _listener;
  bool? _isAbnormallyBlack;
  bool _started = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_started) {
      _started = true;
      _inspectImage();
    }
  }

  @override
  void didUpdateWidget(covariant _MenuDishImage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.url != widget.url) {
      _detach();
      _isAbnormallyBlack = null;
      _started = true;
      _inspectImage();
    }
  }

  void _inspectImage() {
    final ImageProvider provider = NetworkImage(widget.url);
    final ImageStream stream = provider.resolve(
      createLocalImageConfiguration(context),
    );

    late final ImageStreamListener listener;
    listener = ImageStreamListener(
      (ImageInfo info, bool synchronousCall) async {
        try {
          final data = await info.image.toByteData(
            format: ui.ImageByteFormat.rawRgba,
          );
          if (data == null || !mounted) return;

          final bytes = data.buffer.asUint8List();
          final int pixelCount = bytes.length ~/ 4;
          if (pixelCount <= 0) return;

          final int step = pixelCount > 12000 ? pixelCount ~/ 12000 : 1;
          int sampled = 0;
          int nearBlack = 0;
          int colored = 0;

          for (int pixel = 0; pixel < pixelCount; pixel += step) {
            final int offset = pixel * 4;
            final int r = bytes[offset];
            final int g = bytes[offset + 1];
            final int b = bytes[offset + 2];
            final int a = bytes[offset + 3];
            if (a < 16) continue;

            sampled++;
            if (r <= 18 && g <= 18 && b <= 18) {
              nearBlack++;
            }
            if ((r - g).abs() > 12 ||
                (g - b).abs() > 12 ||
                (r - b).abs() > 12) {
              colored++;
            }
          }

          if (sampled == 0 || !mounted) return;
          final double blackRatio = nearBlack / sampled;
          final double coloredRatio = colored / sampled;

          setState(() {
            _isAbnormallyBlack = blackRatio >= 0.78 && coloredRatio <= 0.03;
          });
        } catch (_) {
          if (mounted) setState(() => _isAbnormallyBlack = true);
        }
      },
      onError: (error, stackTrace) {
        if (mounted) setState(() => _isAbnormallyBlack = true);
      },
    );

    _stream = stream;
    _listener = listener;
    stream.addListener(listener);
  }

  void _detach() {
    final ImageStream? stream = _stream;
    final ImageStreamListener? listener = _listener;
    if (stream != null && listener != null) {
      stream.removeListener(listener);
    }
    _stream = null;
    _listener = null;
  }

  @override
  void dispose() {
    _detach();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_isAbnormallyBlack == true) {
      return const _MenuImagePlaceholder();
    }

    return Image.network(
      widget.url,
      width: 102,
      height: double.infinity,
      fit: BoxFit.contain,
      cacheWidth: 420,
      loadingBuilder: (context, child, progress) {
        if (progress == null) return child;
        return const _MenuImagePlaceholder(showProgress: true);
      },
      errorBuilder: (context, error, stackTrace) =>
          const _MenuImagePlaceholder(),
    );
  }
}

class _MenuImagePlaceholder extends StatelessWidget {
  final bool showProgress;

  const _MenuImagePlaceholder({this.showProgress = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      color: const Color(0xFFFFF7F2),
      alignment: Alignment.center,
      child: showProgress
          ? const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: AppTheme.orangeColor,
              ),
            )
          : const Icon(
              Icons.restaurant_menu_rounded,
              size: 38,
              color: AppTheme.orangeColor,
            ),
    );
  }
}
