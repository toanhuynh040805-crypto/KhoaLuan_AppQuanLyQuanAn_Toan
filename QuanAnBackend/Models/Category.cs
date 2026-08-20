namespace QuanAnBackend.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
