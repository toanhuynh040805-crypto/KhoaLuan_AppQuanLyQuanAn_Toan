using System.Security.Cryptography;
using System.Text;

namespace QuanAnBackend.Services;

// Ham bam mat khau don gian bang SHA256 (du dung cho do an/demo).
// LUU Y: cho san pham that nen doi sang BCrypt.Net-Next hoac ASP.NET Core Identity
// (SHA256 khong co "salt" nen kem an toan hon truoc tan cong rainbow table).
public static class PasswordHasher
{
    public static string Hash(string plainPassword)
    {
        var bytes = Encoding.UTF8.GetBytes(plainPassword);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // chuoi hex chu HOA, vi du "8D969EEF..."
    }

    public static bool Verify(string plainPassword, string storedHash)
    {
        var computed = Hash(plainPassword);
        return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
