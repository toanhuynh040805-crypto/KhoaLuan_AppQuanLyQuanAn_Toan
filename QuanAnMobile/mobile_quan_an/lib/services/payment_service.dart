import '../models/payment_method_option.dart';
import '../models/payment_preview.dart';
import 'api_client.dart';

class PaymentService {
  final ApiClient _apiClient;

  const PaymentService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<void> requestPayment({
    required int tableId,
    required String employeeCode,
  }) async {
    await _apiClient.post(
      '/payments/request',
      body: {
        'tableId': tableId,
        'employeeCode': employeeCode,
      },
    );
  }

  /// Lấy phương thức thanh toán từ backend dùng chung.
  /// Mobile không tự khai báo Tiền mặt/QR/Thẻ/Ví điện tử nữa.
  Future<List<PaymentMethodOption>> getPaymentMethods() async {
    final dynamic response = await _apiClient.get(
      '/client-options/payment-methods',
    );

    dynamic data = response;
    if (response is Map) {
      data = response['data'] ?? response['methods'] ?? [];
    }

    if (data is! List) {
      return <PaymentMethodOption>[];
    }

    return data
        .whereType<Map>()
        .map(
          (item) => PaymentMethodOption.fromJson(
            Map<String, dynamic>.from(item),
          ),
        )
        .where(
          (item) => item.code.isNotEmpty && item.label.isNotEmpty,
        )
        .toList();
  }

  Future<Set<int>> getPendingTableIds() async {
    final dynamic response = await _apiClient.get(
      '/mobile/payments/pending',
    );

    if (response is! List) {
      return <int>{};
    }

    final Set<int> result = <int>{};

    for (final dynamic raw in response) {
      if (raw is! Map) continue;

      final dynamic value = raw['tableId'];
      final int? tableId = value is int
          ? value
          : int.tryParse(value?.toString() ?? '');

      if (tableId != null && tableId > 0) {
        result.add(tableId);
      }
    }

    return result;
  }

  Future<PaymentPreview> getPreview({
    required int tableId,
  }) async {
    final dynamic response = await _apiClient.get(
      '/payments/preview/table/$tableId',
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể tải thông tin hóa đơn',
      );
    }

    return PaymentPreview.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<int> _resolvePaymentMethod(Object paymentMethod) async {
    if (paymentMethod is int) {
      return paymentMethod;
    }

    // Tương thích màn chi tiết cũ còn trả về code dạng String, nhưng vẫn
    // tra code đó trong danh sách backend thay vì hard-code mapping ở Mobile.
    final String code = paymentMethod.toString().trim();
    final List<PaymentMethodOption> methods = await getPaymentMethods();

    for (final PaymentMethodOption option in methods) {
      if (option.code.toLowerCase() == code.toLowerCase()) {
        return option.method;
      }
    }

    throw const ApiException(
      message: 'Hình thức thanh toán không còn được hệ thống hỗ trợ',
    );
  }

  Future<Map<String, dynamic>> completePayment({
    required int tableId,
    required String employeeCode,
    required Object paymentMethod,
    String? transactionRef,
  }) async {
    final PaymentPreview preview =
        await getPreview(tableId: tableId);
    final int method = await _resolvePaymentMethod(paymentMethod);

    final dynamic response = await _apiClient.post(
      '/payments',
      body: {
        'orderId': preview.orderId,
        'method': method,
        'transactionRef': transactionRef,
        // Thanh toán hoàn tất ở backend chung nhưng email được giữ lại để
        // WinForms/Thu ngân có thể bấm Gửi email sau khi kiểm tra hóa đơn.
        'deferEmailToWinForms': true,
      },
    );

    if (response is! Map) {
      throw const ApiException(
        message: 'Không thể hoàn tất thanh toán',
      );
    }

    // Backend /payments đã hoàn tất hóa đơn và trả bàn về Trống.
    // Không gọi /tables/{id}/release lần thứ hai vì sẽ phát thêm event trạng
    // thái bàn, làm màn danh sách phía dưới rebuild đúng lúc route đang đóng.

    // showDialog hoàn tất Future ngay khi Navigator.pop được gọi, trong khi
    // animation đóng dialog vẫn cần thêm một nhịp để gỡ xong các Element.
    // Chờ ngắn trước khi màn chi tiết tự pop để hai route không teardown chồng.
    await Future<void>.delayed(
      const Duration(milliseconds: 360),
    );

    return Map<String, dynamic>.from(response);
  }
}
