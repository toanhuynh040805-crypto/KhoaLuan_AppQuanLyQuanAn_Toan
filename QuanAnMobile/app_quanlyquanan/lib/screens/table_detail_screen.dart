import 'package:flutter/material.dart';
import '../models/restaurant_table.dart';
import '../models/order_item_status.dart';
import '../theme/app_colors.dart';
import '../widgets/status_badge.dart';
import 'menu_screen.dart';

// Model tam thoi cho 1 dong mon trong don - TODO: thay bang OrderItemDto
// that lay tu GET /api/orders/{id} khi noi API day du.
class _OrderLine {
  final String name;
  final int quantity;
  final String? note;
  final OrderItemStatus status;
  _OrderLine({required this.name, required this.quantity, this.note, required this.status});
}

class TableDetailScreen extends StatefulWidget {
  final RestaurantTable table;
  const TableDetailScreen({super.key, required this.table});

  @override
  State<TableDetailScreen> createState() => _TableDetailScreenState();
}

class _TableDetailScreenState extends State<TableDetailScreen> {
  // TODO: thay du lieu mau nay bang goi that GET /api/orders/{id}
  final List<_OrderLine> _sampleLines = [
    _OrderLine(name: 'Phở Bò', quantity: 2, note: 'Bát lớn, nhiều hành', status: OrderItemStatus.hoanThanh),
    _OrderLine(name: 'Nem rán', quantity: 1, status: OrderItemStatus.dangLam),
    _OrderLine(name: 'Coca Cola', quantity: 2, note: 'Có đá', status: OrderItemStatus.daPhucVu),
  ];

  ({Color barColor, Color badgeBg, Color badgeText, IconData icon, String label}) _styleFor(OrderItemStatus s) {
    switch (s) {
      case OrderItemStatus.hoanThanh:
        return (barColor: AppColors.tertiary, badgeBg: const Color(0xFFE3F2E8), badgeText: AppColors.tertiary, icon: Icons.check_circle_outline, label: 'Đã xong');
      case OrderItemStatus.dangLam:
        return (barColor: AppColors.primaryContainer, badgeBg: AppColors.tableDangDungBg, badgeText: AppColors.primary, icon: Icons.local_fire_department_outlined, label: 'Đang làm');
      case OrderItemStatus.daPhucVu:
        return (barColor: AppColors.outline, badgeBg: AppColors.surfaceContainerHigh, badgeText: AppColors.onSurfaceVariant, icon: Icons.done_all, label: 'Đã phục vụ');
      case OrderItemStatus.choXuLy:
      case OrderItemStatus.daInPhieu:
        return (barColor: AppColors.outline, badgeBg: AppColors.surfaceContainerHigh, badgeText: AppColors.onSurfaceVariant, icon: Icons.hourglass_empty, label: 'Chờ xử lý');
    }
  }

  int get _totalItems => _sampleLines.fold(0, (sum, l) => sum + l.quantity);
  // TODO: tinh tu gia that cua tung mon, hien dang hardcode theo du lieu mau thiet ke
  final double _totalAmount = 320000;

  void _requestPayment() {
    // TODO: goi endpoint bao Thu ngan (vi du dang qua SignalR) khi co
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Đã gửi yêu cầu thanh toán tới Thu ngân')),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Bàn #${widget.table.id}')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // The tong quan don
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLowest,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFE9ECEF)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('TỔNG QUAN ĐƠN',
                          style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 0.6, color: AppColors.onSurfaceVariant)),
                      const SizedBox(height: 6),
                      Row(children: [
                        Text('$_totalItems món', style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                        const Text('  •  ', style: TextStyle(color: AppColors.onSurfaceVariant)),
                        Text('${_totalAmount.toStringAsFixed(0)} đ',
                            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16, color: AppColors.primaryContainer)),
                      ]),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(color: AppColors.tableDangDungBg, borderRadius: BorderRadius.circular(10)),
                  child: const Icon(Icons.receipt_long_outlined, color: AppColors.primaryContainer),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Danh sach mon
          ..._sampleLines.map((line) {
            final s = _styleFor(line.status);
            return Container(
              margin: const EdgeInsets.only(bottom: 12),
              decoration: BoxDecoration(
                color: AppColors.surfaceContainerLowest,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFFE9ECEF)),
                boxShadow: [BoxShadow(color: s.barColor, offset: const Offset(-4, 0), blurRadius: 0)],
              ),
              padding: const EdgeInsets.fromLTRB(16, 14, 14, 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(line.name, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                      StatusBadge(label: s.label, background: s.badgeBg, textColor: s.badgeText, icon: s.icon),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(children: [
                        const Text('SL: ', style: TextStyle(color: AppColors.onSurfaceVariant)),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
                          decoration: BoxDecoration(color: AppColors.surfaceContainerHigh, borderRadius: BorderRadius.circular(6)),
                          child: Text('${line.quantity}', style: const TextStyle(fontWeight: FontWeight.w700)),
                        ),
                      ]),
                      if (line.note != null)
                        Text(line.note!, style: const TextStyle(color: AppColors.onSurfaceVariant, fontSize: 13)),
                    ],
                  ),
                ],
              ),
            );
          }),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ElevatedButton.icon(
                onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => MenuScreen(table: widget.table))),
                icon: const Icon(Icons.add_circle_outline),
                label: const Text('Gọi thêm món'),
                style: ElevatedButton.styleFrom(minimumSize: const Size.fromHeight(50)),
              ),
              const SizedBox(height: 10),
              OutlinedButton.icon(
                onPressed: _requestPayment,
                icon: const Icon(Icons.payments_outlined),
                label: const Text('Khách gọi thanh toán'),
                style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(50)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
