using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Idempotent compatibility patch for schema additions shared by WinForms and
/// Mobile. Existing tables/columns/data are preserved; only missing compatible
/// fields/constraints/indexes are added and legacy invalid takeaway table type
/// values are normalized.
/// </summary>
public class WinFormsCompatibilitySchemaService
{
    private readonly AppDbContext _db;

    public WinFormsCompatibilitySchemaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        // SQL Server compiles a whole batch before executing it. Therefore a
        // column added near the top of one batch cannot safely be referenced by
        // statements later in that same batch on an older database: SQL Server
        // may raise error 207 (Invalid column name) before ALTER TABLE runs.
        // Keep schema creation and statements that consume the new columns in
        // separate batches. Every step remains idempotent.

        // Phase 1: add/alter columns only.
        await _db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.OrderItems', N'CancelReason') IS NULL
                ALTER TABLE dbo.OrderItems ADD CancelReason NVARCHAR(255) NULL;

            IF COL_LENGTH(N'dbo.OrderItems', N'CancelledByUserId') IS NULL
                ALTER TABLE dbo.OrderItems ADD CancelledByUserId INT NULL;

            IF COL_LENGTH(N'dbo.Reservations', N'DurationMinutes') IS NULL
            BEGIN
                ALTER TABLE dbo.Reservations
                    ADD DurationMinutes INT NOT NULL
                        CONSTRAINT DF_Reservations_DurationMinutes DEFAULT(30) WITH VALUES;
            END;

            IF COL_LENGTH(N'dbo.Reservations', N'CancelledByUserId') IS NULL
                ALTER TABLE dbo.Reservations ADD CancelledByUserId INT NULL;

            IF COL_LENGTH(N'dbo.Reservations', N'CancelledAt') IS NULL
                ALTER TABLE dbo.Reservations ADD CancelledAt DATETIME2 NULL;
            IF COL_LENGTH(N'dbo.Reservations', N'Source') IS NULL
                ALTER TABLE dbo.Reservations
                    ADD Source NVARCHAR(20) NOT NULL
                        CONSTRAINT DF_Reservations_Source DEFAULT N'NhanVien' WITH VALUES;

            IF COL_LENGTH(N'dbo.Reservations', N'BookingGroupId') IS NULL
                ALTER TABLE dbo.Reservations ADD BookingGroupId INT NULL;
            IF COL_LENGTH(N'dbo.ShiftAssignments', N'AreaId') IS NULL
                ALTER TABLE dbo.ShiftAssignments ADD AreaId INT NULL;

            IF COL_LENGTH(N'dbo.Users', N'LastLoginAt') IS NULL
                ALTER TABLE dbo.Users ADD LastLoginAt DATETIME2 NULL;

            IF COL_LENGTH(N'dbo.MenuItems', N'ImageUrl') IS NULL
                ALTER TABLE dbo.MenuItems ADD ImageUrl NVARCHAR(500) NULL;

            IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.Orders')
                      AND name = N'TableId'
                      AND is_nullable = 0
                )
                    ALTER TABLE dbo.Orders ALTER COLUMN TableId INT NULL;

                IF COL_LENGTH(N'dbo.Orders', N'OrderType') IS NULL
                    ALTER TABLE dbo.Orders
                        ADD OrderType NVARCHAR(10) NOT NULL
                            CONSTRAINT DF_Orders_OrderType DEFAULT N'TaiBan' WITH VALUES;

                IF COL_LENGTH(N'dbo.Orders', N'TakeawayNumber') IS NULL
                    ALTER TABLE dbo.Orders ADD TakeawayNumber INT NULL;
            END;

            IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.OrderItems', N'IsTakeaway') IS NULL
                ALTER TABLE dbo.OrderItems
                    ADD IsTakeaway BIT NOT NULL
                        CONSTRAINT DF_OrderItems_IsTakeaway DEFAULT 0 WITH VALUES;
            """);

        // Phase 2: constraints that may reference columns created above.
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.FK_Reservations_CancelledByUser', N'F') IS NULL
               AND COL_LENGTH(N'dbo.Reservations', N'CancelledByUserId') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.Reservations
                    ADD CONSTRAINT FK_Reservations_CancelledByUser
                    FOREIGN KEY (CancelledByUserId) REFERENCES dbo.Users(UserId);
            END;

            IF OBJECT_ID(N'dbo.FK_ShiftAssignments_Areas', N'F') IS NULL
               AND COL_LENGTH(N'dbo.ShiftAssignments', N'AreaId') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.ShiftAssignments
                    ADD CONSTRAINT FK_ShiftAssignments_Areas
                    FOREIGN KEY (AreaId) REFERENCES dbo.Areas(AreaId);
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = N'IX_Reservations_Customer'
                             AND object_id = OBJECT_ID(N'dbo.Reservations'))
               AND COL_LENGTH(N'dbo.Reservations', N'CustomerId') IS NOT NULL
                CREATE INDEX IX_Reservations_Customer ON dbo.Reservations(CustomerId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = N'IX_Reservations_BookingGroup'
                             AND object_id = OBJECT_ID(N'dbo.Reservations'))
               AND COL_LENGTH(N'dbo.Reservations', N'BookingGroupId') IS NOT NULL
                CREATE INDEX IX_Reservations_BookingGroup ON dbo.Reservations(BookingGroupId);
            """);

        // Phase 3: data normalization. These statements are intentionally in a
        // later batch so newly-added columns are visible to SQL Server's parser.
        await _db.Database.ExecuteSqlRawAsync("""
            -- Mobile từng dùng TableType = 'MangVe', trong khi DiningTable chỉ
            -- có Thuong/VIP. Khu vực "Mang về" vẫn được giữ nguyên để tương thích
            -- luồng Mobile cũ; chỉ chuẩn hóa TableType về giá trị hợp lệ.
            IF OBJECT_ID(N'dbo.DiningTables', N'U') IS NOT NULL
               AND OBJECT_ID(N'dbo.Areas', N'U') IS NOT NULL
            BEGIN
                UPDATE t
                SET TableType = N'Thuong'
                FROM dbo.DiningTables t
                INNER JOIN dbo.Areas a ON a.AreaId = t.AreaId
                WHERE t.TableType = N'MangVe'
                  AND (a.AreaName = N'Mang về' OR t.TableName LIKE N'MV-%');
            END;

            IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Orders', N'OrderType') IS NOT NULL
            BEGIN
                UPDATE dbo.Orders
                SET OrderType = N'TaiBan'
                WHERE OrderType IS NULL OR LTRIM(RTRIM(OrderType)) = N'';
            END;
            """);

        // Phase 4: constraints/indexes for the completed Orders schema.
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Orders', N'OrderType') IS NOT NULL
               AND COL_LENGTH(N'dbo.Orders', N'TakeawayNumber') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM sys.check_constraints
                    WHERE name = N'CK_Orders_OrderType_Table'
                      AND parent_object_id = OBJECT_ID(N'dbo.Orders')
                )
                BEGIN
                    ALTER TABLE dbo.Orders WITH CHECK ADD CONSTRAINT CK_Orders_OrderType_Table CHECK (
                        (OrderType = N'TaiBan' AND TableId IS NOT NULL)
                        OR
                        (OrderType = N'MangVe' AND TableId IS NULL AND TakeawayNumber IS NOT NULL)
                    );
                END;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Orders_Takeaway'
                      AND object_id = OBJECT_ID(N'dbo.Orders')
                )
                    CREATE INDEX IX_Orders_Takeaway
                    ON dbo.Orders(OrderTime, TakeawayNumber)
                    WHERE TakeawayNumber IS NOT NULL;
            END;
            """);
    }
}