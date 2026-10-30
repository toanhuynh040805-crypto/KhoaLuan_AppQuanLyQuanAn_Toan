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

                IF COL_LENGTH(N'dbo.Customers', N'PasswordHash') IS NULL
                    ALTER TABLE dbo.Customers ADD PasswordHash NVARCHAR(500) NULL;

                IF COL_LENGTH(N'dbo.Customers', N'AvatarUrl') IS NULL
                    ALTER TABLE dbo.Customers ADD AvatarUrl NVARCHAR(1000) NULL;
                ELSE IF EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'dbo.Customers')
                      AND c.name = N'AvatarUrl'
                      AND t.name = N'nvarchar'
                      AND c.max_length < 2000
                )
                    ALTER TABLE dbo.Customers ALTER COLUMN AvatarUrl NVARCHAR(1000) NULL;

                IF COL_LENGTH(N'dbo.Reservations', N'CustomerId') IS NULL
                    ALTER TABLE dbo.Reservations ADD CustomerId INT NULL;

                IF COL_LENGTH(N'dbo.Notifications', N'CustomerId') IS NULL
                    ALTER TABLE dbo.Notifications ADD CustomerId INT NULL;

                IF COL_LENGTH(N'dbo.Notifications', N'EventKey') IS NULL
                    ALTER TABLE dbo.Notifications ADD EventKey NVARCHAR(200) NULL;
                """;

            await _context.Database.ExecuteSqlRawAsync(addColumnsSql);

            // Các bảng Web/đánh giá được thêm sau khi dự án đã vận hành.
            // Tạo idempotent để database cũ tự nâng cấp khi Backend khởi động.
            const string webFeatureTablesSql = """
                SET XACT_ABORT ON;

                IF OBJECT_ID(N'dbo.Feedbacks', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Feedbacks
                    (
                        FeedbackId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Feedbacks PRIMARY KEY,
                        InvoiceId INT NOT NULL,
                        CustomerId INT NULL,
                        Rating TINYINT NOT NULL,
                        Comment NVARCHAR(1000) NULL,
                        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Feedbacks_CreatedAt DEFAULT(GETDATE()),
                        CONSTRAINT FK_Feedbacks_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices(InvoiceId),
                        CONSTRAINT FK_Feedbacks_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
                        CONSTRAINT CK_Feedbacks_Rating CHECK (Rating BETWEEN 1 AND 5)
                    );
                END;

                IF OBJECT_ID(N'dbo.FeedbackItems', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.FeedbackItems
                    (
                        FeedbackItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeedbackItems PRIMARY KEY,
                        FeedbackId INT NOT NULL,
                        MenuItemId INT NOT NULL,
                        Rating TINYINT NOT NULL,
                        CONSTRAINT FK_FeedbackItems_Feedbacks FOREIGN KEY (FeedbackId) REFERENCES dbo.Feedbacks(FeedbackId),
                        CONSTRAINT FK_FeedbackItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems(MenuItemId),
                        CONSTRAINT CK_FeedbackItems_Rating CHECK (Rating BETWEEN 1 AND 5)
                    );
                END;

                IF OBJECT_ID(N'dbo.OrderReviews', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.OrderReviews
                    (
                        ReviewId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderReviews PRIMARY KEY,
                        OrderId INT NOT NULL,
                        OrderItemId INT NULL,
                        CustomerId INT NOT NULL,
                        Rating TINYINT NOT NULL,
                        Comment NVARCHAR(1000) NULL,
                        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_OrderReviews_CreatedAt DEFAULT(SYSUTCDATETIME()),
                        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_OrderReviews_UpdatedAt DEFAULT(SYSUTCDATETIME()),
                        IsVisible BIT NOT NULL CONSTRAINT DF_OrderReviews_IsVisible DEFAULT(1),
                        ModerationStatus NVARCHAR(16) NOT NULL CONSTRAINT DF_OrderReviews_ModerationStatus DEFAULT(N'Public'),
                        CONSTRAINT FK_OrderReviews_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
                        CONSTRAINT FK_OrderReviews_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(OrderItemId),
                        CONSTRAINT FK_OrderReviews_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
                        CONSTRAINT CK_OrderReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
                    );
                END;
                """;

            await _context.Database.ExecuteSqlRawAsync(webFeatureTablesSql);

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

                IF OBJECT_ID(N'dbo.Feedbacks', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Feedbacks') AND name = N'IX_Feedbacks_InvoiceId')
                    CREATE INDEX IX_Feedbacks_InvoiceId ON dbo.Feedbacks(InvoiceId);

                IF OBJECT_ID(N'dbo.Feedbacks', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Feedbacks') AND name = N'IX_Feedbacks_Rating')
                    CREATE INDEX IX_Feedbacks_Rating ON dbo.Feedbacks(Rating);

                IF OBJECT_ID(N'dbo.OrderReviews', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderReviews') AND name = N'UX_OrderReviews_OrderItemId')
                    CREATE UNIQUE INDEX UX_OrderReviews_OrderItemId
                    ON dbo.OrderReviews(OrderItemId) WHERE OrderItemId IS NOT NULL;

                IF OBJECT_ID(N'dbo.OrderReviews', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderReviews') AND name = N'UX_OrderReviews_OrderId_Experience')
                    CREATE UNIQUE INDEX UX_OrderReviews_OrderId_Experience
                    ON dbo.OrderReviews(OrderId) WHERE OrderItemId IS NULL;

                COMMIT TRANSACTION;
                """;

            await _context.Database.ExecuteSqlRawAsync(updateDataSql);
        }
    }
}