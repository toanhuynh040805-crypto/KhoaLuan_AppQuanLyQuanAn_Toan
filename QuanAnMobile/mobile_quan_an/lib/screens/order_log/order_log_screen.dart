import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class OrderLogScreen extends StatefulWidget {
  final int tableId;
  final String tableName;

  const OrderLogScreen({
    super.key,
    required this.tableId,
    required this.tableName,
  });

  @override
  State<OrderLogScreen> createState() => _OrderLogScreenState();
}

class _OrderLogScreenState extends State<OrderLogScreen> {
  bool _loading = true;
  int? _orderId;
  List<Map<String, dynamic>> _events = <Map<String, dynamic>>[];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (mounted) setState(() => _loading = true);
    try {
      final Map<String, dynamic> data =
          await AppServices.referenceOpsService.getOrderLog(tableId: widget.tableId);
      final dynamic rawEvents = data['events'];
      if (!mounted) return;
      setState(() {
        _orderId = int.tryParse(data['orderId']?.toString() ?? '');
        _events = rawEvents is List
            ? rawEvents
                .whereType<Map>()
                .map((item) => Map<String, dynamic>.from(item))
                .toList()
            : <Map<String, dynamic>>[];
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      TopMessage.show(context, message: error.message, type: TopMessageType.error);
    }
  }

  String _time(dynamic value) {
    final DateTime? parsed = DateTime.tryParse(value?.toString() ?? '');
    if (parsed == null) return '--:--';
    return '${parsed.hour.toString().padLeft(2, '0')}:'
        '${parsed.minute.toString().padLeft(2, '0')}:'
        '${parsed.second.toString().padLeft(2, '0')}';
  }

  Color _color(String status) {
    if (status.contains('hủy')) return Colors.red;
    if (status.contains('phục vụ')) return Colors.green;
    if (status.contains('xong')) return Colors.blue;
    if (status.contains('chế biến')) return AppTheme.orangeColor;
    return Colors.blueGrey;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF6F7F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: Text(
          'Nhật ký Order • ${widget.tableName}',
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: _events.isEmpty
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: const [
                        SizedBox(height: 180),
                        Icon(Icons.history, size: 54, color: Colors.grey),
                        SizedBox(height: 10),
                        Center(child: Text('Chưa có lịch sử Order')),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(14),
                      itemCount: _events.length + 1,
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        if (index == 0) {
                          return Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: Text(
                              _orderId == null
                                  ? widget.tableName
                                  : '${widget.tableName} • Order #$_orderId',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                          );
                        }

                        final Map<String, dynamic> event = _events[index - 1];
                        final String status = event['status']?.toString() ?? '';
                        final Color color = _color(status.toLowerCase());
                        return Container(
                          padding: const EdgeInsets.all(14),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(14),
                            border: Border.all(color: Colors.grey.shade200),
                          ),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Container(
                                width: 10,
                                height: 10,
                                margin: const EdgeInsets.only(top: 5),
                                decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      '${event['itemName'] ?? 'Món'}  x${event['quantity'] ?? 1}',
                                      style: const TextStyle(fontWeight: FontWeight.bold),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      '$status • ${_time(event['changedAt'])}',
                                      style: TextStyle(color: color, fontWeight: FontWeight.w600),
                                    ),
                                    const SizedBox(height: 3),
                                    Text(
                                      'Thực hiện: ${event['changedBy'] ?? 'Hệ thống'}',
                                      style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
                                    ),
                                    if ((event['note']?.toString().trim() ?? '').isNotEmpty) ...[
                                      const SizedBox(height: 3),
                                      Text(
                                        'Ghi chú: ${event['note']}',
                                        style: TextStyle(
                                          color: Colors.grey.shade600,
                                          fontStyle: FontStyle.italic,
                                          fontSize: 12,
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
