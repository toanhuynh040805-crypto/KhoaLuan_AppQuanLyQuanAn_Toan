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

    String parseMemberLevel(dynamic value) {
      if (value is num) {
        return switch (value.toInt()) {
          0 => 'Thành viên',
          1 => 'Bạc',
          2 => 'Vàng',
          3 => 'Kim cương',
          _ => 'Thành viên',
        };
      }

      final String raw = value?.toString().trim() ?? '';
      if (raw.isEmpty) return 'Thành viên';

      final int? numeric = int.tryParse(raw);
      if (numeric != null) {
        return switch (numeric) {
          0 => 'Thành viên',
          1 => 'Bạc',
          2 => 'Vàng',
          3 => 'Kim cương',
          _ => 'Thành viên',
        };
      }

      return switch (raw.toLowerCase()) {
        'thanhvien' || 'thành viên' => 'Thành viên',
        'bac' || 'bạc' => 'Bạc',
        'vang' || 'vàng' => 'Vàng',
        'kimcuong' || 'kim cương' => 'Kim cương',
        _ => raw,
      };
    }

    return MemberCustomer(
      id: parseInt(json['id'] ?? json['customerId']),
      fullName: (json['fullName'] ?? '').toString().trim(),
      phone: (json['phone'] ?? '').toString().trim(),
      loyaltyPoint: parseInt(json['loyaltyPoint']),
      memberLevel: parseMemberLevel(json['memberLevel']),
    );
  }
}
