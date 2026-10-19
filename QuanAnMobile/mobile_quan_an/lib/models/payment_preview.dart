class PaymentPreview {
  final int orderId;
  final int tableId;
  final int subTotal;
  final double discountPercent;
  final int discountAmount;
  final int? appliedCampaignId;
  final String? appliedCampaignName;
  final int taxAmount;
  final int vipRoomFee;
  final int vipMinimumGap;
  final int totalAmount;

  const PaymentPreview({
    required this.orderId,
    required this.tableId,
    required this.subTotal,
    required this.discountPercent,
    required this.discountAmount,
    this.appliedCampaignId,
    this.appliedCampaignName,
    required this.taxAmount,
    required this.vipRoomFee,
    required this.vipMinimumGap,
    required this.totalAmount,
  });

  factory PaymentPreview.fromJson(
    Map<String, dynamic> json,
  ) {
    return PaymentPreview(
      orderId: _parseInt(json['orderId']),
      tableId: _parseInt(json['tableId']),
      subTotal: _parseInt(json['subTotal']),
      discountPercent:
          _parseDouble(json['discountPercent']),
      discountAmount:
          _parseInt(json['discountAmount']),
      appliedCampaignId:
          json['appliedCampaignId'] == null
              ? null
              : _parseInt(
                  json['appliedCampaignId'],
                ),
      appliedCampaignName:
          json['appliedCampaignName']?.toString(),
      taxAmount: _parseInt(json['taxAmount']),
      vipRoomFee: _parseInt(json['vipRoomFee']),
      vipMinimumGap:
          _parseInt(json['vipMinimumGap']),
      totalAmount: _parseInt(json['totalAmount']),
    );
  }

  static int _parseInt(dynamic value) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.round();
    }

    return double.tryParse(
              value?.toString() ?? '',
            )?.round() ??
        0;
  }

  static double _parseDouble(dynamic value) {
    if (value is num) {
      return value.toDouble();
    }

    return double.tryParse(
          value?.toString() ?? '',
        ) ??
        0;
  }
}
