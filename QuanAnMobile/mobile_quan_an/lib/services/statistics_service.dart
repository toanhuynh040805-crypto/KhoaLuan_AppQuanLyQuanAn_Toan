import '../models/statistics_data.dart';
import 'api_client.dart';

class StatisticsService {
  final ApiClient apiClient;

  const StatisticsService({required this.apiClient});

  Future<StatisticsDashboard> getDashboard({
    required DateTime from,
    required DateTime to,
  }) async {
    final dynamic raw = await apiClient.get(
      '/mobile/statistics?from=${_date(from)}&to=${_date(to)}',
    );
    if (raw is! Map) {
      throw const ApiException(message: 'Dữ liệu thống kê không hợp lệ');
    }
    return StatisticsDashboard.fromJson(Map<String, dynamic>.from(raw));
  }

  String _date(DateTime value) {
    final String m = value.month.toString().padLeft(2, '0');
    final String d = value.day.toString().padLeft(2, '0');
    return '${value.year}-$m-$d';
  }
}
