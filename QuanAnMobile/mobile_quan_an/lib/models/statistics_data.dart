class RevenuePoint {
  final String label;
  final double revenue;

  const RevenuePoint({required this.label, required this.revenue});

  factory RevenuePoint.fromJson(Map<String, dynamic> json) => RevenuePoint(
        label: (json['label'] ?? json['Label'] ?? '').toString(),
        revenue: _number(json['revenue'] ?? json['Revenue']),
      );
}

class TopItemStat {
  final String name;
  final int quantity;
  final double revenue;

  const TopItemStat({required this.name, required this.quantity, required this.revenue});

  factory TopItemStat.fromJson(Map<String, dynamic> json) => TopItemStat(
        name: (json['name'] ?? json['Name'] ?? '').toString(),
        quantity: _integer(json['quantity'] ?? json['Quantity']),
        revenue: _number(json['revenue'] ?? json['Revenue']),
      );
}

class EmployeeStat {
  final String name;
  final int invoiceCount;
  final double revenue;

  const EmployeeStat({required this.name, required this.invoiceCount, required this.revenue});

  factory EmployeeStat.fromJson(Map<String, dynamic> json) => EmployeeStat(
        name: (json['name'] ?? json['Name'] ?? '').toString(),
        invoiceCount: _integer(json['invoiceCount'] ?? json['InvoiceCount']),
        revenue: _number(json['revenue'] ?? json['Revenue']),
      );
}

class CampaignStat {
  final String name;
  final int invoiceCount;
  final double totalDiscount;

  const CampaignStat({required this.name, required this.invoiceCount, required this.totalDiscount});

  factory CampaignStat.fromJson(Map<String, dynamic> json) => CampaignStat(
        name: (json['name'] ?? json['Name'] ?? '').toString(),
        invoiceCount: _integer(json['invoiceCount'] ?? json['InvoiceCount']),
        totalDiscount: _number(json['totalDiscount'] ?? json['TotalDiscount']),
      );
}

class StatisticsDashboard {
  final double revenue;
  final int invoiceCount;
  final double avgInvoiceValue;
  final double totalDiscount;
  final double? trendPercent;
  final List<RevenuePoint> revenuePoints;
  final List<TopItemStat> topItems;
  final List<EmployeeStat> employees;
  final List<CampaignStat> campaigns;

  const StatisticsDashboard({
    required this.revenue,
    required this.invoiceCount,
    required this.avgInvoiceValue,
    required this.totalDiscount,
    required this.trendPercent,
    required this.revenuePoints,
    required this.topItems,
    required this.employees,
    required this.campaigns,
  });

  factory StatisticsDashboard.fromJson(Map<String, dynamic> json) {
    List<T> listOf<T>(dynamic raw, T Function(Map<String, dynamic>) mapper) {
      if (raw is! List) return <T>[];
      return raw.whereType<Map>().map((item) => mapper(Map<String, dynamic>.from(item))).toList();
    }

    final dynamic trend = json['trendPercent'] ?? json['TrendPercent'];
    return StatisticsDashboard(
      revenue: _number(json['revenue'] ?? json['Revenue']),
      invoiceCount: _integer(json['invoiceCount'] ?? json['InvoiceCount']),
      avgInvoiceValue: _number(json['avgInvoiceValue'] ?? json['AvgInvoiceValue']),
      totalDiscount: _number(json['totalDiscount'] ?? json['TotalDiscount']),
      trendPercent: trend == null ? null : _number(trend),
      revenuePoints: listOf(json['revenuePoints'] ?? json['RevenuePoints'], RevenuePoint.fromJson),
      topItems: listOf(json['topItems'] ?? json['TopItems'], TopItemStat.fromJson),
      employees: listOf(json['employees'] ?? json['Employees'], EmployeeStat.fromJson),
      campaigns: listOf(json['campaigns'] ?? json['Campaigns'], CampaignStat.fromJson),
    );
  }
}

double _number(dynamic value) {
  if (value is num) return value.toDouble();
  return double.tryParse(value?.toString() ?? '') ?? 0;
}

int _integer(dynamic value) {
  if (value is num) return value.toInt();
  return int.tryParse(value?.toString() ?? '') ?? 0;
}
