using System.IO.Compression;

namespace QuanAnBackend.Services;

/// <summary>
/// Khôi phục bộ ảnh thực đơn WinForms đi kèm source vào wwwroot/images/menu.
/// Chỉ tạo file còn thiếu để không ghi đè ảnh được quản lý upload sau này.
/// </summary>
public sealed class BundledMenuImageService
{
    private readonly IWebHostEnvironment _env;

    public BundledMenuImageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task EnsureAsync()
    {
        var archivePath = Path.Combine(
            _env.ContentRootPath,
            "SeedAssets",
            "menu-images.zip"
        );

        if (!File.Exists(archivePath))
        {
            Console.WriteLine(
                $"[MenuImages] Không tìm thấy gói ảnh: {archivePath}"
            );
            return;
        }

        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var destinationFolder = Path.Combine(webRoot, "images", "menu");
        Directory.CreateDirectory(destinationFolder);

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
            if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
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
            $"[MenuImages] Sẵn sàng {archive.Entries.Count} ảnh; khôi phục mới {restored} file."
        );
    }
}
