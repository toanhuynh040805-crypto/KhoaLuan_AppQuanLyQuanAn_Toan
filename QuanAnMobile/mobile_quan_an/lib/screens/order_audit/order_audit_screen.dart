import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../widgets/top_message.dart';

class OrderAuditScreen extends StatefulWidget {
  const OrderAuditScreen({super.key});

  @override
  State<OrderAuditScreen> createState() => _OrderAuditScreenState();
}

class _OrderAuditScreenState extends State<OrderAuditScreen> {
  bool _loading = true;
  DateTime _date = DateTime.now();
  int _dateMode = 0; // 0 hôm nay, 1 hôm qua, 2 chọn ngày
  int _tableId = 0;
  int _requesterUserId = 0;

  List<Map<String, dynamic>> _tables = <Map<String, dynamic>>[];
  List<Map<String, dynamic>> _requesters = <Map<String, dynamic>>[];
  List<Map<String, dynamic>> _records = <Map<String, dynamic>>[];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final data = await AppServices.orderAuditService.getAudit(
        date: _date,
        tableId: _tableId == 0 ? null : _tableId,
        paymentRequestedByUserId:
            _requesterUserId == 0 ? null : _requesterUserId,
      );

      if (!mounted) return;
      setState(() {
        _tables = _mapList(data['tables']);
        _requesters = _mapList(data['paymentRequesters']);
        _records = _mapList(data['records']);
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  List<Map<String, dynamic>> _mapList(dynamic value) {
    if (value is! List) return <Map<String, dynamic>>[];
    return value
        .whereType<Map>()
        .map(
          (item) => item.map(
            (key, value) => MapEntry(key.toString(), value),
          ),
        )
        .toList();
  }

  String _two(int value) => value.toString().padLeft(2, '0');

  String _dateLabel(DateTime value) =>
      '${_two(value.day)}/${_two(value.month)}/${value.year}';

  String _time(dynamic value) {
    final DateTime? parsed = DateTime.tryParse(value?.toString() ?? '');
    if (parsed == null) return '--';
    return '${_two(parsed.hour)}:${_two(parsed.minute)}';
  }

  String _dateTime(dynamic value) {
    final DateTime? parsed = DateTime.tryParse(value?.toString() ?? '');
    if (parsed == null) return '--';
    return '${_dateLabel(parsed)} • ${_time(value)}';
  }

  int _int(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '') ?? 0;
  }

  num _num(dynamic value) {
    if (value is num) return value;
    return num.tryParse(value?.toString() ?? '') ?? 0;
  }

  String _money(dynamic value) {
    final int amount = _num(value).round();
    final String source = amount.toString();
    final StringBuffer result = StringBuffer();
    for (int i = 0; i < source.length; i++) {
      if (i > 0 && (source.length - i) % 3 == 0) {
        result.write('.');
      }
      result.write(source[i]);
    }
    return '${result}đ';
  }

  Future<void> _setDateMode(int mode) async {
    DateTime next = DateTime.now();
    if (mode == 1) {
      next = next.subtract(const Duration(days: 1));
    } else if (mode == 2) {
      final DateTime? picked = await showDatePicker(
        context: context,
        initialDate: _date,
        firstDate: DateTime(2024),
        lastDate: DateTime.now().add(const Duration(days: 365)),
      );
      if (picked == null) return;
      next = picked;
    }

    if (!mounted) return;
    setState(() {
      _dateMode = mode;
      _date = DateTime(next.year, next.month, next.day);
    });
    await _load();
  }

  Widget _filters() {
    return Card(
      margin: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ChoiceChip(
                  label: const Text('Hôm nay'),
                  selected: _dateMode == 0,
                  onSelected: (_) => _setDateMode(0),
                ),
                ChoiceChip(
                  label: const Text('Hôm qua'),
                  selected: _dateMode == 1,
                  onSelected: (_) => _setDateMode(1),
                ),
                ChoiceChip(
                  label: Text(
                    _dateMode == 2
                        ? _dateLabel(_date)
                        : 'Chọn ngày',
                  ),
                  selected: _dateMode == 2,
                  onSelected: (_) => _setDateMode(2),
                ),
              ],
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int>(
              value: _tableId,
              decoration: const InputDecoration(
                labelText: 'Bàn',
                border: OutlineInputBorder(),
                isDense: true,
              ),
              items: <DropdownMenuItem<int>>[
                const DropdownMenuItem<int>(
                  value: 0,
                  child: Text('Tất cả bàn'),
                ),
                ..._tables.map(
                  (table) => DropdownMenuItem<int>(
                    value: _int(table['id']),
                    child: Text(
                      '${table['name'] ?? ''}'
                      '${(table['areaName'] ?? '').toString().trim().isEmpty ? '' : ' • ${table['areaName']}'}',
                    ),
                  ),
                ),
              ],
              onChanged: (value) async {
                setState(() => _tableId = value ?? 0);
                await _load();
              },
            ),
            const SizedBox(height: 10),
            DropdownButtonFormField<int>(
              value: _requesterUserId,
              decoration: const InputDecoration(
                labelText: 'Nhân viên gọi thanh toán',
                border: OutlineInputBorder(),
                isDense: true,
              ),
              items: <DropdownMenuItem<int>>[
                const DropdownMenuItem<int>(
                  value: 0,
                  child: Text('Tất cả nhân viên'),
                ),
                ..._requesters.map(
                  (user) => DropdownMenuItem<int>(
                    value: _int(user['userId']),
                    child: Text(
                      (user['fullName'] ?? 'Không rõ').toString(),
                    ),
                  ),
                ),
              ],
              onChanged: (value) async {
                setState(() => _requesterUserId = value ?? 0);
                await _load();
              },
            ),
          ],
        ),
      ),
    );
  }

  Widget _recordCard(Map<String, dynamic> record) {
    final List<dynamic> tableNames =
        record['tableNames'] is List ? record['tableNames'] as List : const [];
    final List<Map<String, dynamic>> batches = _mapList(record['batches']);
    final List<Map<String, dynamic>> journey = _mapList(record['tableJourney']);
    final String requester =
        (record['paymentRequestedByName'] ?? 'Chưa gọi thanh toán').toString();
    final int invoiceId = _int(record['invoiceId']);
    final int orderId = _int(record['orderId']);

    return Card(
      margin: const EdgeInsets.fromLTRB(12, 0, 12, 12),
      clipBehavior: Clip.antiAlias,
      child: ExpansionTile(
        initiallyExpanded: true,
        title: Text(
          invoiceId > 0
              ? 'Hóa đơn #$invoiceId • Order #$orderId'
              : 'Order #$orderId • Chưa có hóa đơn',
          style: const TextStyle(fontWeight: FontWeight.w800),
        ),
        subtitle: Text(
          '${tableNames.isEmpty ? 'Không rõ bàn' : tableNames.join(', ')}\n'
          '${_dateTime(record['invoiceDate'] ?? record['orderTime'])}',
        ),
        childrenPadding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
        children: [
          _infoRow('Nhân viên phục vụ', record['waiterName'] ?? 'Không rõ'),
          _infoRow('Gọi thanh toán', requester),
          if (record['paymentRequestedAt'] != null)
            _infoRow(
              'Thời điểm gọi',
              _dateTime(record['paymentRequestedAt']),
            ),
          _infoRow('Tổng tiền', _money(record['totalAmount'])),
          if (_num(record['depositAmount']) > 0)
            _infoRow('Tiền cọc', _money(record['depositAmount'])),
          if (journey.isNotEmpty) ...[
            const Divider(height: 24),
            const Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'Hành trình bàn',
                style: TextStyle(fontWeight: FontWeight.w800),
              ),
            ),
            const SizedBox(height: 6),
            ...journey.map(
              (item) => Container(
                width: double.infinity,
                margin: const EdgeInsets.only(bottom: 6),
                padding: const EdgeInsets.all(9),
                decoration: BoxDecoration(
                  color: const Color(0xFFFFF7ED),
                  borderRadius: BorderRadius.circular(9),
                ),
                child: Text(
                  '${item['details'] ?? item['operationLabel'] ?? 'Thao tác bàn'}'
                  '\n${_time(item['createdAt'])} • '
                  '${item['actorName'] ?? 'Hệ thống'}',
                ),
              ),
            ),
          ],
          const Divider(height: 24),
          if (batches.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Text('Order này chưa có món.'),
            )
          else
            ...batches.map(_batchCard),
        ],
      ),
    );
  }

  Widget _batchCard(Map<String, dynamic> batch) {
    final List<Map<String, dynamic>> items = _mapList(batch['items']);
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(12),
      ),
      child: ExpansionTile(
        initiallyExpanded: true,
        title: Text(
          'Đợt ${_int(batch['batch'])} • ${_time(batch['calledAt'])}',
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        children: items.map(_itemRow).toList(),
      ),
    );
  }

  Widget _itemRow(Map<String, dynamic> item) {
    final bool deleted = item['isDeleted'] == true;
    final String cancelReason = (item['cancelReason'] ?? '').toString().trim();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      decoration: const BoxDecoration(
        border: Border(
          top: BorderSide(color: Color(0xFFF0F0F0)),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${item['name'] ?? 'Món'} ×${_int(item['quantity'])}',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    color: deleted ? Colors.red : AppTheme.textColor,
                    decoration:
                        deleted ? TextDecoration.lineThrough : null,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  (item['statusLabel'] ?? item['status'] ?? '').toString(),
                  style: TextStyle(
                    fontSize: 12,
                    color: deleted ? Colors.red : Colors.grey.shade700,
                  ),
                ),
                if (cancelReason.isNotEmpty)
                  Text(
                    'Lý do xóa: $cancelReason',
                    style: const TextStyle(
                      fontSize: 12,
                      color: Colors.red,
                    ),
                  ),
              ],
            ),
          ),
          Text(
            _money(_num(item['unitPrice']) * _int(item['quantity'])),
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: deleted ? Colors.red : AppTheme.orangeColor,
            ),
          ),
        ],
      ),
    );
  }

  Widget _infoRow(String label, Object value) {
    return Padding(
      padding: const EdgeInsets.only(top: 5),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 125,
            child: Text(
              label,
              style: TextStyle(color: Colors.grey.shade700),
            ),
          ),
          Expanded(
            child: Text(
              value.toString(),
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF7F7F7),
      appBar: AppBar(
        title: const Text('Nhật ký Order'),
        actions: [
          IconButton(
            onPressed: _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        children: [
          _filters(),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _records.isEmpty
                    ? Center(
                        child: Text(
                          'Không có order trong ngày ${_dateLabel(_date)}',
                          style: TextStyle(color: Colors.grey.shade600),
                        ),
                      )
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView.builder(
                          physics: const AlwaysScrollableScrollPhysics(),
                          itemCount: _records.length,
                          itemBuilder: (_, index) =>
                              _recordCard(_records[index]),
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}
