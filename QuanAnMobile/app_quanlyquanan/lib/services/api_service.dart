import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/restaurant_table.dart';
import '../models/menu_item.dart';
import '../models/cart_item.dart';
import '../models/user_profile.dart';
import 'api_config.dart';

// Toàn bộ giao tiếp REST với Backend đi qua đây, để UI không cần biết chi tiết http
class ApiService {
  Future<UserProfile> login(String username, String password) async {
    final res = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'username': username, 'password': password}),
    );
    if (res.statusCode != 200) throw Exception('Sai tai khoan hoac mat khau');
    return UserProfile.fromJson(jsonDecode(res.body));
  }

  Future<List<RestaurantTable>> getTables() async {
    final res = await http.get(Uri.parse('${ApiConfig.baseUrl}/api/tables'));
    if (res.statusCode != 200) throw Exception('Khong tai duoc so do ban');
    final List data = jsonDecode(res.body);
    return data.map((e) => RestaurantTable.fromJson(e)).toList();
  }

  Future<List<MenuItem>> getMenu() async {
    final res = await http.get(Uri.parse('${ApiConfig.baseUrl}/api/menu'));
    if (res.statusCode != 200) throw Exception('Khong tai duoc thuc don');
    final List data = jsonDecode(res.body);
    return data.map((e) => MenuItem.fromJson(e)).toList();
  }

  // Gửi các món trong giỏ lên server -> kích hoạt thuật toán phân luồng bên Backend
  Future<int> submitOrder({
    required int tableId,
    required int userId,
    required List<CartItem> items,
  }) async {
    final body = jsonEncode({
      'tableId': tableId,
      'userId': userId,
      'items': items
          .map((c) => {
                'menuItemId': c.menuItem.id,
                'quantity': c.quantity,
                'note': c.note,
              })
          .toList(),
    });

    final res = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/orders/add-items'),
      headers: {'Content-Type': 'application/json'},
      body: body,
    );

    if (res.statusCode != 200) throw Exception('Gui order that bai');
    return jsonDecode(res.body)['orderId'];
  }
}
