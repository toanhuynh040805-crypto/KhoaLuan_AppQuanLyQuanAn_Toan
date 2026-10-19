import 'menu_item.dart';

class CartItem {
  final MenuItem menuItem;

  int quantity;

  // Ghi chú riêng cho món
  String? note;

  // Số phần của món này khách yêu cầu MANG VỀ (0..quantity).
  // Phần còn lại (quantity - takeawayQuantity) là ăn tại chỗ.
  int takeawayQuantity;

  CartItem({
    required this.menuItem,
    required this.quantity,
    this.note,
    this.takeawayQuantity = 0,
  });

  int get totalPrice => menuItem.price * quantity;

  // Số phần ăn tại chỗ
  int get dineInQuantity => quantity - takeawayQuantity;

  // Đảm bảo số phần mang về không vượt quá số lượng (gọi sau khi đổi quantity)
  void clampTakeaway() {
    if (takeawayQuantity > quantity) takeawayQuantity = quantity;
    if (takeawayQuantity < 0) takeawayQuantity = 0;
  }
}