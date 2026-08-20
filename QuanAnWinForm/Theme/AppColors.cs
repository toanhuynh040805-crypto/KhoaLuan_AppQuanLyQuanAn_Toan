using System.Drawing;

namespace QuanAnWinForm.Theme;

// Mau lay dung tu Design System "Culinary Operations System" (Stitch AI)
// Terracotta (cam dat nung) lam primary, Navy lam secondary quan ly/dieu huong.
public static class AppColors
{
    public static readonly Color Primary = ColorTranslator.FromHtml("#AC2D00");
    public static readonly Color PrimaryContainer = ColorTranslator.FromHtml("#D53E0B");
    public static readonly Color OnPrimary = Color.White;

    public static readonly Color Secondary = ColorTranslator.FromHtml("#546067");
    public static readonly Color SecondaryContainer = ColorTranslator.FromHtml("#D7E4EC");

    public static readonly Color Tertiary = ColorTranslator.FromHtml("#00685C");
    public static readonly Color TertiaryContainer = ColorTranslator.FromHtml("#198374");

    public static readonly Color Error = ColorTranslator.FromHtml("#BA1A1A");
    public static readonly Color ErrorContainer = ColorTranslator.FromHtml("#FFDAD6");
    public static readonly Color Warning = ColorTranslator.FromHtml("#F59E0B");
    public static readonly Color Success = ColorTranslator.FromHtml("#198374");

    public static readonly Color Background = ColorTranslator.FromHtml("#F9F9F9");
    public static readonly Color SurfaceLowest = Color.White;
    public static readonly Color SurfaceLow = ColorTranslator.FromHtml("#F3F3F3");
    public static readonly Color SurfaceContainer = ColorTranslator.FromHtml("#EEEEEE");
    public static readonly Color SurfaceContainerHigh = ColorTranslator.FromHtml("#E8E8E8");

    public static readonly Color OnSurface = ColorTranslator.FromHtml("#1A1C1C");
    public static readonly Color OnSurfaceVariant = ColorTranslator.FromHtml("#5B4139");
    public static readonly Color Outline = ColorTranslator.FromHtml("#8F7068");
    public static readonly Color OutlineVariant = ColorTranslator.FromHtml("#E0E0E0");

    // Trang thai ban (dung trong Table Management)
    public static readonly Color TableTrong = ColorTranslator.FromHtml("#198374");   // xanh - trong
    public static readonly Color TableCoKhach = ColorTranslator.FromHtml("#AC2D00"); // terracotta - co khach
    public static readonly Color TableChoThanhToan = ColorTranslator.FromHtml("#00685C");
    public static readonly Color TableDonDep = ColorTranslator.FromHtml("#F59E0B");  // amber - don dep

    // Sidebar rieng (navy toi)
    public static readonly Color SidebarBg = Color.White;
    public static readonly Color SidebarSelectedBg = ColorTranslator.FromHtml("#AC2D00");
    public static readonly Color SidebarText = ColorTranslator.FromHtml("#3D3D3A");
}
