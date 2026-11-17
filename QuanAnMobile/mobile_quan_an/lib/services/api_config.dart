class ApiConfig {
  ApiConfig._();

  static String _serverHost = '10.0.2.2';
  static int _serverPort = 5000;

  static String get serverHost => _serverHost;
  static int get serverPort => _serverPort;

  static String get serverOrigin =>
      'http://$_serverHost:$_serverPort';

  static String get baseUrl =>
      '$serverOrigin/api';

  static String get hubUrl =>
      '$serverOrigin/hubs/order';

  static void configureServer({
    required String host,
    int port = 5000,
  }) {
    final String normalizedHost = host.trim();
    if (normalizedHost.isEmpty) {
      throw ArgumentError.value(
        host,
        'host',
        'Địa chỉ máy chủ không được để trống',
      );
    }
    if (port <= 0 || port > 65535) {
      throw ArgumentError.value(
        port,
        'port',
        'Cổng máy chủ không hợp lệ',
      );
    }

    _serverHost = normalizedHost;
    _serverPort = port;
  }

  // Backend trả ImageUrl dạng /images/menu/xxx.jpg. Hàm này đổi đường dẫn
  // tương đối thành URL tuyệt đối cùng host với API để Image.network dùng được
  // trên emulator, thiết bị thật và mọi mạng LAN.
  static String resolveAssetUrl(String? value) {
    final String raw = value?.trim() ?? '';
    if (raw.isEmpty) return '';

    final Uri? absolute = Uri.tryParse(raw);
    if (absolute != null && absolute.hasScheme) {
      return absolute.toString();
    }

    final Uri serverUri = Uri.parse(serverOrigin);
    final String path = raw.startsWith('/') ? raw : '/$raw';
    return serverUri.resolve(path).toString();
  }

  static const Duration connectTimeout =
      Duration(seconds: 15);

  static const Duration receiveTimeout =
      Duration(seconds: 15);
}
