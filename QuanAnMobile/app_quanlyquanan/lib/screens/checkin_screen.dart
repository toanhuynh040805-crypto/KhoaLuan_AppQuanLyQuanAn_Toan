import 'dart:async';
import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

// Man hinh diem danh vao ca - tinh nang moi de xuat tu Stitch, dung GPS
// de xac nhan nhan vien dang co mat tai quan truoc khi cho vao ca.
// TODO: hien chua goi API/GPS that. Can:
//  1. Dung package geolocator de lay vi tri that cua may
//  2. Goi API Backend (can bo sung POST /api/attendance/checkin) gui kem toa do
//  3. Backend so sanh khoang cach voi toa do quan (Haversine) de xac dinh "Hop le"
class CheckinScreen extends StatefulWidget {
  const CheckinScreen({super.key});

  @override
  State<CheckinScreen> createState() => _CheckinScreenState();
}

class _CheckinScreenState extends State<CheckinScreen> {
  late Timer _timer;
  DateTime _now = DateTime.now();

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) => setState(() => _now = DateTime.now()));
  }

  @override
  void dispose() {
    _timer.cancel();
    super.dispose();
  }

  String _weekday(DateTime d) {
    const names = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'];
    return names[d.weekday - 1];
  }

  @override
  Widget build(BuildContext context) {
    final hh = _now.hour.toString().padLeft(2, '0');
    final mm = _now.minute.toString().padLeft(2, '0');

    return Scaffold(
      appBar: AppBar(title: const Text('Điểm danh vào ca')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const SizedBox(height: 8),
          Center(
            child: Column(
              children: [
                Text('$hh:$mm', style: const TextStyle(fontSize: 44, fontWeight: FontWeight.w800)),
                const SizedBox(height: 4),
                Text('${_weekday(_now)}, ${_now.day} Tháng ${_now.month}',
                    style: const TextStyle(color: AppColors.onSurfaceVariant)),
                const SizedBox(height: 14),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  decoration: BoxDecoration(
                    border: Border.all(color: AppColors.outlineVariant),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: const Row(mainAxisSize: MainAxisSize.min, children: [
                    Icon(Icons.work_outline, size: 16, color: AppColors.secondary),
                    SizedBox(width: 6),
                    Text('Ca sáng: 08:00 - 14:00', style: TextStyle(fontWeight: FontWeight.w600)),
                  ]),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          Container(
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLowest,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: const Color(0xFFE9ECEF)),
            ),
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('Vị trí hiện tại', style: TextStyle(fontWeight: FontWeight.w700)),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                      decoration: BoxDecoration(color: const Color(0xFFE3F2E8), borderRadius: BorderRadius.circular(999)),
                      child: const Row(mainAxisSize: MainAxisSize.min, children: [
                        Icon(Icons.check_circle, size: 14, color: AppColors.tertiary),
                        SizedBox(width: 4),
                        Text('Hợp lệ', style: TextStyle(color: AppColors.tertiary, fontSize: 12, fontWeight: FontWeight.w700)),
                      ]),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                // TODO: thay bang widget ban do that (vi du package google_maps_flutter)
                // khi tich hop GPS that. Hien chi la placeholder hinh anh tinh.
                ClipRRect(
                  borderRadius: BorderRadius.circular(10),
                  child: Container(
                    height: 130,
                    color: AppColors.surfaceContainerHigh,
                    alignment: Alignment.center,
                    child: const Icon(Icons.map_outlined, size: 40, color: AppColors.onSurfaceVariant),
                  ),
                ),
                const SizedBox(height: 10),
                const Row(children: [
                  Icon(Icons.restaurant, size: 16, color: AppColors.onSurfaceVariant),
                  SizedBox(width: 6),
                  Text('Nhà hàng Central Point - Tầng 1', style: TextStyle(color: AppColors.onSurfaceVariant)),
                ]),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ElevatedButton.icon(
                onPressed: () {
                  // TODO: goi API check-in that
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Điểm danh thành công')));
                },
                icon: const Icon(Icons.fingerprint),
                label: const Text('Bắt đầu làm việc'),
                style: ElevatedButton.styleFrom(minimumSize: const Size.fromHeight(50)),
              ),
              const SizedBox(height: 10),
              OutlinedButton.icon(
                onPressed: () {},
                icon: const Icon(Icons.history),
                label: const Text('Lịch sử điểm danh'),
                style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(50)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
