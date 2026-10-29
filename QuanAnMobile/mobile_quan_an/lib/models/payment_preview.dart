class PaymentPreview {
  final int orderId;
  final int tableId;
  final DateTime? orderTime;
  final String waiterName;
  final int subTotal;
  final double discountPercent;
  final int discountAmount;
  final int? appliedCampaignId;
  final String? appliedCampaignName;
  final int taxAmount;
  final int vipRoomFee;
  final int vipMinimumGap;
  final int totalAmount;

  final String qrImageUrl;
  final String bankName;
  final String bankAccountNo;
  final String bankAccountName;
  final String bankBranch;
  final String transferContent;
  final String shopName;
  final String shopAddress;

  const PaymentPreview({
    required this.orderId,
    required this.tableId,
    this.orderTime,
    required this.waiterName,
    required this.subTotal,
    required this.discountPercent,
    required this.discountAmount,
    this.appliedCampaignId,
    this.appliedCampaignName,
    required this.taxAmount,
    required this.vipRoomFee,
    required this.vipMinimumGap,
    required this.totalAmount,
    required this.qrImageUrl,
    required this.bankName,
    required this.bankAccountNo,
    required this.bankAccountName,
    required this.bankBranch,
    required this.transferContent,
    required this.shopName,
    required this.shopAddress,
  });

  factory PaymentPreview.fromJson(
    Map<String, dynamic> json,
  ) {
    return PaymentPreview(
      orderId: _parseInt(json['orderId']),
      tableId: _parseInt(json['tableId']),
      orderTime: DateTime.tryParse(
        json['orderTime']?.toString() ?? '',
      ),
      waiterName:
          json['waiterName']?.toString() ?? '',
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
      qrImageUrl:
          json['qrImageUrl']?.toString() ?? '',
      bankName:
          json['bankName']?.toString() ?? '',
      bankAccountNo:
          json['bankAccountNo']?.toString() ?? '',
      bankAccountName:
          json['bankAccountName']?.toString() ?? '',
      bankBranch:
          json['bankBranch']?.toString() ?? '',
      transferContent:
          json['transferContent']?.toString() ?? '',
      shopName:
          json['shopName']?.toString() ?? 'Lẩu Bò Nhà Gỗ Sài Gòn',
      shopAddress:
          json['shopAddress']?.toString() ?? '',
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
