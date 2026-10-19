class ApiConfig {
  ApiConfig._();

  // Có thể ghi đè khi build/run bằng:
  // --dart-define=API_BASE_URL=http://<host>:5000/api
  //
  // Android Emulator dùng 10.0.2.2 để truy cập localhost
  // của máy Windows chạy backend.
  static const String baseUrl =
      String.fromEnvironment(
    'API_BASE_URL',
    defaultValue:
        'http://10.0.2.2:5000/api',
  );

  // Có thể ghi đè bằng:
  // --dart-define=HUB_URL=http://<host>:5000/hubs/order
  static const String hubUrl =
      String.fromEnvironment(
    'HUB_URL',
    defaultValue:
        'http://10.0.2.2:5000/hubs/order',
  );

  // Backend trả ImageUrl dạng /images/menu/xxx.jpg. Hàm này đổi đường dẫn
  // tương đối thành URL tuyệt đối cùng host với API để Image.network dùng được
  // trên Android emulator, thiết bị thật và môi trường deploy.
  static String resolveAssetUrl(String? value) {
    final String raw = value?.trim() ?? '';
    if (raw.isEmpty) return '';

    final Uri? absolute = Uri.tryParse(raw);
    if (absolute != null && absolute.hasScheme) {
      return absolute.toString();
    }

    final Uri apiUri = Uri.parse(baseUrl);
    final String path = raw.startsWith('/') ? raw : '/$raw';
    return apiUri.resolve(path).toString();
  }

  static const Duration connectTimeout =
      Duration(seconds: 15);

  static const Duration receiveTimeout =
      Duration(seconds: 15);
}
