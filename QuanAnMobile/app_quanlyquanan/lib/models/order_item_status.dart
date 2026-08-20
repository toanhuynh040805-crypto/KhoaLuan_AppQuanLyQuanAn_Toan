// Khop voi enum KitchenStatus ben Backend (Models/OrderItem.cs)
enum OrderItemStatus { choXuLy, daInPhieu, dangLam, hoanThanh, daPhucVu }

OrderItemStatus orderItemStatusFromInt(int value) => OrderItemStatus.values[value];

extension OrderItemStatusX on OrderItemStatus {
  String get label {
    switch (this) {
      case OrderItemStatus.choXuLy:
      case OrderItemStatus.daInPhieu:
        return 'Cho xu ly';
      case OrderItemStatus.dangLam:
        return 'Dang lam';
      case OrderItemStatus.hoanThanh:
        return 'Da xong';
      case OrderItemStatus.daPhucVu:
        return 'Da phuc vu';
    }
  }
}
