using System.Drawing;

namespace QuanAnWinForm.Theme;

// Font Inter khong co san mac dinh tren Windows, dung Segoe UI (font he thong
// gan giong nhat ve do ro net va x-height cao) de tranh phai nhung font ngoai.
public static class AppFonts
{
    private const string FamilyName = "Segoe UI";

    public static Font HeadlineLg => new(FamilyName, 22, FontStyle.Bold);
    public static Font HeadlineMd => new(FamilyName, 16, FontStyle.Bold);
    public static Font HeadlineSm => new(FamilyName, 13, FontStyle.Bold);
    public static Font BodyLg => new(FamilyName, 11, FontStyle.Regular);
    public static Font BodyMd => new(FamilyName, 10, FontStyle.Regular);
    public static Font LabelLg => new(FamilyName, 10, FontStyle.Bold);
    public static Font LabelMd => new(FamilyName, 8.5f, FontStyle.Bold);
}
