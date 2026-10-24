using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Khôi phục/đồng bộ ảnh thực đơn dùng chung cho WinForms, Web và Mobile.
/// Không ghi đè ảnh đang tồn tại hợp lệ. Nếu DB còn trỏ tới tên file cũ
/// không còn trong source mới, tự tìm ảnh vật lý cùng MenuItemId và sửa ImageUrl.
/// </summary>
public sealed class BundledMenuImageService
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

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

        // Một số bản source cũ có gói ZIP ảnh seed. Nếu có thì chỉ khôi phục
        // file còn thiếu; không ghi đè ảnh đã upload sau này.
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

        // Repo tích hợp mới có ảnh thật nhưng hash/tên file có thể khác ImageUrl
        // cũ trong database. Chỉ sửa URL nếu file DB đang trỏ tới bị thiếu.
        var repaired = await RepairBrokenImageUrlsAsync(destinationFolder);

        Console.WriteLine(
            $"[MenuImages] Đồng bộ xong; sửa {repaired} ImageUrl bị lệch tên file."
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

    private async Task<int> RepairBrokenImageUrlsAsync(
        string destinationFolder)
    {
        var items = await _db.MenuItems.ToListAsync();
        var repaired = 0;

        foreach (var item in items)
        {
            var raw = item.ImageUrl?.Trim() ?? string.Empty;

            // URL tuyệt đối (CDN/ảnh ngoài) không thuộc kho local thì giữ nguyên.
            if (Uri.TryCreate(raw, UriKind.Absolute, out _))
                continue;

            // Nếu URL hiện tại đã trỏ tới file thật đang tồn tại thì giữ nguyên.
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var currentFileName = Path.GetFileName(
                    raw.Replace('/', Path.DirectorySeparatorChar)
                );
                var currentPath = Path.Combine(
                    destinationFolder,
                    currentFileName
                );

                if (File.Exists(currentPath) &&
                    new FileInfo(currentPath).Length > 0)
                {
                    continue;
                }
            }

            // Ảnh của hệ thống có dạng menu-{MenuItemId}-{hash}.ext.
            // Khi repo mới đổi hash/tên file, tự tìm file thật cùng MenuItemId.
            var prefix = $"menu-{item.MenuItemId}-";
            var candidate = Directory
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
                .OrderByDescending(
                    path => new FileInfo(path).LastWriteTimeUtc)
                .FirstOrDefault();

            if (candidate is null)
                continue;

            var newUrl =
                $"/images/menu/{Path.GetFileName(candidate)}";

            if (string.Equals(
                    item.ImageUrl,
                    newUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.WriteLine(
                $"[MenuImages] {item.MenuItemId} {item.ItemName}: " +
                $"{item.ImageUrl ?? "(trống)"} -> {newUrl}"
            );

            item.ImageUrl = newUrl;
            repaired++;
        }

        if (repaired > 0)
            await _db.SaveChangesAsync();

        return repaired;
    }
}
