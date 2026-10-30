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
        // Đơn đặt món từ web: UUID chống gửi trùng.
        // Web tạo bằng crypto.randomUUID(), Backend dùng Guid?,
        // SQL Server lưu bằng UNIQUEIDENTIFIER.
        await _db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.Orders', N'ClientRequestId') IS NULL
            BEGIN
                ALTER TABLE dbo.Orders
                    ADD ClientRequestId UNIQUEIDENTIFIER NULL;
            END
            ELSE IF EXISTS
            (
                SELECT 1
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
                  AND c.name = N'ClientRequestId'
                  AND t.name <> N'uniqueidentifier'
            )
            BEGIN
                -- Không chuyển nếu DB cũ chứa giá trị không phải GUID.
                -- Dừng để tránh làm mất dữ liệu.
                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.Orders
                    WHERE ClientRequestId IS NOT NULL
                      AND TRY_CONVERT(UNIQUEIDENTIFIER, ClientRequestId) IS NULL
                )
                BEGIN
                    THROW 51001,
                        'Orders.ClientRequestId contains legacy non-GUID values. Migration stopped to avoid data loss.',
                        1;
                END;

                IF EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_Orders_ClientRequestId'
                      AND object_id = OBJECT_ID(N'dbo.Orders')
                )
                    DROP INDEX UX_Orders_ClientRequestId ON dbo.Orders;

                IF EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_Orders_Customer_ClientRequestId'
                      AND object_id = OBJECT_ID(N'dbo.Orders')
                )
                    DROP INDEX UX_Orders_Customer_ClientRequestId ON dbo.Orders;

                ALTER TABLE dbo.Orders
                    ALTER COLUMN ClientRequestId UNIQUEIDENTIFIER NULL;
            END;

            IF COL_LENGTH(N'dbo.Orders', N'ClientRequestHash') IS NULL
            BEGIN
                ALTER TABLE dbo.Orders
                    ADD ClientRequestHash VARCHAR(64) NULL;
            END
            ELSE IF EXISTS
            (
                SELECT 1
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
                  AND c.name = N'ClientRequestHash'
                  AND (t.name <> N'varchar' OR c.max_length < 64)
            )
            BEGIN
                ALTER TABLE dbo.Orders
                    ALTER COLUMN ClientRequestHash VARCHAR(64) NULL;
            END;

            IF COL_LENGTH(N'dbo.Orders', N'Source') IS NULL
                ALTER TABLE dbo.Orders
                    ADD Source NVARCHAR(20) NOT NULL
                        CONSTRAINT DF_Orders_Source DEFAULT N'NhanVien' WITH VALUES;
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            IF EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE name = N'UX_Orders_ClientRequestId'
                  AND object_id = OBJECT_ID(N'dbo.Orders')
            )
                DROP INDEX UX_Orders_ClientRequestId ON dbo.Orders;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE name = N'UX_Orders_Customer_ClientRequestId'
                  AND object_id = OBJECT_ID(N'dbo.Orders')
            )
                CREATE UNIQUE INDEX UX_Orders_Customer_ClientRequestId
                    ON dbo.Orders(CustomerId, ClientRequestId)
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