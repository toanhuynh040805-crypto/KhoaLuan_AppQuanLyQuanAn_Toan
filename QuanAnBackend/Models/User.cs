namespace QuanAnBackend.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
