using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using System.Threading.Tasks;

namespace QuanAnBackend.Services
{
    public class WebProfileSchemaService
    {
        private readonly AppDbContext _context;

        public WebProfileSchemaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task EnsureAsync()
        {
            // BƯỚC 1: THÊM CỘT (SCHEMA)
            // Chạy riêng biệt để SQL Server ghi nhận cột mới vào hệ thống.
            const string addColumnsSql = """
                SET XACT_ABORT ON;

                IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
                    THROW 51001, N'Required table dbo.Customers does not exist.', 1;
                IF OBJECT_ID(N'dbo.Reservations', N'U') IS NULL
                    THROW 51002, N'Required table dbo.Reservations does not exist.', 1;
                IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
                    THROW 51003, N'Required base table dbo.Notifications does not exist.', 1;

                IF COL_LENGTH(N'dbo.Customers', N'AvatarUrl') IS NULL
                    ALTER TABLE dbo.Customers ADD AvatarUrl NVARCHAR(500) NULL;

                IF COL_LENGTH(N'dbo.Reservations', N'CustomerId') IS NULL
                    ALTER TABLE dbo.Reservations ADD CustomerId INT NULL;

                IF COL_LENGTH(N'dbo.Notifications', N'CustomerId') IS NULL
                    ALTER TABLE dbo.Notifications ADD CustomerId INT NULL;

                IF COL_LENGTH(N'dbo.Notifications', N'EventKey') IS NULL
                    ALTER TABLE dbo.Notifications ADD EventKey NVARCHAR(200) NULL;
                """;

            await _context.Database.ExecuteSqlRawAsync(addColumnsSql);

            // BƯỚC 2: CẬP NHẬT DỮ LIỆU & RÀNG BUỘC (DATA & CONSTRAINTS)
            // Lúc này SQL Server đã biết các cột tồn tại, nên sẽ không báo lỗi Invalid column name nữa.
            const string updateDataSql = """
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Reservations_Customers')
                    ALTER TABLE dbo.Reservations ADD CONSTRAINT FK_Reservations_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'IX_Reservations_CustomerId_ReservationTime')
                    CREATE INDEX IX_Reservations_CustomerId_ReservationTime ON dbo.Reservations(CustomerId, ReservationTime DESC) WHERE CustomerId IS NOT NULL;

                UPDATE reservation
                SET CustomerId = customer.CustomerId
                FROM dbo.Reservations AS reservation
                INNER JOIN dbo.Customers AS customer
                    ON customer.Phone = reservation.CustomerPhone
                WHERE reservation.CustomerId IS NULL
                  AND reservation.CustomerPhone IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_Customers')
                    ALTER TABLE dbo.Notifications ADD CONSTRAINT FK_Notifications_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Notifications') AND name = N'IX_Notifications_CustomerId_IsRead_CreatedAt')
                    CREATE INDEX IX_Notifications_CustomerId_IsRead_CreatedAt ON dbo.Notifications(CustomerId, IsRead, CreatedAt DESC) WHERE CustomerId IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Notifications') AND name = N'UX_Notifications_EventKey')
                    CREATE UNIQUE INDEX UX_Notifications_EventKey ON dbo.Notifications(EventKey) WHERE EventKey IS NOT NULL;

                COMMIT TRANSACTION;
                """;

            await _context.Database.ExecuteSqlRawAsync(updateDataSql);
        }
    }
}