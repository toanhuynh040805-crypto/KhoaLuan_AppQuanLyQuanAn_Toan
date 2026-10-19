using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Đồng bộ danh mục thực đơn của WinForms vào database dùng chung.
/// WinForms/backend chung là nguồn dữ liệu nghiệp vụ gốc; Mobile chỉ đọc lại
/// dữ liệu này qua API, không tự bổ sung nhóm món/món demo riêng.
///
/// Các món demo Mobile cũ chỉ được chuyển sang IsAvailable = 0 để giữ nguyên
/// khóa ngoại/lịch sử OrderItems. Không xóa dữ liệu lịch sử và không ghi đè
/// giá/ảnh/trạng thái mà quản lý đã chỉnh trên bản WinForms hiện tại.
/// </summary>
public sealed class SharedMenuCatalogService
{
    private readonly AppDbContext _db;

    public SharedMenuCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        const string sql = """
            SET NOCOUNT ON;

            IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
               OR OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
               OR OBJECT_ID(N'dbo.Stations', N'U') IS NULL
            BEGIN
                RETURN;
            END;

            -----------------------------------------------------------------
            -- 1. Loại khỏi thực đơn vận hành các món DEMO Mobile cũ.
            --    Chỉ set IsAvailable = 0 để không phá lịch sử đơn hàng.
            -----------------------------------------------------------------
            UPDATE dbo.MenuItems
            SET IsAvailable = 0
            WHERE UPPER(LTRIM(RTRIM(ItemName))) IN
            (
                UPPER(N'Gỏi ngó sen tôm thịt'),
                UPPER(N'Phở bò đặc biệt'),
                UPPER(N'Cơm chiên dương châu'),
                UPPER(N'Trà đá thanh nhiệt')
            );

            -----------------------------------------------------------------
            -- 2. Các nhóm món đang có trên bản WinForms làm nguồn gốc.
            -----------------------------------------------------------------
            INSERT INTO dbo.Categories (CategoryName, CategoryType)
            SELECT src.CategoryName, src.CategoryType
            FROM (VALUES
                (N'Lẩu bò',   N'MonAn'),
                (N'Món nhắm', N'MonAn'),
                (N'Đồ thêm',  N'MonAn'),
                (N'Đồ uống',  N'ThucUong')
            ) AS src(CategoryName, CategoryType)
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.Categories c
                WHERE UPPER(LTRIM(RTRIM(c.CategoryName))) =
                      UPPER(LTRIM(RTRIM(src.CategoryName)))
            );

            DECLARE @Catalog TABLE
            (
                SortOrder INT NOT NULL,
                CategoryName NVARCHAR(100) NOT NULL,
                StationName NVARCHAR(100) NULL,
                ItemName NVARCHAR(200) NOT NULL,
                Description NVARCHAR(500) NULL,
                Price DECIMAL(18,2) NOT NULL,
                Unit NVARCHAR(50) NOT NULL,
                IsFreeRefill BIT NOT NULL,
                FreeRefillLimit INT NOT NULL,
                RefillPrice DECIMAL(18,2) NULL,
                ImageUrl NVARCHAR(500) NULL
            );

            INSERT INTO @Catalog
                (SortOrder, CategoryName, StationName, ItemName, Description,
                 Price, Unit, IsFreeRefill, FreeRefillLimit, RefillPrice, ImageUrl)
            VALUES
                (5, N'Lẩu bò', N'Bep', N'Lẩu bò 2-3 người (Thường)',
                    N'Nồi lẩu bò cho 2-3 người', 220000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-5-4556885e.jpg'),
                (6, N'Lẩu bò', N'Bep', N'Lẩu bò 2-3 người (Đặc biệt)',
                    N'Nồi lẩu bò cho 2-3 người', 280000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-6-2ede9891.jpg'),
                (7, N'Lẩu bò', N'Bep', N'Lẩu bò 4-5 người (Thường)',
                    N'Nồi lẩu bò cho 4-5 người', 330000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-7-3ad5c397.jpg'),
                (8, N'Lẩu bò', N'Bep', N'Lẩu bò 4-5 người (Đặc biệt)',
                    N'Nồi lẩu bò cho 4-5 người', 390000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-8-635e7ffe.jpg'),
                (9, N'Lẩu bò', N'Bep', N'Lẩu bò 6-8 người (Thường)',
                    N'Nồi lẩu bò cho 6-8 người', 440000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-9-88d6c301.jpg'),
                (10, N'Lẩu bò', N'Bep', N'Lẩu bò 6-8 người (Đặc biệt)',
                    N'Nồi lẩu bò cho 6-8 người', 500000, N'Nồi', 0, 0, NULL,
                    N'/images/menu/menu-10-42f2d01b.jpg'),

                (11, N'Món nhắm', N'Bep', N'Bò viên chiên', NULL,
                    60000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-11-97391c56.jpg'),
                (12, N'Món nhắm', N'Bep', N'Chân gà sả tắc', NULL,
                    99000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-12-9604055a.jpg'),
                (13, N'Món nhắm', N'Bep', N'Gỏi lưỡi bò', NULL,
                    70000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-13-5192b2dc.jpg'),
                (14, N'Món nhắm', N'Bep', N'Gỏi bò + khoai môn', NULL,
                    139000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-14-5677d6b7.jpg'),
                (15, N'Món nhắm', N'Bep', N'Bò tơ + rau rừng', NULL,
                    159000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-15-a6b1fdb0.jpg'),
                (16, N'Món nhắm', N'Bep', N'Bò tơ cháy mè', NULL,
                    149000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-16-298ae6e7.jpg'),
                (17, N'Món nhắm', N'Bep', N'Bò một nắng đặc biệt', NULL,
                    99000, N'Đĩa', 0, 0, NULL,
                    N'/images/menu/menu-17-12621540.jpg'),

                (18, N'Đồ thêm', N'Bep', N'Bò viên thêm (nhỏ)',
                    N'Phần bò viên thêm', 60000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-18-4a681549.jpg'),
                (19, N'Đồ thêm', N'Bep', N'Bò viên thêm (lớn)',
                    N'Phần bò viên thêm', 115000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-19-45f85fe0.jpg'),
                (20, N'Đồ thêm', N'Bep', N'Thịt thêm (nhỏ)',
                    N'Phần thịt bò thêm', 60000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-20-f79b04c9.jpg'),
                (21, N'Đồ thêm', N'Bep', N'Thịt thêm (lớn)',
                    N'Phần thịt bò thêm', 115000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-21-1c867612.jpg'),
                (22, N'Đồ thêm', N'Bep', N'Gân thêm (nhỏ)',
                    N'Phần gân bò thêm', 60000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-22-faf03d0e.jpg'),
                (23, N'Đồ thêm', N'Bep', N'Gân thêm (lớn)',
                    N'Phần gân bò thêm', 115000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-23-cc816f4a.jpg'),
                (24, N'Đồ thêm', N'Bep', N'Đậu hủ ky', NULL,
                    16000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-24-a8a2967d.jpg'),
                (25, N'Đồ thêm', N'Bep', N'Đậu hủ non', NULL,
                    11000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-25-9aacb9a3.jpg'),
                (26, N'Đồ thêm', N'Bep', N'Bánh phở', NULL,
                    14000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-26-104a4e45.png'),
                (27, N'Đồ thêm', N'Bep', N'Mì vắt', NULL,
                    12000, N'Phần', 0, 0, NULL,
                    N'/images/menu/menu-27-69d4c722.jpg'),
                (28, N'Đồ thêm', N'Bep', N'Mì gói', NULL,
                    6000, N'Gói', 0, 0, NULL,
                    N'/images/menu/menu-28-6f4244fd.jpg'),
                (29, N'Đồ thêm', NULL, N'Khăn lạnh', NULL,
                    4000, N'Cái', 0, 0, NULL,
                    N'/images/menu/menu-29-0b7915e0.jpg'),

                (30, N'Đồ uống', N'QuayNuoc', N'Trà tắc mật ong đường phèn', NULL,
                    22000, N'Ly', 0, 0, NULL,
                    N'/images/menu/menu-30-5da9ea4a.jpg'),
                (31, N'Đồ uống', N'QuayNuoc', N'Pepsi', NULL,
                    17000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-31-340ee4b2.png'),
                (32, N'Đồ uống', N'QuayNuoc', N'Coca', NULL,
                    17000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-32-58d69abb.jpg'),
                (33, N'Đồ uống', N'QuayNuoc', N'7up', NULL,
                    17000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-33-991263c8.jpg'),
                (34, N'Đồ uống', N'QuayNuoc', N'Sting', NULL,
                    17000, N'Chai', 0, 0, NULL,
                    N'/images/menu/menu-34-a92dce2a.jpg'),
                (35, N'Đồ uống', N'QuayNuoc', N'Xá xị', NULL,
                    17000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-35-95932323.png'),
                (36, N'Đồ uống', N'QuayNuoc', N'Nước suối', NULL,
                    14000, N'Chai', 0, 0, NULL,
                    N'/images/menu/menu-36-d9866951.jpg'),
                (37, N'Đồ uống', N'QuayNuoc', N'Ken bạc', NULL,
                    26000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-37-d49be95c.jpg'),
                (38, N'Đồ uống', N'QuayNuoc', N'Ken xanh', NULL,
                    29000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-38-890892ec.jpg'),
                (39, N'Đồ uống', N'QuayNuoc', N'Ken lùn', NULL,
                    24000, N'Chai', 0, 0, NULL,
                    N'/images/menu/menu-39-fb52312d.jpg'),
                (40, N'Đồ uống', N'QuayNuoc', N'Tiger nâu - bạc', NULL,
                    25000, N'Lon', 0, 0, NULL,
                    N'/images/menu/menu-40-4bab29cc.jpg'),
                (41, N'Đồ uống', N'QuayNuoc', N'Bia hơi', NULL,
                    63000, N'Tháp', 0, 0, NULL,
                    N'/images/menu/menu-41-97e104ae.png');

            -----------------------------------------------------------------
            -- 3. Chỉ bổ sung món WinForms còn thiếu. Không ghi đè giá/trạng thái.
            -----------------------------------------------------------------
            INSERT INTO dbo.MenuItems
                (CategoryId, StationId, ItemName, Description, Price, Unit,
                 IsAvailable, IsFreeRefill, FreeRefillLimit, RefillPrice, ImageUrl)
            SELECT
                c.CategoryId,
                s.StationId,
                src.ItemName,
                src.Description,
                src.Price,
                src.Unit,
                1,
                src.IsFreeRefill,
                src.FreeRefillLimit,
                src.RefillPrice,
                src.ImageUrl
            FROM @Catalog src
            INNER JOIN dbo.Categories c
                ON UPPER(LTRIM(RTRIM(c.CategoryName))) =
                   UPPER(LTRIM(RTRIM(src.CategoryName)))
            LEFT JOIN dbo.Stations s
                ON UPPER(LTRIM(RTRIM(s.StationName))) =
                   UPPER(LTRIM(RTRIM(src.StationName)))
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.MenuItems m
                WHERE UPPER(LTRIM(RTRIM(m.ItemName))) =
                      UPPER(LTRIM(RTRIM(src.ItemName)))
            );

            -----------------------------------------------------------------
            -- 4. DB cũ có món WinForms nhưng thiếu ImageUrl: gán ảnh gốc.
            --    Không ghi đè ảnh mà quản lý đã upload sau này.
            -----------------------------------------------------------------
            UPDATE m
            SET m.ImageUrl = src.ImageUrl
            FROM dbo.MenuItems m
            INNER JOIN @Catalog src
                ON UPPER(LTRIM(RTRIM(m.ItemName))) =
                   UPPER(LTRIM(RTRIM(src.ItemName)))
            WHERE (m.ImageUrl IS NULL OR LTRIM(RTRIM(m.ImageUrl)) = N'')
              AND src.ImageUrl IS NOT NULL;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql);
    }
}
