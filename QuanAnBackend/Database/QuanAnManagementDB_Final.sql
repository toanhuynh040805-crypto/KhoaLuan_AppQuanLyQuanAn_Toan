/* ======================================================================
   HỆ THỐNG QUẢN LÝ QUÁN ĂN - GUSTOPOS MANAGEMENT DATABASE
   DATABASE SCRIPT CHUẨN HÓA DUY NHẤT (FINAL MASTER SCRIPT)
   Tích hợp toàn bộ cấu hình: Backend Web, WinForms, Mobile Flutter
   Hỗ trợ triển khai mới (Clean Install) hoặc đối chiếu cấu trúc hoàn chỉnh
   ====================================================================== */

IF DB_ID('QuanAnManagementDB') IS NULL
BEGIN
    CREATE DATABASE QuanAnManagementDB;
END;
GO

USE QuanAnManagementDB;
GO

/* ======================================================================
   NHÓM 1: NGƯỜI DÙNG & PHÂN QUYỀN (Role, User, UserRoles, Employee)
   ====================================================================== */

CREATE TABLE Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleName        NVARCHAR(50)  NOT NULL UNIQUE, -- QuanLy (kiêm Admin), ThuNgan, PhucVu, Bep
    Description     NVARCHAR(255) NULL
    -- Quản lý và Admin gộp chung thành 1 vai trò "QuanLy": toàn quyền hệ
    -- thống (quản trị user, cấu hình) + quyền nghiệp vụ (báo cáo, giám sát).
);

CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    Username        NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255) NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Phone           NVARCHAR(20)  NULL,
    Email           NVARCHAR(100) NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    LastLoginAt     DATETIME2 NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2 NULL
    -- Không có cột RoleId ở đây: 1 User có thể có nhiều role cùng lúc (xem UserRoles).
);

CREATE TABLE UserRoles (                             -- quan hệ N-N User <-> Role
    UserRoleId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NOT NULL,
    RoleId          INT NOT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,          -- tạm vô hiệu 1 role mà không cần xóa (giữ lịch sử)
    AssignedBy      INT NULL,                        -- UserId của Quản lý đã gán role này
    AssignedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    RevokedAt       DATETIME2 NULL,
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId),
    CONSTRAINT UQ_UserRoles_UserId_RoleId UNIQUE (UserId, RoleId)
);

CREATE TABLE Employees (
    EmployeeId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NOT NULL UNIQUE,             -- 1-1 với Users
    EmployeeCode    NVARCHAR(20) NOT NULL UNIQUE,
    Position        NVARCHAR(50) NOT NULL,           -- Bep, PhucVu, ThuNgan, QuanLy
    HireDate        DATE NOT NULL,
    Salary          DECIMAL(18,2) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DangLamViec', -- DangLamViec, NghiViec, TamNghi
    CONSTRAINT FK_Employees_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

/* ======================================================================
   NHÓM 2: BÀN & KHU VỰC (Area, DiningTable, VIPRoomDetails)
   ====================================================================== */

CREATE TABLE Areas (
    AreaId          INT IDENTITY(1,1) PRIMARY KEY,
    AreaName        NVARCHAR(50) NOT NULL,           -- Tầng 1, Sân vườn, Phòng VIP...
    Description     NVARCHAR(255) NULL
);

CREATE TABLE DiningTables (
    TableId         INT IDENTITY(1,1) PRIMARY KEY,
    TableName       NVARCHAR(20) NOT NULL,           -- Bàn A1, Bàn B2...
    AreaId          INT NOT NULL,
    Capacity        INT NOT NULL DEFAULT 4,
    TableType       NVARCHAR(20) NOT NULL DEFAULT N'Thuong', -- Thuong, VIP (phòng riêng tư)
    Status          NVARCHAR(20) NOT NULL DEFAULT N'Trong', -- Trong, DangPhucVu, DaDat, DangDonDep
    CONSTRAINT FK_Tables_Areas FOREIGN KEY (AreaId) REFERENCES Areas(AreaId)
);

-- Thông tin riêng cho phòng VIP: 1-1 với DiningTables, CHỈ tồn tại
-- khi TableType = 'VIP' (kiểm tra bằng trigger bên dưới).
CREATE TABLE VIPRoomDetails (
    TableId             INT PRIMARY KEY,
    RoomFee             DECIMAL(18,2) NOT NULL DEFAULT 0,  -- phí thuê phòng / lượt
    MinimumBillAmount   DECIMAL(18,2) NOT NULL DEFAULT 0,  -- hóa đơn tối thiểu phải đạt; thiếu sẽ thu thêm chênh lệch
    DepositAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,  -- tiền cọc bắt buộc khi đặt trước phòng này
    Amenities           NVARCHAR(500) NULL,                 -- TV, dàn karaoke, máy lạnh riêng...
    Description         NVARCHAR(500) NULL,
    CONSTRAINT FK_VIPRoomDetails_DiningTables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId)
);
GO

-- Trigger đảm bảo chỉ bàn có TableType = 'VIP' mới được phép có dòng trong VIPRoomDetails
CREATE TRIGGER trg_VIPRoomDetails_ValidateTableType
ON VIPRoomDetails
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN DiningTables t ON t.TableId = i.TableId
        WHERE t.TableType <> N'VIP'
    )
    BEGIN
        RAISERROR (N'Chỉ bàn/phòng có TableType = ''VIP'' mới được khai báo VIPRoomDetails.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

CREATE INDEX IX_DiningTables_TableType ON DiningTables(TableType);
GO

-- Trigger chặn việc đổi TableType của bàn từ 'VIP' sang loại khác khi bàn đó vẫn còn VIPRoomDetails
CREATE TRIGGER trg_DiningTables_PreventOrphanVIPDetails
ON DiningTables
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(TableType) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN deleted d ON d.TableId = i.TableId
        INNER JOIN VIPRoomDetails v ON v.TableId = i.TableId
        WHERE d.TableType = N'VIP' AND i.TableType <> N'VIP'
    )
    BEGIN
        RAISERROR (N'Không thể đổi TableType khỏi ''VIP'' vì bàn/phòng này vẫn còn dữ liệu trong VIPRoomDetails. Hãy xóa VIPRoomDetails của bàn này trước.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

/* ======================================================================
   NHÓM 3: KHÁCH HÀNG & TÀI KHOẢN WEB (Customer, CustomerAccounts, CustomerRefreshTokens)
   ====================================================================== */

CREATE TABLE Customers (
    CustomerId      INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100) NOT NULL,
    Phone           NVARCHAR(20)  NOT NULL UNIQUE,
    Email           NVARCHAR(100) NULL,
    PasswordHash    NVARCHAR(500) NULL,              -- Mật khẩu Web khách hàng
    DateOfBirth     DATE NULL,
    LoyaltyPoint    INT NOT NULL DEFAULT 0,
    MemberLevel     NVARCHAR(20) NOT NULL DEFAULT N'ThanhVien', -- ThanhVien, Bac, Vang, KimCuong
    AvatarUrl       NVARCHAR(1000) NULL,             -- URL ảnh đại diện
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Tài khoản web cho khách hàng (mật khẩu + trạng thái khóa + reset token)
CREATE TABLE CustomerAccounts (
    CustomerId              INT NOT NULL PRIMARY KEY,
    PasswordHash            NVARCHAR(200) NOT NULL,
    IsActive                BIT NOT NULL CONSTRAINT DF_CustomerAccounts_IsActive DEFAULT(1),
    FailedLoginCount        INT NOT NULL CONSTRAINT DF_CustomerAccounts_Failed DEFAULT(0),
    LockedUntil             DATETIME2 NULL,
    PasswordResetTokenHash  NVARCHAR(100) NULL,
    PasswordResetExpiresAt  DATETIME2 NULL,
    CreatedAt               DATETIME2 NOT NULL CONSTRAINT DF_CustomerAccounts_CreatedAt DEFAULT(GETDATE()),
    LastLoginAt             DATETIME2 NULL,
    CONSTRAINT FK_CustomerAccounts_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

CREATE INDEX IX_CustomerAccounts_ResetToken ON CustomerAccounts(PasswordResetTokenHash);

-- Phiên đăng nhập Web (Refresh token)
CREATE TABLE CustomerRefreshTokens (
    TokenId         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerId      INT NOT NULL,
    TokenHash       NVARCHAR(100) NOT NULL,
    ExpiresAt       DATETIME2 NOT NULL,
    RevokedAt       DATETIME2 NULL,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_CustomerRefreshTokens_CreatedAt DEFAULT(GETDATE()),
    CONSTRAINT FK_CustomerRefreshTokens_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

CREATE UNIQUE INDEX UX_CustomerRefreshTokens_Hash ON CustomerRefreshTokens(TokenHash);
CREATE INDEX IX_CustomerRefreshTokens_Customer ON CustomerRefreshTokens(CustomerId, RevokedAt);
GO

/* ======================================================================
   NHÓM 4: ĐẶT BÀN & XẾP HÀNG CHỜ (Reservation, WaitingQueue)
   ====================================================================== */

CREATE TABLE Reservations (
    ReservationId   INT IDENTITY(1,1) PRIMARY KEY,
    TableId         INT NOT NULL,
    CustomerId      INT NULL,                        -- có thể null nếu khách vãng lai
    CustomerName    NVARCHAR(100) NULL,
    CustomerPhone   NVARCHAR(20) NULL,
    ReservationTime DATETIME2 NOT NULL,
    DurationMinutes INT NOT NULL CONSTRAINT DF_Reservations_DurationMinutes DEFAULT(30), -- Thời gian giữ bàn (phút)
    NumberOfGuests  INT NOT NULL DEFAULT 1,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChoXacNhan', -- ChoXacNhan, DaXacNhan, DaNhanBan, DaHuy, KhongDen, HoanTat
    DepositAmount   DECIMAL(18,2) NOT NULL DEFAULT 0, -- tiền cọc yêu cầu
    DepositPaid     BIT NOT NULL DEFAULT 0,
    DepositPaidAt   DATETIME2 NULL,
    Note            NVARCHAR(255) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CancelledByUserId INT NULL,
    CancelledAt     DATETIME2 NULL,
    Source          NVARCHAR(20) NOT NULL CONSTRAINT DF_Reservations_Source DEFAULT N'NhanVien', -- NhanVien, Web
    BookingGroupId  INT NULL,                         -- Mã nhóm đặt nhiều bàn cùng lúc
    CONSTRAINT FK_Reservations_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT FK_Reservations_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Reservations_CancelledByUser FOREIGN KEY (CancelledByUserId) REFERENCES Users(UserId)
);

-- Khách vãng lai xếp hàng chờ bàn
CREATE TABLE WaitingQueue (
    QueueId             INT IDENTITY(1,1) PRIMARY KEY,
    QueueDate           DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE), -- reset số thứ tự mỗi ngày
    QueueNumber         INT NOT NULL,
    CustomerName        NVARCHAR(100) NULL,
    CustomerPhone       NVARCHAR(20) NULL,
    NumberOfGuests      INT NOT NULL DEFAULT 1,
    Status              NVARCHAR(20) NOT NULL DEFAULT N'DangCho', -- DangCho, DaGoi, DaNhanBan, Huy, KhongDen
    CheckInTime         DATETIME2 NOT NULL DEFAULT GETDATE(),
    CalledTime          DATETIME2 NULL,
    SeatedTime          DATETIME2 NULL,
    AssignedTableId     INT NULL,
    EstimatedWaitMinutes INT NULL,
    CONSTRAINT FK_WaitingQueue_Tables FOREIGN KEY (AssignedTableId) REFERENCES DiningTables(TableId),
    CONSTRAINT UQ_WaitingQueue_DateNumber UNIQUE (QueueDate, QueueNumber)
);
GO

-- Trigger tự sinh số thứ tự tăng dần theo từng ngày
CREATE TRIGGER trg_WaitingQueue_AutoNumber
ON WaitingQueue
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    ;WITH NumberedInserted AS (
        SELECT
            i.*,
            ISNULL(i.QueueDate, CAST(GETDATE() AS DATE)) AS EffectiveQueueDate,
            ROW_NUMBER() OVER (
                PARTITION BY ISNULL(i.QueueDate, CAST(GETDATE() AS DATE))
                ORDER BY (SELECT NULL)
            ) AS RowSeq
        FROM inserted i
    ),
    MaxPerDate AS (
        SELECT DISTINCT
            ni.EffectiveQueueDate,
            ISNULL(MaxQ.MaxNumber, 0) AS BaseNumber
        FROM NumberedInserted ni
        OUTER APPLY (
            SELECT MAX(w.QueueNumber) AS MaxNumber
            FROM WaitingQueue w WITH (UPDLOCK, HOLDLOCK)
            WHERE w.QueueDate = ni.EffectiveQueueDate
        ) MaxQ
    )
    INSERT INTO WaitingQueue (QueueDate, QueueNumber, CustomerName, CustomerPhone, NumberOfGuests, Status, CheckInTime, EstimatedWaitMinutes)
    SELECT
        ni.EffectiveQueueDate,
        mp.BaseNumber + ni.RowSeq,
        ni.CustomerName,
        ni.CustomerPhone,
        ni.NumberOfGuests,
        ISNULL(ni.Status, N'DangCho'),
        ISNULL(ni.CheckInTime, GETDATE()),
        ni.EstimatedWaitMinutes
    FROM NumberedInserted ni
    INNER JOIN MaxPerDate mp ON mp.EffectiveQueueDate = ni.EffectiveQueueDate;

    COMMIT TRANSACTION;
END;
GO

CREATE INDEX IX_WaitingQueue_Date_Status ON WaitingQueue(QueueDate, Status);
GO

/* ======================================================================
   NHÓM 5: THỰC ĐƠN & KHO NGUYÊN LIỆU (Station, Category, MenuItem, Combo, Ingredient)
   ====================================================================== */

CREATE TABLE Stations (                              -- Bộ phận chế biến: Bếp, Quầy nước...
    StationId   INT IDENTITY(1,1) PRIMARY KEY,
    StationName NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName    NVARCHAR(50) NOT NULL,            -- Món khai vị, Món chính, Đồ uống...
    CategoryType    NVARCHAR(20) NOT NULL DEFAULT N'MonAn' -- MonAn, ThucUong
);

CREATE TABLE MenuItems (
    MenuItemId      INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId      INT NOT NULL,
    StationId       INT NULL,                         -- Món do bộ phận nào chế biến (Bếp / Quầy nước)
    ItemName        NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    Price           DECIMAL(18,2) NOT NULL,
    Unit            NVARCHAR(20) NULL DEFAULT N'Phan',
    ImageUrl        NVARCHAR(500) NULL,
    IsAvailable     BIT NOT NULL DEFAULT 1,           -- Còn bán / Hết món (Kho tự động cập nhật)
    IsFreeRefill    BIT NOT NULL DEFAULT 0,           -- Áp dụng chính sách gọi thêm
    FreeRefillLimit INT NOT NULL DEFAULT 2,           -- Số lần gọi thêm miễn phí
    RefillPrice     DECIMAL(18,2) NULL,               -- Giá tính từ lần vượt FreeRefillLimit
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_MenuItems_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT FK_MenuItems_Stations FOREIGN KEY (StationId) REFERENCES Stations(StationId)
);

CREATE TABLE Combos (
    ComboId         INT IDENTITY(1,1) PRIMARY KEY,
    ComboName       NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    Price           DECIMAL(18,2) NOT NULL,
    IsAvailable     BIT NOT NULL DEFAULT 1
);

CREATE TABLE ComboItems (                            -- Chi tiết món trong combo
    ComboItemId     INT IDENTITY(1,1) PRIMARY KEY,
    ComboId         INT NOT NULL,
    MenuItemId      INT NOT NULL,
    Quantity        INT NOT NULL DEFAULT 1,
    CONSTRAINT FK_ComboItems_Combos FOREIGN KEY (ComboId) REFERENCES Combos(ComboId),
    CONSTRAINT FK_ComboItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId)
);

-- Gán StationId cho Employees
ALTER TABLE Employees ADD StationId INT NULL
    CONSTRAINT FK_Employees_Stations FOREIGN KEY REFERENCES Stations(StationId);
GO

CREATE TABLE Ingredients (
    IngredientId    INT IDENTITY(1,1) PRIMARY KEY,
    IngredientName  NVARCHAR(100) NOT NULL,
    Unit            NVARCHAR(20)  NOT NULL,           -- gram, ml, cái...
    CurrentStock    DECIMAL(18,2) NOT NULL DEFAULT 0,
    MinStockAlert   DECIMAL(18,2) NOT NULL DEFAULT 0, -- Ngưỡng cảnh báo sắp hết
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Công thức món ăn (BOM): 1 món cần nhiều nguyên liệu, 1 nguyên liệu dùng cho nhiều món
CREATE TABLE MenuItemIngredients (
    MenuItemIngredientId INT IDENTITY(1,1) PRIMARY KEY,
    MenuItemId      INT NOT NULL,
    IngredientId    INT NOT NULL,
    QuantityRequired DECIMAL(18,2) NOT NULL,          -- Cần bao nhiêu Unit cho 1 phần món này
    CONSTRAINT FK_MII_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT FK_MII_Ingredients FOREIGN KEY (IngredientId) REFERENCES Ingredients(IngredientId),
    CONSTRAINT UQ_MenuItem_Ingredient UNIQUE (MenuItemId, IngredientId)
);

-- Sổ nhật ký nhập/xuất kho
CREATE TABLE StockTransactions (
    StockTransactionId INT IDENTITY(1,1) PRIMARY KEY,
    IngredientId    INT NOT NULL,
    ChangeType      NVARCHAR(20) NOT NULL,            -- NhapKho, XuatBanMon, DieuChinh, HuyMon
    QuantityChange  DECIMAL(18,2) NOT NULL,            -- Dương = nhập thêm, Âm = xuất đi
    RelatedOrderItemId INT NULL,
    Note            NVARCHAR(255) NULL,
    CreatedBy       INT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_StockTx_Ingredients FOREIGN KEY (IngredientId) REFERENCES Ingredients(IngredientId),
    CONSTRAINT FK_StockTx_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
GO

/* ======================================================================
   NHÓM 6: ĐẶT MÓN & BẾP (Order, OrderItem, KitchenTicket, DishStatus, OrderReview, Priorities)
   ====================================================================== */

CREATE TABLE Orders (
    OrderId         INT IDENTITY(1,1) PRIMARY KEY,
    TableId         INT NULL,                         -- NULL với đơn Mang Về
    OrderType       NVARCHAR(10) NOT NULL CONSTRAINT DF_Orders_OrderType DEFAULT N'TaiBan', -- TaiBan, MangVe
    TakeawayNumber  INT NULL,                         -- Số thứ tự đơn mang về
    EmployeeId      INT NOT NULL,
    CustomerId      INT NULL,
    OrderTime       DATETIME2 NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DangMo', -- DangMo, DaThanhToan, DaHuy
    PaymentRequestedByUserId INT NULL,                -- User yêu cầu thanh toán
    PaymentRequestedAt DATETIME2 NULL,
    Note            NVARCHAR(255) NULL,
    ClientRequestId UNIQUEIDENTIFIER NULL,            -- Khóa chống trùng lặp từ Web (Idempotent)
    ClientRequestHash VARCHAR(64) NULL,               -- Hash request từ Web
    Source          NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_Source DEFAULT N'NhanVien', -- NhanVien, Web
    TotalAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,  -- Trigger tự động đồng bộ
    RowVer          ROWVERSION NOT NULL,               -- Kiểm soát concurrency
    CONSTRAINT FK_Orders_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT FK_Orders_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Orders_PaymentRequestedByUser FOREIGN KEY (PaymentRequestedByUserId) REFERENCES Users(UserId),
    CONSTRAINT CK_Orders_OrderType_Table CHECK (
        (OrderType = N'TaiBan' AND TableId IS NOT NULL)
        OR
        (OrderType = N'MangVe' AND TableId IS NULL AND TakeawayNumber IS NOT NULL)
    )
);

CREATE TABLE OrderItems (
    OrderItemId     INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,
    MenuItemId      INT NULL,                         -- 1 trong 2: MenuItemId hoặc ComboId
    ComboId         INT NULL,
    Quantity        INT NOT NULL DEFAULT 1,
    UnitPrice       DECIMAL(18,2) NOT NULL,            -- Giá tại thời điểm order
    Note            NVARCHAR(255) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'MoiGoi', -- MoiGoi, DangCheBien, DaCheBien, DaPhucVu, DaHuy
    CancelReason    NVARCHAR(255) NULL,
    CancelledByUserId INT NULL,
    IsRefillRequest      BIT NOT NULL DEFAULT 0,       -- True = lần gọi thêm
    IsTakeaway      BIT NOT NULL CONSTRAINT DF_OrderItems_IsTakeaway DEFAULT 0, -- Món khách xin mang về
    RefillSequenceNumber INT NULL,                     -- Lần gọi thêm thứ mấy (trigger tự tính)
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    RowVer          ROWVERSION NOT NULL,
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT FK_OrderItems_Combos FOREIGN KEY (ComboId) REFERENCES Combos(ComboId),
    CONSTRAINT FK_OrderItems_CancelledByUser FOREIGN KEY (CancelledByUserId) REFERENCES Users(UserId),
    CONSTRAINT CK_OrderItems_ItemType CHECK (
        (MenuItemId IS NOT NULL AND ComboId IS NULL) OR
        (MenuItemId IS NULL AND ComboId IS NOT NULL)
    )
);

-- Khóa ngoại từ StockTransactions đến OrderItems
ALTER TABLE StockTransactions
    ADD CONSTRAINT FK_StockTx_OrderItems FOREIGN KEY (RelatedOrderItemId) REFERENCES OrderItems(OrderItemId);
GO

CREATE TABLE KitchenTickets (
    KitchenTicketId INT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId     INT NOT NULL,
    StationId       INT NULL,                          -- Phiếu gửi tới bộ phận nào (Bếp / Quầy nước)
    SentTime        DATETIME2 NOT NULL DEFAULT GETDATE(),
    ChefId          INT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChoXuLy', -- ChoXuLy, DangCheBien, HoanThanh
    CompletedTime   DATETIME2 NULL,
    CONSTRAINT FK_KitchenTickets_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId),
    CONSTRAINT FK_KitchenTickets_Employees FOREIGN KEY (ChefId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_KitchenTickets_Stations FOREIGN KEY (StationId) REFERENCES Stations(StationId)
);

CREATE TABLE DishStatusHistory (
    HistoryId       INT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId     INT NOT NULL,
    Status          NVARCHAR(20) NOT NULL,
    ChangedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    ChangedBy       INT NULL,
    CONSTRAINT FK_DishStatusHistory_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId),
    CONSTRAINT FK_DishStatusHistory_Users FOREIGN KEY (ChangedBy) REFERENCES Users(UserId)
);

-- Đánh giá món ăn và trải nghiệm đơn hàng của khách hàng
CREATE TABLE OrderReviews (
    ReviewId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderReviews PRIMARY KEY,
    OrderId         INT NOT NULL,
    OrderItemId     INT NULL,                         -- Có giá trị = đánh giá món; NULL = đánh giá đơn chung
    CustomerId      INT NOT NULL,
    Rating          TINYINT NOT NULL,
    Comment         NVARCHAR(1000) NULL,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_OrderReviews_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2 NOT NULL CONSTRAINT DF_OrderReviews_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    IsVisible       BIT NOT NULL CONSTRAINT DF_OrderReviews_IsVisible DEFAULT (1),
    ModerationStatus NVARCHAR(16) NOT NULL CONSTRAINT DF_OrderReviews_ModerationStatus DEFAULT (N'Public'),
    CONSTRAINT FK_OrderReviews_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderReviews_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId),
    CONSTRAINT FK_OrderReviews_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_OrderReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
);

-- Bảng đánh dấu ưu tiên chế biến từ ứng dụng Mobile
CREATE TABLE MobileKitchenPriorities (
    PriorityId          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PriorityScope       NVARCHAR(20) NOT NULL,         -- 'Table' hoặc 'Item'
    TableId             INT NULL,
    OrderItemId         INT NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_MobileKitchenPriorities_IsActive DEFAULT (1),
    RequestedByUserId   INT NULL,
    RequestedAt         DATETIME2 NOT NULL CONSTRAINT DF_MobileKitchenPriorities_RequestedAt DEFAULT (GETDATE()),
    UpdatedAt           DATETIME2 NOT NULL CONSTRAINT DF_MobileKitchenPriorities_UpdatedAt DEFAULT (GETDATE()),
    CONSTRAINT FK_MobileKitchenPriorities_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT FK_MobileKitchenPriorities_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId),
    CONSTRAINT FK_MobileKitchenPriorities_Users FOREIGN KEY (RequestedByUserId) REFERENCES Users(UserId)
);
GO

/* ----------------------------------------------------------------------
   TRIGGER 1: Tự động đồng bộ Orders.TotalAmount khi OrderItems thay đổi
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_OrderItems_SyncOrderTotal
ON OrderItems
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH AffectedOrders AS (
        SELECT OrderId FROM inserted
        UNION
        SELECT OrderId FROM deleted
    )
    UPDATE o
    SET o.TotalAmount = ISNULL((
            SELECT SUM(oi.Quantity * oi.UnitPrice)
            FROM OrderItems oi
            WHERE oi.OrderId = o.OrderId
              AND oi.Status <> N'DaHuy'          -- Món đã hủy không tính vào tổng tiền
        ), 0)
    FROM Orders o
    INNER JOIN AffectedOrders ao ON ao.OrderId = o.OrderId;
END;
GO

/* ----------------------------------------------------------------------
   TRIGGER 2: Trừ kho nguyên liệu khi order món lẻ hoặc combo
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_OrderItems_DeductStock
ON OrderItems
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Trường hợp 1: Gọi món lẻ trực tiếp
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'XuatBanMon',
        -1 * bom.QuantityRequired * ins.Quantity,
        ins.OrderItemId,
        N'Tự động trừ khi order món lẻ'
    FROM inserted ins
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ins.MenuItemId
    WHERE ins.MenuItemId IS NOT NULL;

    -- Trường hợp 2: Gọi Combo -> Trừ kho từng món con trong combo
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'XuatBanMon',
        -1 * bom.QuantityRequired * ci.Quantity * ins.Quantity,
        ins.OrderItemId,
        N'Tự động trừ khi order combo'
    FROM inserted ins
    INNER JOIN ComboItems ci ON ci.ComboId = ins.ComboId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ci.MenuItemId
    WHERE ins.ComboId IS NOT NULL;
END;
GO

/* ----------------------------------------------------------------------
   TRIGGER 3: Áp dụng chính sách refill cho các món có IsFreeRefill = 1
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_OrderItems_ApplyRefillPolicy
ON OrderItems
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH RefillRows AS (
        SELECT
            ins.OrderItemId,
            ins.OrderId,
            ins.MenuItemId,
            (
                SELECT COUNT(*)
                FROM OrderItems oi2
                WHERE oi2.OrderId = ins.OrderId
                  AND oi2.MenuItemId = ins.MenuItemId
                  AND oi2.IsRefillRequest = 1
                  AND oi2.OrderItemId < ins.OrderItemId
            ) + 1 AS SeqNumber
        FROM inserted ins
        WHERE ins.IsRefillRequest = 1
    )
    UPDATE oi
    SET oi.RefillSequenceNumber = r.SeqNumber,
        oi.UnitPrice = CASE
            WHEN r.SeqNumber <= mi.FreeRefillLimit THEN 0
            ELSE ISNULL(mi.RefillPrice, mi.Price)
        END
    FROM OrderItems oi
    INNER JOIN RefillRows r ON r.OrderItemId = oi.OrderItemId
    INNER JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId;
END;
GO

-- Đảm bảo thứ tự trigger: ApplyRefillPolicy chạy đầu, SyncOrderTotal chạy cuối
EXEC sp_settriggerorder @triggername = N'dbo.trg_OrderItems_ApplyRefillPolicy', @order = 'First', @stmttype = 'INSERT';
EXEC sp_settriggerorder @triggername = N'dbo.trg_OrderItems_SyncOrderTotal', @order = 'Last', @stmttype = 'INSERT';
GO

/* ----------------------------------------------------------------------
   TRIGGER 4: Cập nhật CurrentStock và IsAvailable khi xuất/nhập kho
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_StockTransactions_UpdateStock
ON StockTransactions
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE i
    SET i.CurrentStock = i.CurrentStock + t.TotalChange,
        i.UpdatedAt = GETDATE()
    FROM Ingredients i
    INNER JOIN (
        SELECT IngredientId, SUM(QuantityChange) AS TotalChange
        FROM inserted
        GROUP BY IngredientId
    ) t ON t.IngredientId = i.IngredientId;

    UPDATE mi
    SET mi.IsAvailable = CASE
        WHEN NOT EXISTS (
            SELECT 1
            FROM MenuItemIngredients bom
            INNER JOIN Ingredients ing ON ing.IngredientId = bom.IngredientId
            WHERE bom.MenuItemId = mi.MenuItemId
              AND ing.CurrentStock < bom.QuantityRequired
        ) THEN 1 ELSE 0 END
    FROM MenuItems mi
    WHERE mi.MenuItemId IN (
        SELECT DISTINCT bom.MenuItemId
        FROM MenuItemIngredients bom
        INNER JOIN inserted t ON t.IngredientId = bom.IngredientId
    );
END;
GO

/* ----------------------------------------------------------------------
   VIEW 1: Xem nhanh số phần khả dụng của món theo công thức kho
   ---------------------------------------------------------------------- */
CREATE VIEW vw_MenuItemAvailability AS
SELECT
    mi.MenuItemId,
    mi.ItemName,
    mi.IsAvailable,
    CASE WHEN EXISTS (
        SELECT 1 FROM MenuItemIngredients r WHERE r.MenuItemId = mi.MenuItemId
    ) THEN 1 ELSE 0 END AS HasRecipeTracked,
    (
        SELECT MIN(FLOOR(ing.CurrentStock / bom.QuantityRequired))
        FROM MenuItemIngredients bom
        INNER JOIN Ingredients ing ON ing.IngredientId = bom.IngredientId
        WHERE bom.MenuItemId = mi.MenuItemId
    ) AS EstimatedServingsLeft
FROM MenuItems mi;
GO

/* ======================================================================
   NHÓM 7: HÓA ĐƠN & THANH TOÁN (Invoice, Payment, DiscountCampaign)
   ====================================================================== */

CREATE TABLE DiscountCampaigns (
    CampaignId          INT IDENTITY(1,1) PRIMARY KEY,
    CampaignName        NVARCHAR(100) NOT NULL,
    StartDate           DATETIME2 NOT NULL,
    EndDate             DATETIME2 NOT NULL,
    DiscountPercent     DECIMAL(5,2) NOT NULL,
    RequireMembership   BIT NOT NULL DEFAULT 0,
    MinVisitCount       INT NULL,
    VisitLookbackDays   INT NULL,
    IsActive            BIT NOT NULL DEFAULT 1,
    Note                NVARCHAR(255) NULL,
    CreatedBy           INT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_DiscountCampaigns_DateRange CHECK (EndDate > StartDate),
    CONSTRAINT CK_DiscountCampaigns_Percent   CHECK (DiscountPercent BETWEEN 0 AND 100),
    CONSTRAINT CK_DiscountCampaigns_MembershipRule CHECK (
        (RequireMembership = 0 AND MinVisitCount IS NULL AND VisitLookbackDays IS NULL)
        OR
        (RequireMembership = 1 AND MinVisitCount IS NOT NULL AND VisitLookbackDays IS NOT NULL
         AND MinVisitCount > 0 AND VisitLookbackDays > 0)
    ),
    CONSTRAINT FK_DiscountCampaigns_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
GO

CREATE INDEX IX_DiscountCampaigns_DateRange ON DiscountCampaigns(StartDate, EndDate) WHERE IsActive = 1;
GO

CREATE TRIGGER trg_DiscountCampaigns_PreventOverlap
ON DiscountCampaigns
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN DiscountCampaigns c
            ON c.CampaignId <> i.CampaignId
           AND c.IsActive = 1
           AND c.RequireMembership = i.RequireMembership
           AND i.IsActive = 1
           AND i.StartDate < c.EndDate
           AND i.EndDate   > c.StartDate
    )
    BEGIN
        RAISERROR (N'Khoảng ngày của chiến dịch giảm giá bị giao với một chiến dịch khác cùng loại đang hoạt động.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

CREATE FUNCTION fn_GetActiveDiscountPercent
(
    @CustomerId INT,
    @CheckDate  DATETIME2
)
RETURNS DECIMAL(5,2)
AS
BEGIN
    DECLARE @DiscountPercent DECIMAL(5,2) = 0;

    IF @CustomerId IS NOT NULL
    BEGIN
        SELECT TOP 1 @DiscountPercent = c.DiscountPercent
        FROM DiscountCampaigns c
        WHERE c.IsActive = 1
          AND c.RequireMembership = 1
          AND @CheckDate >= c.StartDate
          AND @CheckDate <  c.EndDate
          AND (
                SELECT COUNT(DISTINCT o.OrderId)
                FROM Orders o
                WHERE o.CustomerId = @CustomerId
                  AND o.Status <> N'DaHuy'
                  AND o.OrderTime >= DATEADD(DAY, -c.VisitLookbackDays, @CheckDate)
                  AND o.OrderTime <  @CheckDate
              ) >= c.MinVisitCount
        ORDER BY c.DiscountPercent DESC;
    END

    IF @DiscountPercent = 0
    BEGIN
        SELECT TOP 1 @DiscountPercent = c.DiscountPercent
        FROM DiscountCampaigns c
        WHERE c.IsActive = 1
          AND c.RequireMembership = 0
          AND @CheckDate >= c.StartDate
          AND @CheckDate <  c.EndDate
        ORDER BY c.DiscountPercent DESC;
    END

    RETURN ISNULL(@DiscountPercent, 0);
END;
GO

CREATE FUNCTION fn_GetVIPRoomExtraCharge
(
    @TableId INT,
    @SubTotal DECIMAL(18,2)
)
RETURNS @Result TABLE (RoomFee DECIMAL(18,2), MinimumGap DECIMAL(18,2))
AS
BEGIN
    DECLARE @RoomFee DECIMAL(18,2) = 0;
    DECLARE @MinBill DECIMAL(18,2) = 0;
    DECLARE @Gap DECIMAL(18,2) = 0;

    SELECT @RoomFee = v.RoomFee, @MinBill = v.MinimumBillAmount
    FROM DiningTables t
    INNER JOIN VIPRoomDetails v ON v.TableId = t.TableId
    WHERE t.TableId = @TableId AND t.TableType = N'VIP';

    IF @MinBill > @SubTotal
        SET @Gap = @MinBill - @SubTotal;

    INSERT INTO @Result VALUES (ISNULL(@RoomFee, 0), @Gap);
    RETURN;
END;
GO

CREATE TABLE Invoices (
    InvoiceId       INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,                    -- 1 Order có thể chia thành nhiều hóa đơn
    InvoiceDate     DATETIME2 NOT NULL DEFAULT GETDATE(),
    SubTotal        DECIMAL(18,2) NOT NULL,
    DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    AppliedCampaignId INT NULL,                        -- Chiến dịch giảm giá áp dụng
    VoucherId       INT NULL,
    TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    VIPRoomFee      DECIMAL(18,2) NOT NULL DEFAULT 0, -- Phí thuê phòng VIP
    VIPMinimumGap   DECIMAL(18,2) NOT NULL DEFAULT 0, -- Chênh lệch hóa đơn tối thiểu phòng VIP
    TotalAmount     DECIMAL(18,2) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChuaThanhToan', -- ChuaThanhToan, DaThanhToan, DaHuy
    CreatedBy       INT NULL,
    RequestedByUserId INT NULL,                       -- User yêu cầu thanh toán
    ConfirmedByUserId INT NULL,                       -- User thu tiền / xác nhận thanh toán
    RowVer          ROWVERSION NOT NULL,               -- Kiểm soát concurrency
    CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_Invoices_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Invoices_RequestedByUser FOREIGN KEY (RequestedByUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Invoices_ConfirmedByUser FOREIGN KEY (ConfirmedByUserId) REFERENCES Users(UserId)
);

CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,
    PaymentMethod   NVARCHAR(20) NOT NULL,           -- TienMat, TheNganHang, ViDienTu, ChuyenKhoan
    Amount          DECIMAL(18,2) NOT NULL,
    PaymentTime     DATETIME2 NOT NULL DEFAULT GETDATE(),
    TransactionCode NVARCHAR(100) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ThanhCong', -- ThanhCong, ThatBai
    Note            NVARCHAR(255) NULL,
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId)
);
GO

/* ======================================================================
   NHÓM 8: NHÂN SỰ & CHẤM CÔNG (Shift, ShiftAssignment, Areas, Tasks, Attendance)
   ====================================================================== */

CREATE TABLE Shifts (
    ShiftId         INT IDENTITY(1,1) PRIMARY KEY,
    ShiftName       NVARCHAR(50) NOT NULL,
    StartTime       TIME NOT NULL,
    EndTime         TIME NOT NULL
);

CREATE TABLE ShiftAssignments (
    ShiftAssignmentId INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL,
    ShiftId         INT NOT NULL,
    AreaId          INT NULL,                        -- Khu vực chính được phân công
    WorkDate        DATE NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DaXepLich', -- DaXepLich, DaDiemDanh, VangMat, DaHoanThanh
    CheckInTime     DATETIME2 NULL,
    CheckOutTime    DATETIME2 NULL,
    CONSTRAINT FK_ShiftAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_ShiftAssignments_Shifts FOREIGN KEY (ShiftId) REFERENCES Shifts(ShiftId),
    CONSTRAINT FK_ShiftAssignments_Areas FOREIGN KEY (AreaId) REFERENCES Areas(AreaId),
    CONSTRAINT UQ_Employee_Shift_Date UNIQUE (EmployeeId, ShiftId, WorkDate)
);

-- Phân công nhiều khu vực cho một ca làm việc
CREATE TABLE ShiftAssignmentAreas (
    ShiftAssignmentId INT NOT NULL,
    AreaId            INT NOT NULL,
    CONSTRAINT PK_ShiftAssignmentAreas PRIMARY KEY (ShiftAssignmentId, AreaId),
    CONSTRAINT FK_ShiftAssignmentAreas_ShiftAssignments FOREIGN KEY (ShiftAssignmentId) REFERENCES ShiftAssignments(ShiftAssignmentId) ON DELETE CASCADE,
    CONSTRAINT FK_ShiftAssignmentAreas_Areas FOREIGN KEY (AreaId) REFERENCES Areas(AreaId)
);

-- Phân công nhân viên phụ trách khu vực (dài hạn hoặc theo ngày)
CREATE TABLE EmployeeAreaAssignments (
    EmployeeAreaAssignmentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId               INT NOT NULL,
    AreaId                   INT NOT NULL,
    WorkDate                 DATE NULL,                       -- NULL = phân công dài hạn; có giá trị = theo ngày
    IsActive                 BIT NOT NULL CONSTRAINT DF_EmployeeAreaAssignments_IsActive DEFAULT(1),
    AssignedBy               INT NULL,
    AssignedAt               DATETIME2 NOT NULL CONSTRAINT DF_EmployeeAreaAssignments_AssignedAt DEFAULT(GETDATE()),
    CONSTRAINT FK_EmployeeAreaAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_EmployeeAreaAssignments_Areas FOREIGN KEY (AreaId) REFERENCES Areas(AreaId),
    CONSTRAINT FK_EmployeeAreaAssignments_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId)
);

CREATE TABLE Tasks (
    TaskId          INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL,
    TaskName        NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(500) NULL,
    DueDate         DATETIME2 NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChuaLam', -- ChuaLam, DangLam, HoanThanh
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Tasks_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId)
);

-- Cấu hình tọa độ GPS quán và bán kính chấm công
CREATE TABLE AttendanceSettings (
    SettingId               INT NOT NULL CONSTRAINT PK_AttendanceSettings PRIMARY KEY,
    RestaurantLatitude      DECIMAL(9, 6) NULL,
    RestaurantLongitude     DECIMAL(9, 6) NULL,
    RadiusMeters            FLOAT NOT NULL CONSTRAINT DF_AttendanceSettings_Radius DEFAULT (100),
    MaxAccuracyMeters       FLOAT NOT NULL CONSTRAINT DF_AttendanceSettings_Accuracy DEFAULT (50),
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               INT NULL,
    CONSTRAINT FK_AttendanceSettings_Users FOREIGN KEY (UpdatedBy) REFERENCES Users(UserId)
);
GO

/* ======================================================================
   NHÓM 9: TÍCH ĐIỂM & ƯU ĐÃI (LoyaltyPointHistory, Promotion, Voucher)
   ====================================================================== */

CREATE TABLE LoyaltyPointHistory (
    HistoryId       INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT NOT NULL,
    Points          INT NOT NULL,
    Type            NVARCHAR(20) NOT NULL,            -- TichDiem, DoiDiem
    RelatedInvoiceId INT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_LoyaltyPoint_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_LoyaltyPoint_Invoices FOREIGN KEY (RelatedInvoiceId) REFERENCES Invoices(InvoiceId)
);

CREATE TABLE Promotions (
    PromotionId     INT IDENTITY(1,1) PRIMARY KEY,
    PromotionName   NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    DiscountType    NVARCHAR(20) NOT NULL,            -- PhanTram, SoTien
    DiscountValue   DECIMAL(18,2) NOT NULL,
    StartDate       DATETIME2 NOT NULL,
    EndDate         DATETIME2 NOT NULL,
    IsActive        BIT NOT NULL DEFAULT 1
);

CREATE TABLE Vouchers (
    VoucherId       INT IDENTITY(1,1) PRIMARY KEY,
    VoucherCode     NVARCHAR(30) NOT NULL UNIQUE,
    PromotionId     INT NOT NULL,
    CustomerId      INT NULL,
    IsUsed          BIT NOT NULL DEFAULT 0,
    UsedAt          DATETIME2 NULL,
    ExpiryDate      DATETIME2 NOT NULL,
    CONSTRAINT FK_Vouchers_Promotions FOREIGN KEY (PromotionId) REFERENCES Promotions(PromotionId),
    CONSTRAINT FK_Vouchers_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

ALTER TABLE Invoices
    ADD CONSTRAINT FK_Invoices_Vouchers FOREIGN KEY (VoucherId) REFERENCES Vouchers(VoucherId);

ALTER TABLE Invoices
    ADD CONSTRAINT FK_Invoices_DiscountCampaigns FOREIGN KEY (AppliedCampaignId) REFERENCES DiscountCampaigns(CampaignId);
GO

/* ======================================================================
   NHÓM 10: THÔNG BÁO, AUDIT & PHÂN QUYỀN MOBILE (Notification, Audit, Permissions)
   ====================================================================== */

CREATE TABLE Notifications (
    NotificationId  INT IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(200) NOT NULL,
    Content         NVARCHAR(1000) NOT NULL,
    NotificationType NVARCHAR(30) NOT NULL,           -- DonHang, DatBan, CaLam, HeThong, KhuyenMai, CanhBaoKho
    TargetRole      NVARCHAR(50) NULL,
    TargetUserId    INT NULL,
    CustomerId      INT NULL,                         -- Khách hàng nhận thông báo
    EventKey        NVARCHAR(200) NULL,               -- Khóa chống trùng thông báo
    RelatedEntityType NVARCHAR(50) NULL,
    RelatedEntityId INT NULL,
    IsRead          BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (TargetUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Notifications_Roles FOREIGN KEY (TargetRole) REFERENCES Roles(RoleName),
    CONSTRAINT FK_Notifications_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

CREATE TABLE NotificationDeliveryLog (
    DeliveryId      INT IDENTITY(1,1) PRIMARY KEY,
    NotificationId  INT NOT NULL,
    Channel         NVARCHAR(20) NOT NULL,            -- SignalR, Email, FCM
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DaGui',
    SentAt          DATETIME2 NOT NULL DEFAULT GETDATE(),
    ErrorMessage    NVARCHAR(500) NULL,
    CONSTRAINT FK_DeliveryLog_Notifications FOREIGN KEY (NotificationId) REFERENCES Notifications(NotificationId)
);

CREATE TABLE ScheduledReminders (
    ReminderId      INT IDENTITY(1,1) PRIMARY KEY,
    ReminderType    NVARCHAR(30) NOT NULL,
    TargetId        INT NULL,
    ReminderTime    DATETIME2 NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChoXuLy',
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE AuditLogs (
    AuditLogId      BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NULL,
    Action          NVARCHAR(20) NOT NULL,
    TableName       NVARCHAR(50) NOT NULL,
    RecordId        NVARCHAR(50) NULL,
    OldValue        NVARCHAR(MAX) NULL,
    NewValue        NVARCHAR(MAX) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

-- Nhật ký kiểm toán thao tác từ Mobile / WinForms
CREATE TABLE MobileOperationLogs (
    LogId               BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId              INT NULL,
    EmployeeCode        NVARCHAR(50) NULL,
    HttpMethod          NVARCHAR(12) NOT NULL,
    RequestPath         NVARCHAR(300) NOT NULL,
    TableIds            NVARCHAR(500) NULL,
    AreaNames           NVARCHAR(500) NULL,
    OutsideAssignedArea BIT NOT NULL CONSTRAINT DF_MobileOperationLogs_Outside DEFAULT(0),
    StatusCode          INT NOT NULL,
    CreatedAt           DATETIME2 NOT NULL CONSTRAINT DF_MobileOperationLogs_CreatedAt DEFAULT(GETDATE()),
    CONSTRAINT FK_MobileOperationLogs_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

-- Danh mục quyền ứng dụng Mobile
CREATE TABLE MobilePermissions (
    PermissionKey   NVARCHAR(100) NOT NULL PRIMARY KEY,
    DisplayName     NVARCHAR(150) NOT NULL,
    Description     NVARCHAR(500) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_MobilePermissions_IsActive DEFAULT(1)
);

-- Phân quyền Mobile cho từng Role
CREATE TABLE RoleMobilePermissions (
    RoleId          INT NOT NULL,
    PermissionKey   NVARCHAR(100) NOT NULL,
    IsAllowed       BIT NOT NULL CONSTRAINT DF_RoleMobilePermissions_IsAllowed DEFAULT(1),
    UpdatedAt       DATETIME2 NOT NULL CONSTRAINT DF_RoleMobilePermissions_UpdatedAt DEFAULT(GETDATE()),
    CONSTRAINT PK_RoleMobilePermissions PRIMARY KEY(RoleId, PermissionKey),
    CONSTRAINT FK_RoleMobilePermissions_Roles FOREIGN KEY(RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_RoleMobilePermissions_Permissions FOREIGN KEY(PermissionKey) REFERENCES MobilePermissions(PermissionKey)
);
GO

/* ======================================================================
   NHÓM 11: NGHIỆP VỤ NÂNG CAO (Ghép bàn, Chia hóa đơn, Đổi giá, Phản hồi)
   ====================================================================== */

-- Ghép bàn: 1 Order có thể trải trên nhiều bàn
CREATE TABLE OrderTables (
    OrderTableId    INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,
    TableId         INT NOT NULL,
    IsPrimaryTable  BIT NOT NULL DEFAULT 0,           -- Bàn chính hay bàn ghép thêm
    CONSTRAINT FK_OrderTables_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderTables_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT UQ_OrderTables_Order_Table UNIQUE (OrderId, TableId)
);

-- Chia hóa đơn: 1 Order có thể tách thành nhiều Invoice
CREATE TABLE InvoiceItems (
    InvoiceItemId   INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,
    OrderItemId     INT NOT NULL,
    SplitQuantity   INT NOT NULL,
    SplitAmount     DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_InvoiceItems_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId)
);
GO

-- Hoàn kho khi hủy món chưa chế biến (trạng thái 'MoiGoi')
CREATE TRIGGER trg_OrderItems_RestoreStockOnCancel
ON OrderItems
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Status) RETURN;

    -- Món lẻ
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'HuyMon',
        bom.QuantityRequired * ins.Quantity,
        ins.OrderItemId,
        N'Tự động hoàn kho khi hủy món trước khi chế biến'
    FROM inserted ins
    INNER JOIN deleted del ON del.OrderItemId = ins.OrderItemId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ins.MenuItemId
    WHERE ins.Status = N'DaHuy'
      AND del.Status = N'MoiGoi'
      AND ins.MenuItemId IS NOT NULL;

    -- Combo
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'HuyMon',
        bom.QuantityRequired * ci.Quantity * ins.Quantity,
        ins.OrderItemId,
        N'Tự động hoàn kho khi hủy combo trước khi chế biến'
    FROM inserted ins
    INNER JOIN deleted del ON del.OrderItemId = ins.OrderItemId
    INNER JOIN ComboItems ci ON ci.ComboId = ins.ComboId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ci.MenuItemId
    WHERE ins.Status = N'DaHuy'
      AND del.Status = N'MoiGoi'
      AND ins.ComboId IS NOT NULL;
END;
GO

-- Lịch sử giá bán món ăn
CREATE TABLE MenuItemPriceHistory (
    PriceHistoryId  INT IDENTITY(1,1) PRIMARY KEY,
    MenuItemId      INT NOT NULL,
    OldPrice        DECIMAL(18,2) NOT NULL,
    NewPrice        DECIMAL(18,2) NOT NULL,
    ChangedBy       INT NULL,
    ChangedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_PriceHistory_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT FK_PriceHistory_Users FOREIGN KEY (ChangedBy) REFERENCES Users(UserId)
);
GO

CREATE TRIGGER trg_MenuItems_LogPriceChange
ON MenuItems
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Price) RETURN;

    INSERT INTO MenuItemPriceHistory (MenuItemId, OldPrice, NewPrice)
    SELECT d.MenuItemId, d.Price, i.Price
    FROM inserted i
    INNER JOIN deleted d ON d.MenuItemId = i.MenuItemId
    WHERE i.Price <> d.Price;
END;
GO

-- Đánh giá / Phản hồi sau khi thanh toán hóa đơn
CREATE TABLE Feedbacks (
    FeedbackId      INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,
    CustomerId      INT NULL,
    Rating          TINYINT NOT NULL,
    Comment         NVARCHAR(1000) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Feedbacks_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_Feedbacks_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_Feedbacks_Rating CHECK (Rating BETWEEN 1 AND 5)
);

CREATE TABLE FeedbackItems (
    FeedbackItemId  INT IDENTITY(1,1) PRIMARY KEY,
    FeedbackId      INT NOT NULL,
    MenuItemId      INT NOT NULL,
    Rating          TINYINT NOT NULL,
    CONSTRAINT FK_FeedbackItems_Feedbacks FOREIGN KEY (FeedbackId) REFERENCES Feedbacks(FeedbackId),
    CONSTRAINT FK_FeedbackItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT CK_FeedbackItems_Rating CHECK (Rating BETWEEN 1 AND 5)
);
GO

-- Cảnh báo nguyên liệu sắp hết qua Notification
CREATE TRIGGER trg_Ingredients_LowStockAlert
ON Ingredients
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(CurrentStock) RETURN;

    INSERT INTO Notifications (Title, Content, NotificationType, TargetRole, RelatedEntityType, RelatedEntityId)
    SELECT
        N'Cảnh báo sắp hết nguyên liệu',
        N'Nguyên liệu "' + i.IngredientName + N'" chỉ còn ' + CAST(i.CurrentStock AS NVARCHAR(20))
            + N' ' + i.Unit + N', dưới ngưỡng cảnh báo (' + CAST(i.MinStockAlert AS NVARCHAR(20)) + N').',
        N'CanhBaoKho',
        N'QuanLy',
        N'Ingredient',
        i.IngredientId
    FROM inserted i
    INNER JOIN deleted d ON d.IngredientId = i.IngredientId
    WHERE i.MinStockAlert > 0
      AND i.CurrentStock < i.MinStockAlert
      AND d.CurrentStock >= i.MinStockAlert;
END;
GO

-- Gợi ý món ăn yêu thích dựa trên lịch sử khách hàng
CREATE VIEW vw_CustomerFavoriteItems AS
SELECT
    o.CustomerId,
    oi.MenuItemId,
    mi.ItemName,
    COUNT(*)            AS TimesOrdered,
    SUM(oi.Quantity)    AS TotalQuantityOrdered,
    MAX(o.OrderTime)    AS LastOrderedAt
FROM Orders o
INNER JOIN OrderItems oi ON oi.OrderId = o.OrderId
INNER JOIN MenuItems mi ON mi.MenuItemId = oi.MenuItemId
WHERE o.CustomerId IS NOT NULL
  AND oi.Status <> N'DaHuy'
GROUP BY o.CustomerId, oi.MenuItemId, mi.ItemName;
GO

/* ======================================================================
   TỐI ƯU HÓA: INDEX TOÀN DIỆN (CHỈ MỤC TRUY VẤN VÀ TÍNH TOÁN HIỆU NĂNG)
   ====================================================================== */

-- Index Người dùng & Quyền
CREATE INDEX IX_UserRoles_UserId_IsActive  ON UserRoles(UserId, IsActive);

-- Index Bàn & Khu vực
CREATE INDEX IX_DiningTables_Status        ON DiningTables(Status);

-- Index Đặt bàn
CREATE INDEX IX_Reservations_Time_Status   ON Reservations(ReservationTime, Status);
CREATE INDEX IX_Reservations_Customer      ON Reservations(CustomerId) WHERE CustomerId IS NOT NULL;
CREATE INDEX IX_Reservations_CustomerId_ReservationTime ON Reservations(CustomerId, ReservationTime DESC) WHERE CustomerId IS NOT NULL;
CREATE INDEX IX_Reservations_BookingGroup  ON Reservations(BookingGroupId) WHERE BookingGroupId IS NOT NULL;

-- Index Đặt món
CREATE INDEX IX_Orders_Status_TableId      ON Orders(Status, TableId);
CREATE INDEX IX_Orders_CustomerId_OrderTime ON Orders(CustomerId, OrderTime) WHERE CustomerId IS NOT NULL;
CREATE UNIQUE INDEX UX_Orders_Customer_ClientRequestId ON Orders(CustomerId, ClientRequestId) WHERE ClientRequestId IS NOT NULL;
CREATE INDEX IX_Orders_Takeaway ON Orders(OrderTime, TakeawayNumber) WHERE TakeawayNumber IS NOT NULL;

CREATE INDEX IX_OrderItems_OrderId_Status  ON OrderItems(OrderId, Status);
CREATE INDEX IX_OrderItems_MenuItemId      ON OrderItems(MenuItemId) WHERE MenuItemId IS NOT NULL;
CREATE INDEX IX_OrderItems_ComboId         ON OrderItems(ComboId) WHERE ComboId IS NOT NULL;

-- Index Đánh giá món
CREATE UNIQUE INDEX UX_OrderReviews_OrderItemId ON OrderReviews(OrderItemId) WHERE OrderItemId IS NOT NULL;
CREATE UNIQUE INDEX UX_OrderReviews_OrderId_Experience ON OrderReviews(OrderId) WHERE OrderItemId IS NULL;

-- Index Bếp & Kho
CREATE INDEX IX_KitchenTickets_Status      ON KitchenTickets(Status);
CREATE INDEX IX_KitchenTickets_StationId   ON KitchenTickets(StationId);
CREATE INDEX IX_MenuItemIngredients_IngredientId ON MenuItemIngredients(IngredientId);
CREATE INDEX IX_StockTransactions_IngredientId   ON StockTransactions(IngredientId);
CREATE INDEX IX_StockTransactions_RelatedOrderItemId ON StockTransactions(RelatedOrderItemId);
CREATE INDEX IX_ComboItems_ComboId         ON ComboItems(ComboId);
CREATE INDEX IX_MenuItems_StationId        ON MenuItems(StationId);
CREATE INDEX IX_OrderTables_TableId        ON OrderTables(TableId);
CREATE INDEX IX_InvoiceItems_OrderItemId   ON InvoiceItems(OrderItemId);
CREATE INDEX IX_InvoiceItems_InvoiceId     ON InvoiceItems(InvoiceId);
CREATE INDEX IX_MenuItemPriceHistory_MenuItemId ON MenuItemPriceHistory(MenuItemId, ChangedAt);

-- Index Hóa đơn & Phản hồi
CREATE INDEX IX_Invoices_Status            ON Invoices(Status);
CREATE INDEX IX_Invoices_CreatedBy_InvoiceDate ON Invoices(CreatedBy, InvoiceDate);
CREATE INDEX IX_Feedbacks_InvoiceId        ON Feedbacks(InvoiceId);
CREATE INDEX IX_Feedbacks_Rating           ON Feedbacks(Rating);

-- Index Nhân sự & Phân công khu vực
CREATE INDEX IX_ShiftAssignments_WorkDate  ON ShiftAssignments(WorkDate);
CREATE INDEX IX_ShiftAssignmentAreas_Area  ON ShiftAssignmentAreas(AreaId);
CREATE INDEX IX_EmployeeAreaAssignments_EmployeeDate ON EmployeeAreaAssignments(EmployeeId, WorkDate, IsActive);
CREATE INDEX IX_EmployeeAreaAssignments_Area ON EmployeeAreaAssignments(AreaId, IsActive);

-- Index Thông báo & Audit
CREATE INDEX IX_Notifications_TargetUser_IsRead ON Notifications(TargetUserId, IsRead);
CREATE INDEX IX_Notifications_CustomerId_IsRead_CreatedAt ON Notifications(CustomerId, IsRead, CreatedAt DESC) WHERE CustomerId IS NOT NULL;
CREATE UNIQUE INDEX UX_Notifications_EventKey ON Notifications(EventKey) WHERE EventKey IS NOT NULL;
CREATE INDEX IX_AuditLogs_TableName_RecordId ON AuditLogs(TableName, RecordId);
CREATE INDEX IX_MobileOperationLogs_CreatedAt ON MobileOperationLogs(CreatedAt DESC);
CREATE INDEX IX_MobileOperationLogs_EmployeeCode ON MobileOperationLogs(EmployeeCode, CreatedAt DESC);

-- Index Ưu tiên chế biến Mobile
CREATE INDEX IX_MobileKitchenPriorities_Table ON MobileKitchenPriorities(TableId, IsActive);
CREATE INDEX IX_MobileKitchenPriorities_Item  ON MobileKitchenPriorities(OrderItemId, IsActive);
GO

/* ======================================================================
   DỮ LIỆU KHỞI TẠO MẶC ĐỊNH (DEFAULT SEED DATA)
   ====================================================================== */

-- ======================================================================
-- BƯỚC 1: NHÓM DANH MỤC GỐC (Roles, Stations, Areas, Categories, Shifts)
-- ======================================================================

INSERT INTO Roles (RoleName, Description) VALUES
(N'QuanLy', N'Quản lý / Chủ quán (kiêm quyền Admin hệ thống)'),
(N'PhucVu', N'Nhân viên phục vụ / order'),
(N'Bep', N'Nhân viên bếp'),
(N'ThuNgan', N'Thu ngân');

INSERT INTO Stations (StationName) VALUES 
(N'Bep'), 
(N'QuayNuoc');

INSERT INTO Areas (AreaName, Description) VALUES
(N'Tầng 1 - Khu A', N'Khu vực A tầng 1'),
(N'Tầng 1 - Khu B', N'Khu vực B tầng 1'),
(N'Tầng 1 - Khu C', N'Khu vực C tầng 1'),
(N'Tầng 1 - Khu D', N'Khu vực D tầng 1'),
(N'Tầng 2 - Khu A', N'Khu vực A tầng 2'),
(N'Tầng 2 - Khu B', N'Khu vực B tầng 2'),
(N'Tầng 2 - Khu C', N'Khu vực C tầng 2'),
(N'Tầng 2 - Khu D', N'Khu vực D tầng 2'),
(N'Tầng 3 - Phòng VIP', N'Khu vực phòng VIP riêng tư');

INSERT INTO Categories (CategoryName, CategoryType) VALUES
(N'Khai vị', N'MonAn'),
(N'Món chính', N'MonAn'),
(N'Đồ uống', N'ThucUong');

INSERT INTO Shifts (ShiftName, StartTime, EndTime) VALUES
(N'Ca Sáng', '07:00:00', '15:00:00'),
(N'Ca Chiều', '15:00:00', '23:00:00');
GO

-- ======================================================================
-- BƯỚC 2: NHÓM NGƯỜI DÙNG & NHÂN SỰ (Users, UserRoles, Employees)
-- ======================================================================

INSERT INTO Users (Username, PasswordHash, FullName, Phone, Email, IsActive) VALUES
(N'admin', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Nguyễn Quản Lý', N'0901111222', N'admin@quanan.com', 1),
(N'thungan01', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Trần Thị Thu Ngân', N'0902222333', N'thungan@quanan.com', 1),
(N'phucvu01', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Lê Văn Phục Vụ', N'0903333444', N'phucvu@quanan.com', 1),
(N'bep01', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Phạm Bếp Trưởng', N'0904444555', N'bep@quanan.com', 1),
(N'nhanvien_hybrid', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Hoàng Kiêm Nhiệm', N'0905555666', N'hybrid@quanan.com', 1),
(N'web-system', N'!DISABLED!', N'Đơn đặt từ Web', NULL, NULL, 0);

INSERT INTO UserRoles (UserId, RoleId, AssignedBy) VALUES
(1, (SELECT RoleId FROM Roles WHERE RoleName = N'QuanLy'), 1),
(2, (SELECT RoleId FROM Roles WHERE RoleName = N'ThuNgan'), 1),
(3, (SELECT RoleId FROM Roles WHERE RoleName = N'PhucVu'), 1),
(4, (SELECT RoleId FROM Roles WHERE RoleName = N'Bep'), 1),
(5, (SELECT RoleId FROM Roles WHERE RoleName = N'PhucVu'), 1),
(5, (SELECT RoleId FROM Roles WHERE RoleName = N'ThuNgan'), 1);

INSERT INTO Employees (UserId, EmployeeCode, Position, HireDate, Salary, Status, StationId) VALUES
(1, N'NV001', N'QuanLy', '2024-01-15', 15000000, N'DangLamViec', NULL),
(2, N'NV002', N'ThuNgan', '2024-03-01', 8000000, N'DangLamViec', NULL),
(3, N'NV003', N'PhucVu', '2024-05-10', 6500000, N'DangLamViec', NULL),
(4, N'NV004', N'Bep', '2024-02-20', 9500000, N'DangLamViec', (SELECT StationId FROM Stations WHERE StationName = N'Bep')),
(5, N'NV005', N'PhucVu', '2024-06-01', 7000000, N'DangLamViec', NULL),
((SELECT UserId FROM Users WHERE Username = N'web-system'), N'WEB', N'PhucVu', CAST(GETDATE() AS DATE), 0, N'NghiViec', NULL);
GO

-- ======================================================================
-- BƯỚC 3: NHÓM BÀN & PHÒNG VIP (DiningTables, VIPRoomDetails)
-- ======================================================================

-- Bàn Tầng 1 (AreaId từ 1 đến 4)
INSERT INTO DiningTables (TableName, AreaId, Capacity, TableType, Status) VALUES
(N'T1-A1', 1, 4, N'Thuong', N'Trong'), (N'T1-A2', 1, 4, N'Thuong', N'Trong'), (N'T1-A3', 1, 4, N'Thuong', N'Trong'), (N'T1-A4', 1, 4, N'Thuong', N'Trong'),
(N'T1-B1', 2, 4, N'Thuong', N'Trong'), (N'T1-B2', 2, 4, N'Thuong', N'Trong'), (N'T1-B3', 2, 4, N'Thuong', N'Trong'), (N'T1-B4', 2, 4, N'Thuong', N'Trong'),
(N'T1-C1', 3, 4, N'Thuong', N'Trong'), (N'T1-C2', 3, 4, N'Thuong', N'Trong'), (N'T1-C3', 3, 4, N'Thuong', N'Trong'), (N'T1-C4', 3, 4, N'Thuong', N'Trong'),
(N'T1-D1', 4, 4, N'Thuong', N'Trong'), (N'T1-D2', 4, 4, N'Thuong', N'Trong'), (N'T1-D3', 4, 4, N'Thuong', N'Trong'), (N'T1-D4', 4, 4, N'Thuong', N'Trong');

-- Bàn Tầng 2 (AreaId từ 5 đến 8)
INSERT INTO DiningTables (TableName, AreaId, Capacity, TableType, Status) VALUES
(N'T2-A1', 5, 4, N'Thuong', N'Trong'), (N'T2-A2', 5, 4, N'Thuong', N'Trong'), (N'T2-A3', 5, 4, N'Thuong', N'Trong'), (N'T2-A4', 5, 4, N'Thuong', N'Trong'),
(N'T2-B1', 6, 4, N'Thuong', N'Trong'), (N'T2-B2', 6, 4, N'Thuong', N'Trong'), (N'T2-B3', 6, 4, N'Thuong', N'Trong'), (N'T2-B4', 6, 4, N'Thuong', N'Trong'),
(N'T2-C1', 7, 4, N'Thuong', N'Trong'), (N'T2-C2', 7, 4, N'Thuong', N'Trong'), (N'T2-C3', 7, 4, N'Thuong', N'Trong'), (N'T2-C4', 7, 4, N'Thuong', N'Trong'),
(N'T2-D1', 8, 4, N'Thuong', N'Trong'), (N'T2-D2', 8, 4, N'Thuong', N'Trong'), (N'T2-D3', 8, 4, N'Thuong', N'Trong'), (N'T2-D4', 8, 4, N'Thuong', N'Trong');

-- Bàn Tầng 3 (Khu VIP - AreaId = 9)
INSERT INTO DiningTables (TableName, AreaId, Capacity, TableType, Status) VALUES
(N'VIP 01', 9, 8, N'VIP', N'Trong'),
(N'VIP 02', 9, 10, N'VIP', N'Trong'),
(N'VIP 03', 9, 15, N'VIP', N'Trong');

INSERT INTO VIPRoomDetails (TableId, RoomFee, MinimumBillAmount, DepositAmount, Amenities, Description) 
SELECT TableId, 
       CASE WHEN TableName = N'VIP 03' THEN 500000 ELSE 200000 END, 
       CASE WHEN TableName = N'VIP 03' THEN 4000000 ELSE 2000000 END, 
       CASE WHEN TableName = N'VIP 03' THEN 1000000 ELSE 500000 END,   
       N'TV Smart, Máy lạnh riêng, Dàn Karaoke cao cấp', 
       N'Phòng riêng tư phục vụ tiệc và hội họp'
FROM DiningTables 
WHERE TableType = N'VIP';
GO

-- ======================================================================
-- BƯỚC 4: NHÓM THỰC ĐƠN & KHO NGUYÊN LIỆU (MenuItems, Ingredients)
-- ======================================================================

INSERT INTO MenuItems (CategoryId, StationId, ItemName, Description, Price, Unit, IsFreeRefill, FreeRefillLimit, RefillPrice) VALUES
(1, 1, N'Gỏi ngó sen tôm thịt', N'Món khai vị thanh mát', 65000, N'Đĩa', 0, 0, NULL),
(2, 1, N'Phở bò đặc biệt', N'Phở truyền thống nước dùng hầm xương', 65000, N'Tô', 0, 0, NULL),
(2, 1, N'Cơm chiên dương châu', N'Cơm chiên hạt vàng đều', 75000, N'Đĩa', 0, 0, NULL),
(3, 2, N'Trà đá thanh nhiệt', N'Trà đá mát lạnh giải khát', 5000, N'Ly', 1, 3, 3000);

INSERT INTO Ingredients (IngredientName, Unit, CurrentStock, MinStockAlert) VALUES
(N'Thịt bò', N'gram', 10000, 1000),
(N'Bánh phở', N'gram', 15000, 2000),
(N'Gạo tẻ', N'gram', 20000, 3000),
(N'Tôm tươi', N'gram', 5000, 800),
(N'Trà búp', N'gram', 2000, 300);

INSERT INTO MenuItemIngredients (MenuItemId, IngredientId, QuantityRequired) VALUES
(2, 1, 150),
(2, 2, 200),
(3, 3, 250),
(3, 4, 100),
(4, 5, 10);
GO

-- ======================================================================
-- BƯỚC 5: NHÓM KHÁCH HÀNG & KHUYẾN MÃI (Customers, DiscountCampaigns)
-- ======================================================================

INSERT INTO Customers (FullName, Phone, Email, DateOfBirth, LoyaltyPoint, MemberLevel) VALUES
(N'Nguyễn Văn A', N'0988123456', N'vana@gmail.com', '1995-05-12', 150, N'Bac'),
(N'Trần Thị B', N'0977654321', N'thib@gmail.com', '1998-10-20', 450, N'Vang'),
(N'Lê Hoàng C', N'0912345678', N'hoangc@gmail.com', '1990-01-05', 1200, N'KimCuong');

INSERT INTO DiscountCampaigns (CampaignName, StartDate, EndDate, DiscountPercent, RequireMembership, Note, CreatedBy) VALUES
(N'Khai trương hồng phát', '2026-01-01', '2026-12-31', 5.00, 0, N'Giảm 5% cho mọi hóa đơn trong năm', 1);

INSERT INTO DiscountCampaigns 
    (CampaignName, StartDate, EndDate, DiscountPercent, RequireMembership, MinVisitCount, VisitLookbackDays, Note, CreatedBy) 
VALUES
    (N'Tri ân khách hàng thân thiết', '2026-01-01', '2026-12-31', 12.00, 1, 3, 30, N'Dành cho khách quay lại từ 3 lần trong 30 ngày', 1);
GO

-- ======================================================================
-- BƯỚC 6: NHÓM PHÂN CÔNG CA LÀM & NHIỆM VỤ (ShiftAssignments, Tasks)
-- ======================================================================

INSERT INTO ShiftAssignments (EmployeeId, ShiftId, WorkDate, Status) VALUES
((SELECT EmployeeId FROM Employees WHERE UserId = 2), 1, CAST(GETDATE() AS DATE), N'DaXepLich'),
((SELECT EmployeeId FROM Employees WHERE UserId = 3), 1, CAST(GETDATE() AS DATE), N'DaXepLich'),
((SELECT EmployeeId FROM Employees WHERE UserId = 4), 2, CAST(GETDATE() AS DATE), N'DaXepLich'),
((SELECT EmployeeId FROM Employees WHERE UserId = 5), 2, CAST(GETDATE() AS DATE), N'DaXepLich');

INSERT INTO Tasks (EmployeeId, TaskName, Description, DueDate, Status) VALUES
((SELECT EmployeeId FROM Employees WHERE UserId = 3), N'Vệ sinh khu vực bàn tầng trệt', N'Lau dọn sạch sẽ trước giờ mở cửa', DATEADD(HOUR, 2, GETDATE()), N'ChuaLam'),
((SELECT EmployeeId FROM Employees WHERE UserId = 4), N'Kiểm tra kho thịt bò', N'Báo cáo số lượng tồn thực tế cho quản lý', DATEADD(HOUR, 4, GETDATE()), N'ChuaLam');
GO

-- ======================================================================
-- BƯỚC 7: CẤU HÌNH ĐIỂM DANH & QUYỀN MOBILE (AttendanceSettings, Permissions)
-- ======================================================================

IF NOT EXISTS (SELECT 1 FROM AttendanceSettings WHERE SettingId = 1)
BEGIN
    INSERT INTO AttendanceSettings (SettingId, RestaurantLatitude, RestaurantLongitude, RadiusMeters, MaxAccuracyMeters, UpdatedAt, UpdatedBy)
    VALUES (1, NULL, NULL, 100, 50, NULL, NULL);
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
    (N'statistics.view',  N'Xem thống kê',           N'Được xem doanh thu, món bán chạy và doanh thu theo nhân viên'),
    (N'staff.manage',     N'Quản lý nhân viên',      N'Được phân ca, giao nhiệm vụ và xem lịch sử làm việc của nhân viên')
) AS source(PermissionKey, DisplayName, Description)
ON target.PermissionKey = source.PermissionKey
WHEN MATCHED THEN UPDATE SET
    DisplayName = source.DisplayName,
    Description = source.Description,
    IsActive = 1
WHEN NOT MATCHED THEN INSERT(PermissionKey, DisplayName, Description, IsActive)
    VALUES(source.PermissionKey, source.DisplayName, source.Description, 1);

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

INSERT INTO RoleMobilePermissions(RoleId, PermissionKey, IsAllowed)
SELECT r.RoleId, d.PermissionKey, 1
FROM @Defaults d
INNER JOIN Roles r ON r.RoleName = d.RoleName
WHERE NOT EXISTS (
    SELECT 1 FROM RoleMobilePermissions rp
    WHERE rp.RoleId = r.RoleId AND rp.PermissionKey = d.PermissionKey
);
GO
