import '../models/member_customer.dart';
import 'api_client.dart';

class CustomerService {
  final ApiClient _apiClient;

  const CustomerService({required ApiClient apiClient}) : _apiClient = apiClient;

  Future<List<MemberCustomer>> searchByPhone(String phone) async {
    final dynamic response = await _apiClient.get(
      '/customers/search?phone=${Uri.encodeQueryComponent(phone.trim())}',
    );
    if (response is! List) return const <MemberCustomer>[];
    return response
        .whereType<Map>()
        .map((e) => MemberCustomer.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<MemberCustomer> create({
    required String fullName,
    required String phone,
  }) async {
    final dynamic created = await _apiClient.post(
      '/customers',
      body: {
        'fullName': fullName.trim(),
        'phone': phone.trim(),
        'email': null,
        'dateOfBirth': null,
      },
    );
    if (created is! Map) {
      throw const ApiException(message: 'Không thể tạo thành viên');
    }
    final dynamic rawId = created['id'];
    final int id = rawId is int ? rawId : int.tryParse(rawId?.toString() ?? '') ?? 0;
    if (id <= 0) {
      throw const ApiException(message: 'Không lấy được mã thành viên mới');
    }
    final dynamic detail = await _apiClient.get('/customers/$id');
    if (detail is! Map) {
      throw const ApiException(message: 'Không tải được thành viên vừa tạo');
    }
    return MemberCustomer.fromJson(Map<String, dynamic>.from(detail));
  }

  Future<MemberCustomer?> getForTable(int tableId) async {
    final dynamic response = await _apiClient.get('/mobile/customers/table/$tableId');
    if (response is! Map) return null;
    final dynamic customer = response['customer'];
    if (customer is! Map) return null;
    return MemberCustomer.fromJson(Map<String, dynamic>.from(customer));
  }

  Future<MemberCustomer> attachToTable({
    required int tableId,
    required int customerId,
    required String employeeCode,
  }) async {
    final dynamic response = await _apiClient.put(
      '/mobile/customers/table/$tableId',
      body: {
        'customerId': customerId,
        'employeeCode': employeeCode,
      },
    );
    if (response is! Map || response['customer'] is! Map) {
      throw const ApiException(message: 'Không thể gắn thành viên vào đơn');
    }
    return MemberCustomer.fromJson(
      Map<String, dynamic>.from(response['customer'] as Map),
    );
  }
}
