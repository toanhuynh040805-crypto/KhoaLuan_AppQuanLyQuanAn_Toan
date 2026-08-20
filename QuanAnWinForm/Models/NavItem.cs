using System.Linq;

namespace QuanAnWinForm.Models;

// Dai dien 1 muc trong Sidebar. AllowedRoles rong = tat ca role deu thay duoc.
// Day chinh la co che loc menu theo Role da thong nhat (Huong 1: 1 app dung chung).
public class NavItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string IconGlyph { get; set; } = ""; // dung ky tu Unicode/Segoe MDL2 lam icon don gian
    public string[] AllowedRoles { get; set; } = System.Array.Empty<string>();

    public bool IsVisibleFor(string role) => AllowedRoles.Length == 0 || AllowedRoles.Contains(role);
}
