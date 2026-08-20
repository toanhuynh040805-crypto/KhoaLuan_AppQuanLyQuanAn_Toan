// Khop voi LoginResponse ben Backend (Controllers/AuthController.cs)
class UserProfile {
  final int userId;
  final String fullName;
  final String username;
  final String roleName;

  UserProfile({
    required this.userId,
    required this.fullName,
    required this.username,
    required this.roleName,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      userId: json['userId'],
      fullName: json['fullName'],
      username: json['username'],
      roleName: json['roleName'],
    );
  }
}
