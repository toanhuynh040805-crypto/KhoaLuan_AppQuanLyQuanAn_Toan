// TODO: đổi thành IP máy chạy Backend trong mạng LAN quán ăn
// Vi du: 'http://192.168.1.10:5000'
class ApiConfig {
  static const String baseUrl = 'http://10.0.2.2:5000'; // 10.0.2.2 = localhost khi chay Android Emulator
  static const String hubUrl = '$baseUrl/hubs/order';
}
