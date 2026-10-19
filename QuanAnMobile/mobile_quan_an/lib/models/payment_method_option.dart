class PaymentMethodOption {
  final int method;
  final String code;
  final String label;
  final bool isCash;

  const PaymentMethodOption({
    required this.method,
    required this.code,
    required this.label,
    required this.isCash,
  });

  factory PaymentMethodOption.fromJson(Map<String, dynamic> json) {
    return PaymentMethodOption(
      method: _parseInt(json['method']),
      code: json['code']?.toString().trim() ?? '',
      label: json['label']?.toString().trim() ?? '',
      isCash: json['isCash'] == true,
    );
  }

  static int _parseInt(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '') ?? 0;
  }
}
