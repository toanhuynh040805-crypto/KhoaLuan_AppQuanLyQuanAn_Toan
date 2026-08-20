import 'menu_item.dart';

// Món đang nằm trong giỏ order tạm thời trên Mobile, trước khi gửi lên server
class CartItem {
  final MenuItem menuItem;
  int quantity;
  String? note;

  CartItem({required this.menuItem, this.quantity = 1, this.note});

  double get subtotal => menuItem.price * quantity;
}
