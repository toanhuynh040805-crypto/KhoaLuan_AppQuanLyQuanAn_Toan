import 'api_client.dart';

class AreaAccessInfo {
  final bool restricted;
  final Set<int> areaIds;

  const AreaAccessInfo({
    required this.restricted,
    required this.areaIds,
  });

  bool canOperateArea(int? areaId) {
    if (!restricted) return true;
    if (areaId == null) return false;
    return areaIds.contains(areaId);
  }
}

class AreaAssignmentService {
  final ApiClient _apiClient;

  const AreaAssignmentService({
    required ApiClient apiClient,
  }) : _apiClient = apiClient;

  Future<AreaAccessInfo> getMyAccess() async {
    final dynamic response = await _apiClient.get(
      '/area-assignments/me',
    );

    if (response is! Map) {
      return const AreaAccessInfo(
        restricted: false,
        areaIds: <int>{},
      );
    }

    final bool restricted = response['restricted'] == true;
    final Set<int> areaIds = <int>{};
    final dynamic rawIds = response['areaIds'];

    if (rawIds is List) {
      for (final dynamic raw in rawIds) {
        final int? parsed = raw is int
            ? raw
            : int.tryParse(raw?.toString() ?? '');
        if (parsed != null && parsed > 0) {
          areaIds.add(parsed);
        }
      }
    }

    return AreaAccessInfo(
      restricted: restricted,
      areaIds: areaIds,
    );
  }
}
