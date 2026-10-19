class MemberCustomer {
  final int id;
  final String fullName;
  final String phone;
  final int loyaltyPoint;
  final String memberLevel;

  const MemberCustomer({
    required this.id,
    required this.fullName,
    required this.phone,
    required this.loyaltyPoint,
    required this.memberLevel,
  });

  factory MemberCustomer.fromJson(Map<String, dynamic> json) {
    int parseInt(dynamic value) {
      if (value is int) return value;
      if (value is num) return value.toInt();
      return int.tryParse(value?.toString() ?? '') ?? 0;
    }

    return MemberCustomer(
      id: parseInt(json['id'] ?? json['customerId']),
      fullName: (json['fullName'] ?? '').toString(),
      phone: (json['phone'] ?? '').toString(),
      loyaltyPoint: parseInt(json['loyaltyPoint']),
      memberLevel: (json['memberLevel'] ?? 'Thành viên').toString(),
    );
  }
}
