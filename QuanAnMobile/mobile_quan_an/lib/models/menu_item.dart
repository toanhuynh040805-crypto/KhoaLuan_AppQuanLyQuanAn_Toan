class MenuItem {
  final int id;
  final String name;
  final String description;
  final int price;
  final String category;
  final String imageUrl;

  const MenuItem({
    required this.id,
    required this.name,
    required this.description,
    required this.price,
    required this.category,
    this.imageUrl = '',
  });

  // =========================================
  // JSON -> MenuItem
  // =========================================
  factory MenuItem.fromJson(
    Map<String, dynamic> json,
  ) {
    return MenuItem(
      id: _parseInt(json['id']),
      name: json['name']?.toString() ?? '',
      description:
          json['description']?.toString() ?? '',
      price: _parseInt(json['price']),
      category:
          json['category']?.toString() ?? '',
      imageUrl:
          (json['imageUrl'] ?? json['ImageUrl'])?.toString() ?? '',
    );
  }

  // =========================================
  // MenuItem -> JSON
  // =========================================
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'description': description,
      'price': price,
      'category': category,
      'imageUrl': imageUrl,
    };
  }

  // =========================================
  // CHUYỂN KIỂU SỐ AN TOÀN
  // =========================================
  static int _parseInt(dynamic value) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }
}
