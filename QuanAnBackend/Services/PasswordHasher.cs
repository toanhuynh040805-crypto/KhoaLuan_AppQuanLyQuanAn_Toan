using BCrypt.Net;

namespace QuanAnBackend.Services;

// Bam mat khau bang BCrypt: tu sinh salt rieng cho tung user, cost factor = 11
// (tang so nay len se cham hon nhung an toan hon truoc brute-force).
public static class PasswordHasher
{
    private const int WorkFactor = 11;

    public static string Hash(string plainPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);

    public static bool Verify(string plainPassword, string storedHash)
        => BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
}