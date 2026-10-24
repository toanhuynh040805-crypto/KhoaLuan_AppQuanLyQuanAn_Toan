import '../models/staff_management_models.dart';
import 'api_client.dart';

class StaffManagementService {
  final ApiClient _apiClient;

  const StaffManagementService({required ApiClient apiClient})
      : _apiClient = apiClient;

  Future<List<ManagedEmployee>> getEmployees() async {
    final dynamic response = await _apiClient.get('/users');
    if (response is! List) return <ManagedEmployee>[];
    return response
        .whereType<Map>()
        .map((e) => ManagedEmployee.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<List<ManagedShift>> getShifts() async {
    final dynamic response = await _apiClient.get('/shifts');
    if (response is! List) return <ManagedShift>[];
    return response
        .whereType<Map>()
        .map((e) => ManagedShift.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<List<ManagedArea>> getAreas() async {
    final dynamic response = await _apiClient.get('/areas');
    if (response is! List) return <ManagedArea>[];
    return response
        .whereType<Map>()
        .map((e) => ManagedArea.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<List<ManagedShiftAssignment>> getAssignments(DateTime date) async {
    final dynamic response = await _apiClient.get(
      '/shifts/assignments?date=${_date(date)}',
    );
    return _parseAssignments(response);
  }

  Future<void> assignShift({
    required int userId,
    required int shiftId,
    required DateTime workDate,
    int? areaId,
  }) async {
    await _apiClient.post(
      '/shifts/assignments',
      body: {
        'userId': userId,
        'shiftId': shiftId,
        'workDate': _date(workDate),
        'areaId': areaId,
      },
    );
  }

  Future<void> updateAssignmentStatus(int id, int status) async {
    await _apiClient.putValue('/shifts/assignments/$id/status', status);
  }

  Future<void> deleteAssignment(int id) async {
    await _apiClient.delete('/shifts/assignments/$id');
  }

  Future<List<ManagedStaffTask>> getTasks({int? userId}) async {
    final String path =
        userId == null ? '/stafftasks' : '/stafftasks?userId=$userId';
    final dynamic response = await _apiClient.get(path);
    if (response is! List) return <ManagedStaffTask>[];
    return response
        .whereType<Map>()
        .map((e) => ManagedStaffTask.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<void> createTask({
    required String title,
    required String? description,
    required int assignedToUserId,
    required DateTime? dueAt,
  }) async {
    await _apiClient.post(
      '/stafftasks',
      body: {
        'title': title,
        'description': description,
        'assignedToUserId': assignedToUserId,
        'dueAt': dueAt?.toIso8601String(),
      },
    );
  }

  Future<void> updateTask({
    required int id,
    required String title,
    required String? description,
    required int assignedToUserId,
    required DateTime? dueAt,
  }) async {
    await _apiClient.put(
      '/stafftasks/$id',
      body: {
        'title': title,
        'description': description,
        'assignedToUserId': assignedToUserId,
        'dueAt': dueAt?.toIso8601String(),
      },
    );
  }

  Future<void> updateTaskStatus(int id, int status) async {
    await _apiClient.putValue('/stafftasks/$id/status', status);
  }

  Future<void> deleteTask(int id) async {
    await _apiClient.delete('/stafftasks/$id');
  }

  Future<List<ManagedShiftAssignment>> getHistory({
    int? userId,
    required DateTime from,
    required DateTime to,
  }) async {
    String path =
        '/shifts/assignments/history?from=${_date(from)}&to=${_date(to)}';
    if (userId != null) path += '&userId=$userId';
    final dynamic response = await _apiClient.get(path);
    return _parseAssignments(response);
  }

  List<ManagedShiftAssignment> _parseAssignments(dynamic response) {
    if (response is! List) return <ManagedShiftAssignment>[];
    return response
        .whereType<Map>()
        .map(
          (e) => ManagedShiftAssignment.fromJson(
            Map<String, dynamic>.from(e),
          ),
        )
        .toList();
  }

  static String _date(DateTime value) {
    final String y = value.year.toString().padLeft(4, '0');
    final String m = value.month.toString().padLeft(2, '0');
    final String d = value.day.toString().padLeft(2, '0');
    return '$y-$m-$d';
  }
}
