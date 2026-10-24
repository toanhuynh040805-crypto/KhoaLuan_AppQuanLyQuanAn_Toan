using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Khôi phục/đồng bộ ảnh thực đơn dùng chung cho WinForms, Web và Mobile.
/// Bộ ảnh vật lý trong repo mới giữ ID từ catalog WinForms cũ, trong khi ID
/// MenuItem trong DB tích hợp có thể khác. Vì vậy phải map theo TÊN MÓN,
/// không được suy ra ảnh từ MenuItemId.
/// </summary>
public sealed class BundledMenuImageService
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    // Giá trị = ID nằm trong tên file ảnh vật lý menu-{id}-{hash}.ext.
    // Nhóm 5..29 của DB tích hợp bị lệch so với bộ ảnh gốc:
    // ảnh 5 là Khăn lạnh; ảnh 6..29 lần lượt là Lẩu -> Mì gói.
    private static readonly Dictionary<string, int> CatalogImageIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeName("Lẩu bò 2-3 người (Thường)")] = 6,
            [NormalizeName("Lẩu bò 2-3 người (Đặc biệt)")] = 7,
            [NormalizeName("Lẩu bò 4-5 người (Thường)")] = 8,
            [NormalizeName("Lẩu bò 4-5 người (Đặc biệt)")] = 9,
            [NormalizeName("Lẩu bò 6-8 người (Thường)")] = 10,
            [NormalizeName("Lẩu bò 6-8 người (Đặc biệt)")] = 11,

            [NormalizeName("Bò viên chiên")] = 12,
            [NormalizeName("Chân gà sả tắc")] = 13,
            [NormalizeName("Gỏi lưỡi bò")] = 14,
            [NormalizeName("Gỏi bò + khoai môn")] = 15,
            [NormalizeName("Bò tơ + rau rừng")] = 16,
            [NormalizeName("Bò tơ cháy mè")] = 17,
            [NormalizeName("Bò một nắng đặc biệt")] = 18,

            [NormalizeName("Bò viên thêm (nhỏ)")] = 19,
            [NormalizeName("Bò viên thêm (lớn)")] = 20,
            [NormalizeName("Thịt thêm (nhỏ)")] = 21,
            [NormalizeName("Thịt thêm (lớn)")] = 22,
            [NormalizeName("Gân thêm (nhỏ)")] = 23,
            [NormalizeName("Gân thêm (lớn)")] = 24,
            [NormalizeName("Đậu hủ ky")] = 25,
            [NormalizeName("Đậu hủ non")] = 26,
            [NormalizeName("Bánh phở")] = 27,
            [NormalizeName("Mì vắt")] = 28,
            [NormalizeName("Mì gói")] = 29,
            [NormalizeName("Khăn lạnh")] = 5,

            [NormalizeName("Trà tắc mật ong đường phèn")] = 30,
            [NormalizeName("Pepsi")] = 31,
            [NormalizeName("Coca")] = 32,
            [NormalizeName("7up")] = 33,
            [NormalizeName("Sting")] = 34,
            [NormalizeName("Xá xị")] = 35,
            [NormalizeName("Nước suối")] = 36,
            [NormalizeName("Ken bạc")] = 37,
            [NormalizeName("Ken xanh")] = 38,
            [NormalizeName("Ken lùn")] = 39,
            [NormalizeName("Tiger nâu - bạc")] = 40,
            [NormalizeName("Bia hơi")] = 41,
        };

    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;

    public BundledMenuImageService(
        IWebHostEnvironment env,
        AppDbContext db)
    {
        _env = env;
        _db = db;
    }

    public async Task EnsureAsync()
    {
        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var destinationFolder = Path.Combine(webRoot, "images", "menu");
        Directory.CreateDirectory(destinationFolder);

        var archivePath = Path.Combine(
            _env.ContentRootPath,
            "SeedAssets",
            "menu-images.zip"
        );

        if (File.Exists(archivePath))
        {
            await RestoreFromZipAsync(archivePath, destinationFolder);
        }
        else
        {
            Console.WriteLine(
                $"[MenuImages] Không có menu-images.zip; dùng ảnh hiện có trong {destinationFolder}"
            );
        }

        var repaired = await RepairImageUrlsAsync(destinationFolder);

        Console.WriteLine(
            $"[MenuImages] Đồng bộ xong; sửa {repaired} ImageUrl theo đúng tên món."
        );
    }

    private static async Task RestoreFromZipAsync(
        string archivePath,
        string destinationFolder)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        var restored = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var safeName = Path.GetFileName(entry.Name);
            if (string.IsNullOrWhiteSpace(safeName))
                continue;

            var extension = Path.GetExtension(safeName).ToLowerInvariant();
            if (!ImageExtensions.Contains(extension))
                continue;

            var targetPath = Path.Combine(destinationFolder, safeName);
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                continue;

            await using var input = entry.Open();
            await using var output = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            await input.CopyToAsync(output);
            restored++;
        }

        Console.WriteLine(
            $"[MenuImages] ZIP có {archive.Entries.Count} entry; khôi phục mới {restored} file."
        );
    }

    private async Task<int> RepairImageUrlsAsync(string destinationFolder)
    {
        var items = await _db.MenuItems.ToListAsync();
        var repaired = 0;

        foreach (var item in items)
        {
            var raw = item.ImageUrl?.Trim() ?? string.Empty;

            // Ảnh ngoài/CDN do quản lý cấu hình thì không can thiệp.
            if (Uri.TryCreate(raw, UriKind.Absolute, out _))
                continue;

            var normalizedName = NormalizeName(item.ItemName);

            // Với catalog WinForms chuẩn, tên món là nguồn sự thật để tìm ảnh.
            if (CatalogImageIds.TryGetValue(
                    normalizedName,
                    out var physicalImageId))
            {
                var expected = FindPhysicalImage(
                    destinationFolder,
                    physicalImageId
                );

                if (expected is null)
                {
                    Console.WriteLine(
                        $"[MenuImages] Thiếu ảnh gốc cho '{item.ItemName}' " +
                        $"(menu-{physicalImageId}-*)."
                    );
                    continue;
                }

                var expectedUrl =
                    $"/images/menu/{Path.GetFileName(expected)}";

                if (!string.Equals(
                        raw,
                        expectedUrl,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"[MenuImages] {item.ItemName}: " +
                        $"{(string.IsNullOrWhiteSpace(raw) ? "(trống)" : raw)} " +
                        $"-> {expectedUrl}"
                    );
                    item.ImageUrl = expectedUrl;
                    repaired++;
                }

                continue;
            }

            // Món ngoài catalog chuẩn: nếu ảnh hiện tại vẫn tồn tại thì giữ lại.
            if (LocalImageExists(destinationFolder, raw))
                continue;

            // Tương thích dữ liệu tùy chỉnh cũ: chỉ khi URL mất file mới thử
            // tìm ảnh theo MenuItemId.
            var fallback = FindPhysicalImage(
                destinationFolder,
                item.MenuItemId
            );

            if (fallback is null)
                continue;

            var fallbackUrl =
                $"/images/menu/{Path.GetFileName(fallback)}";

            if (!string.Equals(
                    raw,
                    fallbackUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                item.ImageUrl = fallbackUrl;
                repaired++;
            }
        }

        if (repaired > 0)
            await _db.SaveChangesAsync();

        return repaired;
    }

    private static string? FindPhysicalImage(
        string destinationFolder,
        int imageId)
    {
        var prefix = $"menu-{imageId}-";

        return Directory
            .EnumerateFiles(destinationFolder)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                var extension = Path.GetExtension(path).ToLowerInvariant();

                return fileName.StartsWith(
                           prefix,
                           StringComparison.OrdinalIgnoreCase)
                       && ImageExtensions.Contains(extension)
                       && new FileInfo(path).Length > 0;
            })
            .OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool LocalImageExists(
        string destinationFolder,
        string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        var fileName = Path.GetFileName(
            imageUrl.Replace('/', Path.DirectorySeparatorChar)
        );

        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var path = Path.Combine(destinationFolder, fileName);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(
            " ",
            value
                .Trim()
                .Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries)
        );
    }
}
