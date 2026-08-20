import 'package:flutter/material.dart';
import '../models/user_profile.dart';
import '../theme/app_colors.dart';
import 'table_map_screen.dart';
import 'menu_screen.dart';
import 'notifications_screen.dart';
import 'profile_screen.dart';

// Khung dieu huong chinh sau khi dang nhap - 4 tab duoi giong het thiet ke Stitch:
// So do | Thuc don | Thong bao | Ca nhan.
// Dung IndexedStack de giu lai state cua tung tab khi chuyen qua lai.
class MainShell extends StatefulWidget {
  final UserProfile user;
  const MainShell({super.key, required this.user});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _currentIndex = 0;

  // TODO: so luong thong bao chua doc nen lay tu SignalR realtime,
  // hien dang hardcode de khop giao dien mau (co 1 cham do tren icon chuong).
  final int _unreadNotifications = 1;

  late final List<Widget> _tabs = [
    TableMapScreen(onOpenMenuTab: () => setState(() => _currentIndex = 1)),
    const MenuScreen(),
    const NotificationsScreen(),
    ProfileScreen(user: widget.user),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(index: _currentIndex, children: _tabs),
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(
          color: AppColors.surfaceContainerLowest,
          border: Border(top: BorderSide(color: Color(0xFFE9ECEF))),
        ),
        child: SafeArea(
          child: SizedBox(
            height: 62,
            child: Row(
              children: [
                _navItem(0, Icons.grid_view_rounded, 'Sơ đồ'),
                _navItem(1, Icons.restaurant_menu, 'Thực đơn'),
                _navItem(2, Icons.notifications_none_rounded, 'Thông báo', badgeCount: _unreadNotifications),
                _navItem(3, Icons.person_outline_rounded, 'Cá nhân'),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _navItem(int index, IconData icon, String label, {int badgeCount = 0}) {
    final isSelected = _currentIndex == index;
    return Expanded(
      child: InkWell(
        onTap: () => setState(() => _currentIndex = index),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 4),
                  decoration: BoxDecoration(
                    color: isSelected ? AppColors.primaryContainer : Colors.transparent,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Icon(icon, color: isSelected ? Colors.white : AppColors.onSurfaceVariant, size: 22),
                ),
                if (badgeCount > 0)
                  const Positioned(
                    right: 12, top: -2,
                    child: CircleAvatar(radius: 4, backgroundColor: AppColors.error),
                  ),
              ],
            ),
            const SizedBox(height: 3),
            Text(label,
                style: TextStyle(
                  fontSize: 11,
                  color: isSelected ? AppColors.primary : AppColors.onSurfaceVariant,
                  fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                )),
          ],
        ),
      ),
    );
  }
}
