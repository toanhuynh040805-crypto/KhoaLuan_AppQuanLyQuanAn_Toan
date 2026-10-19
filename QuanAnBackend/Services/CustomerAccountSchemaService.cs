using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Đảm bảo có schema tài khoản web cho khách hàng (idempotent, không xóa dữ liệu).
/// - CustomerAccounts: mật khẩu + trạng thái, nối 1-1 với bảng Customers có sẵn.
/// - CustomerRefreshTokens: refresh token (lưu hash) cho phiên đăng nhập web.
/// </summary>
public class CustomerAccountSchemaService
{
    private readonly AppDbContext _db;

    public CustomerAccountSchemaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.CustomerAccounts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CustomerAccounts
                (
                    CustomerId INT NOT NULL PRIMARY KEY,
                    PasswordHash NVARCHAR(200) NOT NULL,
                    IsActive BIT NOT NULL
                        CONSTRAINT DF_CustomerAccounts_IsActive DEFAULT(1),
                    FailedLoginCount INT NOT NULL
                        CONSTRAINT DF_CustomerAccounts_Failed DEFAULT(0),
                    LockedUntil DATETIME2 NULL,
                    PasswordResetTokenHash NVARCHAR(100) NULL,
                    PasswordResetExpiresAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL
                        CONSTRAINT DF_CustomerAccounts_CreatedAt DEFAULT(GETDATE()),
                    LastLoginAt DATETIME2 NULL,
                    CONSTRAINT FK_CustomerAccounts_Customers
                        FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
                );
            END;

            IF OBJECT_ID(N'dbo.CustomerRefreshTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CustomerRefreshTokens
                (
                    TokenId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CustomerId INT NOT NULL,
                    TokenHash NVARCHAR(100) NOT NULL,
                    ExpiresAt DATETIME2 NOT NULL,
                    RevokedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL
                        CONSTRAINT DF_CustomerRefreshTokens_CreatedAt DEFAULT(GETDATE()),
                    CONSTRAINT FK_CustomerRefreshTokens_Customers
                        FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
                );

                CREATE UNIQUE INDEX UX_CustomerRefreshTokens_Hash
                    ON dbo.CustomerRefreshTokens(TokenHash);

                CREATE INDEX IX_CustomerRefreshTokens_Customer
                    ON dbo.CustomerRefreshTokens(CustomerId, RevokedAt);
            END;

            IF OBJECT_ID(N'dbo.CustomerAccounts', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CustomerAccounts_ResetToken'
                      AND object_id = OBJECT_ID(N'dbo.CustomerAccounts'))
            BEGIN
                CREATE INDEX IX_CustomerAccounts_ResetToken
                    ON dbo.CustomerAccounts(PasswordResetTokenHash);
            END;
            """);
                // Đơn đặt món từ web: cột nguồn + khóa chống trùng.
        await _db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.Orders', N'ClientRequestId') IS NULL
                ALTER TABLE dbo.Orders ADD ClientRequestId NVARCHAR(64) NULL;

            IF COL_LENGTH(N'dbo.Orders', N'Source') IS NULL
                ALTER TABLE dbo.Orders
                    ADD Source NVARCHAR(20) NOT NULL
                        CONSTRAINT DF_Orders_Source DEFAULT N'NhanVien' WITH VALUES;
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = N'UX_Orders_ClientRequestId'
                             AND object_id = OBJECT_ID(N'dbo.Orders'))
                CREATE UNIQUE INDEX UX_Orders_ClientRequestId
                    ON dbo.Orders(ClientRequestId)
                    WHERE ClientRequestId IS NOT NULL;
            """);

        // Nhân viên hệ thống "WEB": không đăng nhập được (IsActive = 0, không có role),
        // chỉ làm người tạo cho đơn đặt từ web.
        await _db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'web-system')
                INSERT INTO dbo.Users (Username, PasswordHash, FullName, IsActive)
                VALUES (N'web-system', N'!DISABLED!', N'Đơn đặt từ Web', 0);

            IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeCode = N'WEB')
                INSERT INTO dbo.Employees (UserId, EmployeeCode, Position, HireDate, Status)
                SELECT UserId, N'WEB', N'PhucVu', CAST(GETDATE() AS DATE), N'NghiViec'
                FROM dbo.Users WHERE Username = N'web-system';
            """);
    }
}