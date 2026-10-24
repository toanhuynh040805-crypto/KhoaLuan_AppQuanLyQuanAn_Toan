using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Tạo lớp cấu hình quyền Mobile tách khỏi code nghiệp vụ.
/// Bảng này là schema tương thích, không sửa QuanAnManagementDB_Final.sql.
/// Sau khi seed lần đầu, quyền của từng Role có thể thay đổi trực tiếp trong DB
/// hoặc qua API quản trị mà không cần build lại Mobile/Backend.
/// </summary>
public class MobilePermissionSchemaService
{
    private readonly AppDbContext _db;

    public MobilePermissionSchemaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.MobilePermissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobilePermissions
                (
                    PermissionKey NVARCHAR(100) NOT NULL PRIMARY KEY,
                    DisplayName NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_MobilePermissions_IsActive DEFAULT(1)
                );
            END;

            IF OBJECT_ID(N'dbo.RoleMobilePermissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RoleMobilePermissions
                (
                    RoleId INT NOT NULL,
                    PermissionKey NVARCHAR(100) NOT NULL,
                    IsAllowed BIT NOT NULL CONSTRAINT DF_RoleMobilePermissions_IsAllowed DEFAULT(1),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_RoleMobilePermissions_UpdatedAt DEFAULT(GETDATE()),
                    CONSTRAINT PK_RoleMobilePermissions PRIMARY KEY(RoleId, PermissionKey),
                    CONSTRAINT FK_RoleMobilePermissions_Roles
                        FOREIGN KEY(RoleId) REFERENCES dbo.Roles(RoleId),
                    CONSTRAINT FK_RoleMobilePermissions_Permissions
                        FOREIGN KEY(PermissionKey) REFERENCES dbo.MobilePermissions(PermissionKey)
                );
            END;

            MERGE dbo.MobilePermissions AS target
            USING (VALUES
                (N'mobile.use',       N'Sử dụng Mobile',          N'Được đăng nhập và sử dụng ứng dụng Mobile'),
                (N'screen.tables',    N'Màn hình bàn',           N'Được vào màn hình bàn và hóa đơn'),
                (N'screen.kitchen',   N'Màn hình bếp',           N'Được vào màn hình bếp'),
                (N'table.manage',     N'Thao tác bàn',           N'Nhận bàn, sửa số khách, chuyển/gộp/ghép/tách bàn'),
                (N'order.create',     N'Gọi món',                N'Tạo món mới cho đơn'),
                (N'order.cancel',     N'Hủy món',                N'Hủy món chưa chế biến'),
                (N'order.serve',      N'Xác nhận phục vụ',       N'Đánh dấu món đã phục vụ'),
                (N'payment.request',  N'Yêu cầu thanh toán',     N'Gửi yêu cầu thanh toán'),
                (N'payment.complete', N'Hoàn tất thanh toán',    N'Thu tiền và hoàn tất hóa đơn'),
                (N'kitchen.manage',   N'Thao tác bếp',           N'Nhận/chế biến/hoàn thành món tại bếp'),
                (N'customer.manage',  N'Khách hàng thành viên',  N'Tìm, tạo và gắn khách hàng vào đơn'),
                (N'area.all',         N'Tất cả khu vực',         N'Không bị giới hạn bởi phân công khu vực'),
                (N'area.assign',      N'Phân công khu vực',      N'Được cấu hình nhân viên phụ trách khu vực'),
                (N'access.configure', N'Cấu hình quyền',         N'Được thay đổi quyền của các vai trò'),
                (N'statistics.view', N'Xem thống kê',           N'Được xem doanh thu, món bán chạy và doanh thu theo nhân viên'),
                (N'staff.manage', N'Quản lý nhân viên',       N'Được phân ca, giao nhiệm vụ và xem lịch sử làm việc của nhân viên')
            ) AS source(PermissionKey, DisplayName, Description)
            ON target.PermissionKey = source.PermissionKey
            WHEN MATCHED THEN UPDATE SET
                DisplayName = source.DisplayName,
                Description = source.Description,
                IsActive = 1
            WHEN NOT MATCHED THEN INSERT(PermissionKey, DisplayName, Description, IsActive)
                VALUES(source.PermissionKey, source.DisplayName, source.Description, 1);

            -- Chỉ seed cấu hình mặc định cho những cặp Role/Permission chưa tồn tại.
            -- Đây là dữ liệu khởi tạo để giữ hành vi cũ; logic runtime KHÔNG switch theo RoleName.
            DECLARE @Defaults TABLE(RoleName NVARCHAR(100), PermissionKey NVARCHAR(100));

            INSERT INTO @Defaults(RoleName, PermissionKey) VALUES
                (N'PhucVu', N'mobile.use'),
                (N'PhucVu', N'screen.tables'),
                (N'PhucVu', N'table.manage'),
                (N'PhucVu', N'order.create'),
                (N'PhucVu', N'order.cancel'),
                (N'PhucVu', N'order.serve'),
                (N'PhucVu', N'payment.request'),
                (N'PhucVu', N'customer.manage'),

                (N'ThuNgan', N'mobile.use'),
                (N'ThuNgan', N'screen.tables'),
                (N'ThuNgan', N'table.manage'),
                (N'ThuNgan', N'order.create'),
                (N'ThuNgan', N'order.cancel'),
                (N'ThuNgan', N'order.serve'),
                (N'ThuNgan', N'payment.request'),
                (N'ThuNgan', N'payment.complete'),
                (N'ThuNgan', N'customer.manage'),
                (N'ThuNgan', N'area.all'),

                (N'Bep', N'mobile.use'),
                (N'Bep', N'screen.kitchen'),
                (N'Bep', N'kitchen.manage'),
                (N'Bep', N'area.all'),

                (N'QuanLy', N'mobile.use'),
                (N'QuanLy', N'screen.tables'),
                (N'QuanLy', N'screen.kitchen'),
                (N'QuanLy', N'table.manage'),
                (N'QuanLy', N'order.create'),
                (N'QuanLy', N'order.cancel'),
                (N'QuanLy', N'order.serve'),
                (N'QuanLy', N'payment.request'),
                (N'QuanLy', N'payment.complete'),
                (N'QuanLy', N'kitchen.manage'),
                (N'QuanLy', N'customer.manage'),
                (N'QuanLy', N'area.all'),
                (N'QuanLy', N'area.assign'),
                (N'QuanLy', N'access.configure'),
                (N'QuanLy', N'statistics.view'),
                (N'QuanLy', N'staff.manage');

            INSERT INTO dbo.RoleMobilePermissions(RoleId, PermissionKey, IsAllowed)
            SELECT r.RoleId, d.PermissionKey, 1
            FROM @Defaults d
            INNER JOIN dbo.Roles r ON r.RoleName = d.RoleName
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.RoleMobilePermissions rp
                WHERE rp.RoleId = r.RoleId
                  AND rp.PermissionKey = d.PermissionKey
            );
            """);
    }
}
