import '../models/menu_item.dart';
import 'api_client.dart';

class MenuService {
  final ApiClient _apiClient;

  const MenuService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<List<MenuItem>> getMenuItems() async {
    final dynamic response =
        await _apiClient.get('/menu?availableOnly=true');

    dynamic data = response;

    if (response is Map) {
      data =
          response['data'] ??
          response['items'] ??
          response['menuItems'] ??
          [];
    }

    if (data is! List) {
      return [];
    }

    return data
        .whereType<Map>()
        .map(
          (item) => MenuItem.fromJson(
            Map<String, dynamic>.from(item),
          ),
        )
        .toList();
  }

  /// Danh mục Mobile được suy ra trực tiếp từ các món ĐANG BÁN của backend
  /// dùng chung. Không dùng bảng Categories độc lập vì DB cũ có thể còn các
  /// nhóm demo/nhóm lịch sử mà WinForms hiện không còn phục vụ.
  Future<List<String>> getCategories() async {
    final List<MenuItem> items = await getMenuItems();
    final List<String> result = <String>[];
    final Set<String> seen = <String>{};

    for (final MenuItem item in items) {
      final String category = item.category.trim();
      if (category.isEmpty) continue;

      final String key = category.toLowerCase();
      if (seen.add(key)) {
        result.add(category);
      }
    }

    return result;
  }
}
