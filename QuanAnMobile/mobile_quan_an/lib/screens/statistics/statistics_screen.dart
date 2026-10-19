import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/statistics_data.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class StatisticsScreen extends StatefulWidget {
  const StatisticsScreen({super.key});

  @override
  State<StatisticsScreen> createState() => _StatisticsScreenState();
}

class _StatisticsScreenState extends State<StatisticsScreen> {
  DateTime _from = DateTime.now();
  DateTime _to = DateTime.now();
  StatisticsDashboard? _data;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  DateTime get _today {
    final DateTime now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final StatisticsDashboard data = await AppServices.statisticsService.getDashboard(
        from: _from,
        to: _to,
      );
      if (!mounted) return;
      setState(() {
        _data = data;
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      TopMessage.show(context, message: error.message, type: TopMessageType.error);
    }
  }

  void _preset(int days) {
    final DateTime today = _today;
    setState(() {
      _to = today;
      _from = today.subtract(Duration(days: days - 1));
    });
    _load();
  }

  Future<void> _pickDate({required bool from}) async {
    final DateTime initial = from ? _from : _to;
    final DateTime? picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(2020),
      lastDate: _today.add(const Duration(days: 365)),
      helpText: from ? 'Chọn ngày bắt đầu' : 'Chọn ngày kết thúc',
    );
    if (picked == null || !mounted) return;

    setState(() {
      if (from) {
        _from = picked;
        if (_from.isAfter(_to)) _to = picked;
      } else {
        _to = picked;
        if (_to.isBefore(_from)) _from = picked;
      }
    });
    await _load();
  }

  String _dateText(DateTime value) {
    final String d = value.day.toString().padLeft(2, '0');
    final String m = value.month.toString().padLeft(2, '0');
    return '$d/$m/${value.year}';
  }

  String _money(num amount) {
    final String digits = amount.round().toString();
    final StringBuffer out = StringBuffer();
    for (int i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) out.write('.');
      out.write(digits[i]);
    }
    return '${out}đ';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF6F7F9),
      appBar: AppBar(
        title: const Text('Thống kê'),
        backgroundColor: Colors.white,
        foregroundColor: AppTheme.textColor,
        elevation: 0,
        actions: [
          IconButton(onPressed: _loading ? null : _load, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16),
          children: [
            _rangeCard(),
            const SizedBox(height: 14),
            if (_loading && _data == null)
              const Padding(
                padding: EdgeInsets.only(top: 80),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_data != null) ...[
              _summaryGrid(_data!),
              const SizedBox(height: 14),
              _revenueChart(_data!),
              const SizedBox(height: 14),
              _topItems(_data!),
              const SizedBox(height: 14),
              _employees(_data!),
              if (_data!.campaigns.isNotEmpty) ...[
                const SizedBox(height: 14),
                _campaigns(_data!),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _rangeCard() {
    return _card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Khoảng thời gian', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton(onPressed: () => _preset(1), child: const Text('Hôm nay')),
              OutlinedButton(onPressed: () => _preset(7), child: const Text('7 ngày')),
              OutlinedButton(onPressed: () => _preset(30), child: const Text('30 ngày')),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _dateButton('Từ ngày', _from, () => _pickDate(from: true))),
              const SizedBox(width: 10),
              Expanded(child: _dateButton('Đến ngày', _to, () => _pickDate(from: false))),
            ],
          ),
        ],
      ),
    );
  }

  Widget _dateButton(String title, DateTime value, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          border: Border.all(color: Colors.grey.shade300),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: TextStyle(fontSize: 11, color: Colors.grey.shade600)),
            const SizedBox(height: 4),
            Row(children: [
              const Icon(Icons.calendar_month_outlined, size: 17, color: AppTheme.orangeColor),
              const SizedBox(width: 6),
              Expanded(child: Text(_dateText(value), style: const TextStyle(fontWeight: FontWeight.w600))),
            ]),
          ],
        ),
      ),
    );
  }

  Widget _summaryGrid(StatisticsDashboard data) {
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 10,
      crossAxisSpacing: 10,
      childAspectRatio: 1.55,
      children: [
        _statCard('Doanh thu', _money(data.revenue), Icons.payments_outlined),
        _statCard('Hóa đơn', '${data.invoiceCount}', Icons.receipt_long_outlined),
        _statCard('TB / hóa đơn', _money(data.avgInvoiceValue), Icons.analytics_outlined),
        _statCard('Đã giảm', _money(data.totalDiscount), Icons.discount_outlined),
      ],
    );
  }

  Widget _statCard(String title, String value, IconData icon) {
    return _card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(icon, color: AppTheme.orangeColor, size: 22),
          const SizedBox(height: 7),
          Text(value, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 3),
          Text(title, style: TextStyle(fontSize: 12, color: Colors.grey.shade600)),
        ],
      ),
    );
  }

  Widget _revenueChart(StatisticsDashboard data) {
    final double maxValue = data.revenuePoints.fold<double>(0, (m, p) => p.revenue > m ? p.revenue : m);
    return _card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(children: [
            const Expanded(child: Text('Doanh thu theo ngày', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16))),
            if (data.trendPercent != null)
              Text('${data.trendPercent! >= 0 ? '+' : ''}${data.trendPercent!.toStringAsFixed(1)}%',
                  style: TextStyle(fontWeight: FontWeight.w700,
                      color: data.trendPercent! >= 0 ? Colors.green : Colors.red)),
          ]),
          const SizedBox(height: 14),
          SizedBox(
            height: 190,
            child: data.revenuePoints.isEmpty
                ? const Center(child: Text('Chưa có dữ liệu'))
                : ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: data.revenuePoints.length,
                    separatorBuilder: (_, _) => const SizedBox(width: 8),
                    itemBuilder: (_, index) {
                      final RevenuePoint point = data.revenuePoints[index];
                      final double ratio = maxValue <= 0 ? 0 : point.revenue / maxValue;
                      return SizedBox(
                        width: 44,
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            Expanded(
                              child: Align(
                                alignment: Alignment.bottomCenter,
                                child: Container(
                                  width: 24,
                                  height: 130 * ratio + (point.revenue > 0 ? 4 : 1),
                                  decoration: BoxDecoration(
                                    color: AppTheme.orangeColor,
                                    borderRadius: BorderRadius.circular(6),
                                  ),
                                ),
                              ),
                            ),
                            const SizedBox(height: 7),
                            Text(point.label, style: const TextStyle(fontSize: 10)),
                          ],
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _topItems(StatisticsDashboard data) {
    return _sectionList(
      title: 'Món bán chạy',
      empty: 'Chưa có món bán trong khoảng này',
      count: data.topItems.length,
      builder: (index) {
        final TopItemStat item = data.topItems[index];
        return ListTile(
          contentPadding: EdgeInsets.zero,
          leading: CircleAvatar(
            backgroundColor: const Color(0xFFFFEEE5),
            foregroundColor: AppTheme.orangeColor,
            child: Text('${index + 1}'),
          ),
          title: Text(item.name, style: const TextStyle(fontWeight: FontWeight.w600)),
          subtitle: Text('${item.quantity} phần'),
          trailing: Text(_money(item.revenue), style: const TextStyle(fontWeight: FontWeight.w700)),
        );
      },
    );
  }

  Widget _employees(StatisticsDashboard data) {
    return _sectionList(
      title: 'Doanh thu theo nhân viên',
      empty: 'Chưa có dữ liệu nhân viên',
      count: data.employees.length,
      builder: (index) {
        final EmployeeStat item = data.employees[index];
        return ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.badge_outlined, color: AppTheme.orangeColor),
          title: Text(item.name.isEmpty ? '(Không rõ)' : item.name),
          subtitle: Text('${item.invoiceCount} hóa đơn'),
          trailing: Text(_money(item.revenue), style: const TextStyle(fontWeight: FontWeight.w600)),
        );
      },
    );
  }

  Widget _campaigns(StatisticsDashboard data) {
    return _sectionList(
      title: 'Khuyến mãi',
      empty: 'Không có khuyến mãi trong khoảng này',
      count: data.campaigns.length,
      builder: (index) {
        final CampaignStat item = data.campaigns[index];
        return ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.local_offer_outlined, color: AppTheme.orangeColor),
          title: Text(item.name),
          subtitle: Text('${item.invoiceCount} hóa đơn áp dụng'),
          trailing: Text('-${_money(item.totalDiscount)}', style: const TextStyle(fontWeight: FontWeight.w600)),
        );
      },
    );
  }

  Widget _sectionList({required String title, required String empty, required int count, required Widget Function(int) builder}) {
    return _card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
          const SizedBox(height: 6),
          if (count == 0)
            Padding(padding: const EdgeInsets.symmetric(vertical: 18), child: Text(empty, style: TextStyle(color: Colors.grey.shade600)))
          else
            ...List.generate(count, (index) => Column(children: [
                  builder(index),
                  if (index < count - 1) const Divider(height: 1),
                ])),
        ],
      ),
    );
  }

  Widget _card({required Widget child}) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: child,
    );
  }
}
