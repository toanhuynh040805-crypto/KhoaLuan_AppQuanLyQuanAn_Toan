import 'package:flutter/material.dart';
import '../models/restaurant_table.dart';
import '../theme/app_colors.dart';
import 'status_badge.dart';

// The 1 ban trong luoi So do ban - khop chinh xac voi thiet ke Stitch:
// vien mau theo trang thai, badge trang thai goc phai, vien do doc canh trai
// khi dang "cho mon".
class TableCard extends StatelessWidget {
  final RestaurantTable table;
  final VoidCallback onTap;

  const TableCard({super.key, required this.table, required this.onTap});

  ({Color bg, Color badgeBg, Color badgeText, String label}) get _statusStyle {
    switch (table.status) {
      case TableStatus.trong:
        return (
          bg: AppColors.tableTrongBg,
          badgeBg: AppColors.tableTrongBg,
          badgeText: AppColors.tableTrongText,
          label: 'Trong',
        );
      case TableStatus.dangDung:
        return (
          bg: AppColors.surfaceContainerLowest,
          badgeBg: AppColors.tableDangDungBg,
          badgeText: AppColors.tableDangDungText,
          label: 'Đang dùng',
        );
      case TableStatus.daDat:
        return (
          bg: AppColors.surfaceContainerLowest,
          badgeBg: AppColors.tableDaDatBg,
          badgeText: AppColors.tableDaDatText,
          label: 'Đã đặt',
        );
    }
  }

  String _formatElapsed(int minutes) {
    if (minutes < 60) return '${minutes}p';
    final h = minutes ~/ 60;
    final m = minutes % 60;
    return m == 0 ? '${h}h' : '${h}h ${m}p';
  }

  @override
  Widget build(BuildContext context) {
    final style = _statusStyle;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: style.bg,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: table.status == TableStatus.trong
                ? const Color(0xFFCDE5D4)
                : const Color(0xFFE9ECEF),
          ),
          // Vien do doc canh trai khi dang cho mon qua lau
          boxShadow: table.isWaitingFood
              ? [const BoxShadow(color: AppColors.error, offset: Offset(-3, 0), blurRadius: 0)]
              : null,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 8,
              runSpacing: 4,
              children: [
                Text('Bàn #${table.id.toString().padLeft(2, '0')}',
                    style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                StatusBadge(label: style.label, background: style.badgeBg, textColor: style.badgeText),
              ],
            ),
            const SizedBox(height: 10),
            if (table.status == TableStatus.trong)
              const Align(
                alignment: Alignment.bottomRight,
                child: Icon(Icons.chair_outlined, color: AppColors.tableTrongText, size: 22),
              ),
            if (table.status == TableStatus.dangDung && table.elapsedMinutes != null) ...[
              Row(children: [
                const Icon(Icons.timer_outlined, size: 15, color: AppColors.onSurfaceVariant),
                const SizedBox(width: 4),
                Text(_formatElapsed(table.elapsedMinutes!),
                    style: const TextStyle(fontSize: 13, color: AppColors.onSurfaceVariant)),
              ]),
              if (table.totalAmount != null) ...[
                const SizedBox(height: 2),
                Text('${table.totalAmount!.toStringAsFixed(0)} đ',
                    style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.primary)),
              ],
              if (table.isWaitingFood) ...[
                const SizedBox(height: 2),
                const Row(children: [
                  Icon(Icons.restaurant, size: 13, color: AppColors.error),
                  SizedBox(width: 4),
                  Text('Chờ món', style: TextStyle(fontSize: 12, color: AppColors.error, fontWeight: FontWeight.w600)),
                ]),
              ],
            ],
            if (table.status == TableStatus.daDat && table.reservationTime != null) ...[
              Row(children: [
                const Icon(Icons.access_time, size: 15, color: AppColors.onSurfaceVariant),
                const SizedBox(width: 4),
                Text('${table.reservationTime} - ${table.reservationName ?? ""}',
                    style: const TextStyle(fontSize: 13, color: AppColors.onSurfaceVariant)),
              ]),
            ],
          ],
        ),
      ),
    );
  }
}
