class MenuItem {
  final int id;
  final String name;
  final double price;
  final String? imageUrl;
  final int categoryId;
  final int departmentId; // 1 = Bep, 2 = QuayNuoc

  MenuItem({
    required this.id,
    required this.name,
    required this.price,
    this.imageUrl,
    required this.categoryId,
    required this.departmentId,
  });

  factory MenuItem.fromJson(Map<String, dynamic> json) {
    return MenuItem(
      id: json['id'],
      name: json['name'],
      price: (json['price'] as num).toDouble(),
      imageUrl: json['imageUrl'],
      categoryId: json['categoryId'],
      departmentId: json['departmentId'],
    );
  }
}
