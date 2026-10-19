import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/employee_profile.dart';
import '../../models/order_item.dart';
import '../../models/restaurant_table.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class KitchenRequestsScreen extends StatefulWidget {
  final RestaurantTable table;
  final List<OrderItem> initialItems;

  const KitchenRequestsScreen({
    super.key,
    required this.table,
    required this.initialItems,
  });

  @override
  State<KitchenRequestsScreen> createState() =>
      _KitchenRequestsScreenState();
}

class _KitchenRequestsScreenState extends State<KitchenRequestsScreen> {
  late List<OrderItem> _items;
  final Set<int> _selectedCancelIds = <int>{};
  final Set<int> _priorityItemIds = <int>{};
  bool _tablePriority = false;
  bool _loading = true;
  bool _canceling = false;
  final Set<int> _savingPriorityIds = <int>{};
  bool _savingTablePriority = false;

  @override
  void initState() {
    super.initState();
    _items = List<OrderItem>.from(widget.initialItems);
    _loadPriorities();
  }

  List<OrderItem> get _cancelableItems => _items
      .where(
        (item) =>
            item.id != null && item.status == 'Chờ chế biến',
      )
      .toList();

  List<OrderItem> get _priorityItems => _items
      .where(
        (item) =>
            item.id != null &&
            (item.status == 'Chờ chế biến' ||
                item.status == 'Đang chế biến'),
      )
      .toList();

  Future<void> _loadPriorities() async {
    try {
      final Map<String, Set<int>> result =
          await AppServices.referenceOpsService.getKitchenPriorities();
      if (!mounted) return;
      setState(() {
        _priorityItemIds
          ..clear()
          ..addAll(result['itemIds'] ?? <int>{});
        _tablePriority =
            (result['tableIds'] ?? <int>{}).contains(widget.table.id);
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loading = false);
    }
  }

  Future<void> _cancelSelected() async {
    if (_selectedCancelIds.isEmpty || _canceling) return;

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Bỏ món'),
        content: Text(
          'Gửi yêu cầu bỏ ${_selectedCancelIds.length} món xuống bếp? '
          'Chỉ món chưa bắt đầu chế biến mới có thể bỏ.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Không'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            child: const Text('Bỏ món'),
          ),
        ],
      ),
    );

    if (!mounted || confirmed != true) return;

    setState(() => _canceling = true);
    try {
      final Set<int> requested = Set<int>.from(_selectedCancelIds);
      final Set<int> updated = await AppServices.orderService.cancelItems(
        orderItemIds: requested.toList(),
      );

      if (!mounted) return;
      setState(() {
        _items.removeWhere(
          (item) => item.id != null && updated.contains(item.id),
        );
        _selectedCancelIds.removeAll(updated);
        _priorityItemIds.removeAll(updated);
      });

      if (updated.isEmpty) {
        TopMessage.show(
          context,
          message: 'Các món đã được bếp bắt đầu nên không thể bỏ.',
          type: TopMessageType.warning,
        );
      } else {
        TopMessage.show(
          context,
          message: 'Đã gửi bỏ ${updated.length} món xuống bếp',
          type: TopMessageType.success,
        );
      }
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) setState(() => _canceling = false);
    }
  }

  Future<void> _toggleItemPriority(
    OrderItem item,
    bool active,
  ) async {
    final int? id = item.id;
    if (id == null || _savingPriorityIds.contains(id)) return;

    setState(() => _savingPriorityIds.add(id));
    try {
      await AppServices.referenceOpsService.setItemPriority(
        orderItemId: id,
        employeeCode: appEmployee.employeeCode,
        active: active,
      );
      if (!mounted) return;
      setState(() {
        if (active) {
          _priorityItemIds.add(id);
        } else {
          _priorityItemIds.remove(id);
        }
      });
      TopMessage.show(
        context,
        message: active
            ? 'Đã ưu tiên món ${item.name}'
            : 'Đã bỏ ưu tiên món ${item.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) {
        setState(() => _savingPriorityIds.remove(id));
      }
    }
  }

  Future<void> _toggleTablePriority(bool active) async {
    if (_savingTablePriority) return;
    setState(() => _savingTablePriority = true);

    try {
      await AppServices.referenceOpsService.setTablePriority(
        tableId: widget.table.id,
        employeeCode: appEmployee.employeeCode,
        active: active,
      );
      if (!mounted) return;
      setState(() => _tablePriority = active);
      TopMessage.show(
        context,
        message: active
            ? 'Đã ưu tiên toàn bộ ${widget.table.name} xuống bếp'
            : 'Đã bỏ ưu tiên ${widget.table.name}',
        type: TopMessageType.success,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    } finally {
      if (mounted) setState(() => _savingTablePriority = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 3,
      child: Scaffold(
        backgroundColor: const Color(0xFFF6F7F9),
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Yêu cầu xuống bếp',
                style: TextStyle(
                  color: AppTheme.textColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 18,
                ),
              ),
              Text(
                widget.table.name,
                style: TextStyle(
                  color: Colors.grey.shade600,
                  fontSize: 12,
                ),
              ),
            ],
          ),
          bottom: const TabBar(
            labelColor: AppTheme.orangeColor,
            unselectedLabelColor: Colors.grey,
            indicatorColor: AppTheme.orangeColor,
            tabs: [
              Tab(text: 'Bỏ món'),
              Tab(text: 'Ưu tiên món'),
              Tab(text: 'Ưu tiên bàn'),
            ],
          ),
        ),
        body: _loading
            ? const Center(
                child: CircularProgressIndicator(
                  color: AppTheme.orangeColor,
                ),
              )
            : TabBarView(
                children: [
                  _buildCancelTab(),
                  _buildItemPriorityTab(),
                  _buildTablePriorityTab(),
                ],
              ),
      ),
    );
  }

  Widget _buildCancelTab() {
    final List<OrderItem> items = _cancelableItems;
    if (items.isEmpty) {
      return _emptyState(
        Icons.remove_shopping_cart_outlined,
        'Không có món có thể bỏ',
        'Món đã bắt đầu chế biến không thể hủy tại đây.',
      );
    }

    return Column(
      children: [
        Expanded(
          child: ListView.separated(
            padding: const EdgeInsets.all(14),
            itemCount: items.length,
            separatorBuilder: (_, _) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final OrderItem item = items[index];
              final int id = item.id!;
              final bool selected = _selectedCancelIds.contains(id);
              return Card(
                margin: EdgeInsets.zero,
                child: CheckboxListTile(
                  value: selected,
                  activeColor: Colors.red,
                  title: Text(
                    item.name,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  subtitle: Text(
                    'SL: ${item.quantity}'
                    '${(item.note ?? '').trim().isEmpty ? '' : ' • ${item.note}'}',
                  ),
                  onChanged: _canceling
                      ? null
                      : (value) {
                          setState(() {
                            if (value == true) {
                              _selectedCancelIds.add(id);
                            } else {
                              _selectedCancelIds.remove(id);
                            }
                          });
                        },
                ),
              );
            },
          ),
        ),
        SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(14, 8, 14, 14),
            child: SizedBox(
              width: double.infinity,
              height: 48,
              child: FilledButton.icon(
                onPressed: _selectedCancelIds.isEmpty || _canceling
                    ? null
                    : _cancelSelected,
                style: FilledButton.styleFrom(backgroundColor: Colors.red),
                icon: _canceling
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.delete_outline),
                label: Text(
                  'Bỏ món (${_selectedCancelIds.length})',
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildItemPriorityTab() {
    final List<OrderItem> items = _priorityItems;
    if (items.isEmpty) {
      return _emptyState(
        Icons.flag_outlined,
        'Không có món cần ưu tiên',
        'Chỉ món đang chờ hoặc đang chế biến mới có thể ưu tiên.',
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(14),
      itemCount: items.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final OrderItem item = items[index];
        final int id = item.id!;
        final bool active = _priorityItemIds.contains(id);
        final bool saving = _savingPriorityIds.contains(id);
        return Card(
          margin: EdgeInsets.zero,
          child: SwitchListTile(
            value: active,
            activeThumbColor: Colors.redAccent,
            title: Text(
              item.name,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            subtitle: Text('SL: ${item.quantity} • ${item.status}'),
            secondary: saving
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Icon(
                    active ? Icons.flag : Icons.flag_outlined,
                    color: active ? Colors.redAccent : Colors.grey,
                  ),
            onChanged: saving
                ? null
                : (value) => _toggleItemPriority(item, value),
          ),
        );
      },
    );
  }

  Widget _buildTablePriorityTab() {
    final bool hasKitchenItems = _priorityItems.isNotEmpty;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: Colors.grey.shade200),
          ),
          child: Column(
            children: [
              Icon(
                _tablePriority ? Icons.flag : Icons.flag_outlined,
                size: 46,
                color: _tablePriority ? Colors.redAccent : Colors.grey,
              ),
              const SizedBox(height: 12),
              Text(
                'Ưu tiên ${widget.table.name}',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'Khi bật, các món chưa hoàn thành của bàn này được đưa lên đầu danh sách của Bếp.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.grey.shade700),
              ),
              const SizedBox(height: 18),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: FilledButton.icon(
                  onPressed: !hasKitchenItems || _savingTablePriority
                      ? null
                      : () => _toggleTablePriority(!_tablePriority),
                  style: FilledButton.styleFrom(
                    backgroundColor: _tablePriority
                        ? Colors.grey.shade700
                        : Colors.redAccent,
                  ),
                  icon: _savingTablePriority
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : Icon(
                          _tablePriority
                              ? Icons.flag_outlined
                              : Icons.flag,
                        ),
                  label: Text(
                    _tablePriority ? 'Bỏ ưu tiên bàn' : 'Ưu tiên bàn',
                  ),
                ),
              ),
              if (!hasKitchenItems) ...[
                const SizedBox(height: 12),
                Text(
                  'Bàn hiện không còn món đang chờ bếp xử lý.',
                  style: TextStyle(
                    color: Colors.grey.shade600,
                    fontSize: 12,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }

  Widget _emptyState(
    IconData icon,
    String title,
    String subtitle,
  ) {
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [
        const SizedBox(height: 150),
        Icon(icon, size: 56, color: Colors.grey.shade400),
        const SizedBox(height: 12),
        Center(
          child: Text(
            title,
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 16,
            ),
          ),
        ),
        const SizedBox(height: 6),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 30),
          child: Text(
            subtitle,
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.grey.shade600),
          ),
        ),
      ],
    );
  }
}
