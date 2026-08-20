import 'package:flutter/material.dart';
import '../models/restaurant_table.dart';
import '../services/api_service.dart';
import '../theme/app_colors.dart';
import '../widgets/table_card.dart';
import 'table_detail_screen.dart';
import 'menu_screen.dart';
import 'notifications_screen.dart';

enum _TableFilter { tatCa, trong, dangDung, daDat }

class TableMapScreen extends StatefulWidget {
  final VoidCallback? onOpenMenuTab;
  const TableMapScreen({super.key, this.onOpenMenuTab});

  @override
  State<TableMapScreen> createState() => _TableMapScreenState();
}

class _TableMapScreenState extends State<TableMapScreen> {
  final ApiService _api = ApiService();
  late Future<List<RestaurantTable>> _tablesFuture;
  _TableFilter _filter = _TableFilter.tatCa;

  @override
  void initState() {
    super.initState();
    _loadTables();
  }

  void _loadTables() => _tablesFuture = _api.getTables();

  Future<void> _refresh() async {
    setState(_loadTables);
    await _tablesFuture;
  }

  List<RestaurantTable> _applyFilter(List<RestaurantTable> tables) {
    switch (_filter) {
      case _TableFilter.tatCa:
        return tables;
      case _TableFilter.trong:
        return tables.where((t) => t.status == TableStatus.trong).toList();
      case _TableFilter.dangDung:
        return tables.where((t) => t.status == TableStatus.dangDung).toList();
      case _TableFilter.daDat:
        return tables.where((t) => t.status == TableStatus.daDat).toList();
    }
  }

  void _openTable(RestaurantTable table) {
    if (table.status == TableStatus.trong) {
      // Ban trong -> vao thang man Thuc don de goi mon lan dau
      Navigator.push(context, MaterialPageRoute(builder: (_) => MenuScreen(table: table)));
    } else {
      // Ban dang co khach -> xem chi tiet don truoc
      Navigator.push(context, MaterialPageRoute(builder: (_) => TableDetailScreen(table: table)));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Sơ đồ bàn'),
        centerTitle: false,
        automaticallyImplyLeading: false,
        actions: [
          IconButton(
            icon: const Icon(Icons.notifications_none_rounded),
            onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const NotificationsScreen())),
          ),
        ],
      ),
      body: Column(
        children: [
          // Hang bo loc dang chip cuon ngang
          SizedBox(
            height: 48,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              children: [
                _filterChip('Tất cả', _TableFilter.tatCa),
                const SizedBox(width: 8),
                _filterChip('Trống', _TableFilter.trong),
                const SizedBox(width: 8),
                _filterChip('Đang có khách', _TableFilter.dangDung),
                const SizedBox(width: 8),
                _filterChip('Đã đặt', _TableFilter.daDat),
              ],
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _refresh,
              child: FutureBuilder<List<RestaurantTable>>(
                future: _tablesFuture,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return Center(child: Text('Lỗi: ${snapshot.error}'));
                  }
                  final tables = _applyFilter(snapshot.data ?? []);
                  if (tables.isEmpty) {
                    return const Center(child: Text('Không có bàn nào'));
                  }
                  return GridView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                    gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                      crossAxisCount: 2,
                      crossAxisSpacing: 12,
                      mainAxisSpacing: 12,
                      childAspectRatio: 1.35,
                    ),
                    itemCount: tables.length,
                    itemBuilder: (context, index) {
                      final table = tables[index];
                      return TableCard(table: table, onTap: () => _openTable(table));
                    },
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _filterChip(String label, _TableFilter value) {
    final isSelected = _filter == value;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (_) => setState(() => _filter = value),
      selectedColor: AppColors.primaryContainer,
      backgroundColor: AppColors.surfaceContainerLowest,
      side: BorderSide(color: isSelected ? Colors.transparent : AppColors.outlineVariant),
      labelStyle: TextStyle(
        color: isSelected ? Colors.white : AppColors.onSurfaceVariant,
        fontWeight: FontWeight.w600,
      ),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
    );
  }
}
