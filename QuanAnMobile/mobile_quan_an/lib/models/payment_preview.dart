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

  final int? customerId;
  final String? customerName;
  final int loyaltyPointBalance;
  final int maxRedeemablePoints;
  final int redeemedPoints;
  final int loyaltyPointValue;
  final int loyaltyDiscount;
  final int estimatedEarnedPoints;

  final String qrImageUrl;
  final String qrPayload;
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
    this.customerId,
    this.customerName,
    this.loyaltyPointBalance = 0,
    this.maxRedeemablePoints = 0,
    this.redeemedPoints = 0,
    this.loyaltyPointValue = 1000,
    this.loyaltyDiscount = 0,
    this.estimatedEarnedPoints = 0,
    required this.qrImageUrl,
    required this.qrPayload,
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
      customerId: json['customerId'] == null
          ? null
          : _parseInt(json['customerId']),
      customerName: json['customerName']?.toString(),
      loyaltyPointBalance: _parseInt(json['loyaltyPointBalance']),
      maxRedeemablePoints: _parseInt(json['maxRedeemablePoints']),
      redeemedPoints: _parseInt(json['redeemedPoints']),
      loyaltyPointValue: _parseInt(json['loyaltyPointValue']),
      loyaltyDiscount: _parseInt(json['loyaltyDiscount']),
      estimatedEarnedPoints: _parseInt(json['estimatedEarnedPoints']),
      qrImageUrl:
          json['qrImageUrl']?.toString() ?? '',
      qrPayload:
          json['qrPayload']?.toString() ??
          json['qrCodeContent']?.toString() ??
          '',
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
