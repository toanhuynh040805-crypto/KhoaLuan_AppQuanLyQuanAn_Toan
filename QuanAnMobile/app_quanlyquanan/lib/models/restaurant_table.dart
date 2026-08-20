// Khop voi RestaurantTable ben Backend, mo rong them vai truong hien thi
// (elapsedMinutes, totalAmount...) de khop voi thiet ke Stitch.
// TODO: cac truong mo rong nay chua co san trong response cua Backend hien tai,
// can bo sung endpoint tra ve du lieu tinh toan san (hoac tinh toan phia Flutter
// tu OrderItems neu Backend tra kem).
enum TableStatus { trong, dangDung, daDat }

TableStatus tableStatusFromInt(int value) {
  switch (value) {
    case 1:
      return TableStatus.dangDung;
    case 2:
      return TableStatus.daDat;
    default:
      return TableStatus.trong;
  }
}

class RestaurantTable {
  final int id;
  final String tableName;
  final int capacity;
  final TableStatus status;

  // Cac truong hien thi them (tuy chon, co the null neu Backend chua tra ve)
  final int? elapsedMinutes;      // "45p", "1h 15p"
  final double? totalAmount;      // "1,250,000 d"
  final String? reservationTime;  // "19:30"
  final String? reservationName;  // "Anh Tu"
  final bool isWaitingFood;       // hien "Cho mon (15p)" vien do canh trai

  RestaurantTable({
    required this.id,
    required this.tableName,
    required this.capacity,
    required this.status,
    this.elapsedMinutes,
    this.totalAmount,
    this.reservationTime,
    this.reservationName,
    this.isWaitingFood = false,
  });

  factory RestaurantTable.fromJson(Map<String, dynamic> json) {
    return RestaurantTable(
      id: json['id'],
      tableName: json['tableName'],
      capacity: json['capacity'],
      status: tableStatusFromInt(json['status']),
      elapsedMinutes: json['elapsedMinutes'],
      totalAmount: (json['totalAmount'] as num?)?.toDouble(),
      reservationTime: json['reservationTime'],
      reservationName: json['reservationName'],
      isWaitingFood: json['isWaitingFood'] ?? false,
    );
  }
}
