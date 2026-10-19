CREATE DATABASE QuanAnManagementDB;
GO
USE QuanAnManagementDB;
GO

/* ======================================================================
   NHOM 1: NGUOI DUNG & PHAN QUYEN (Role, User, UserRoles, Employee)
   ====================================================================== */

CREATE TABLE Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleName        NVARCHAR(50)  NOT NULL UNIQUE, -- QuanLy (kiem Admin), ThuNgan, PhucVu, Bep
    Description     NVARCHAR(255) NULL
    -- Quan ly va Admin gop chung thanh 1 vai tro "QuanLy": toan quyen he
    -- thong (quan tri user, cau hinh) + quyen nghiep vu (bao cao, giam sat).
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
    -- KHONG co cot RoleId o day: 1 User co the co NHIEU role cung luc
    -- (vd: cong tac vien vua lam PhucVu vua lam ThuNgan) -> xem UserRoles.
);

CREATE TABLE UserRoles (                             -- quan he N-N User <-> Role
    UserRoleId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NOT NULL,
    RoleId          INT NOT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,          -- tam vo hieu 1 role ma khong can xoa (giu lich su)
    AssignedBy      INT NULL,                        -- UserId cua Quan ly da gan role nay
    AssignedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    RevokedAt       DATETIME2 NULL,
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId),
    CONSTRAINT UQ_UserRoles_UserId_RoleId UNIQUE (UserId, RoleId)
);

CREATE TABLE Employees (
    EmployeeId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NOT NULL UNIQUE,             -- 1-1 voi Users
    EmployeeCode    NVARCHAR(20) NOT NULL UNIQUE,
    Position        NVARCHAR(50) NOT NULL,           -- Bep, PhucVu, ThuNgan, QuanLy
    HireDate        DATE NOT NULL,
    Salary          DECIMAL(18,2) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DangLamViec', -- DangLamViec, NghiViec, TamNghi
    CONSTRAINT FK_Employees_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

/* ======================================================================
   NHOM 2: BAN & KHU VUC (Area, Table, Reservation, WaitingQueue)
   ====================================================================== */

CREATE TABLE Areas (
    AreaId          INT IDENTITY(1,1) PRIMARY KEY,
    AreaName        NVARCHAR(50) NOT NULL,           -- Tang 1, San vuon, Phong VIP...
    Description     NVARCHAR(255) NULL
);

CREATE TABLE DiningTables (
    TableId         INT IDENTITY(1,1) PRIMARY KEY,
    TableName       NVARCHAR(20) NOT NULL,           -- Ban A1, Ban B2...
    AreaId          INT NOT NULL,
    Capacity        INT NOT NULL DEFAULT 4,
    TableType       NVARCHAR(20) NOT NULL DEFAULT N'Thuong', -- Thuong, VIP (phong rieng tu)
    Status          NVARCHAR(20) NOT NULL DEFAULT N'Trong', -- Trong, DangPhucVu, DaDat, DangDonDep
    CONSTRAINT FK_Tables_Areas FOREIGN KEY (AreaId) REFERENCES Areas(AreaId)
);

-- Thong tin rieng cho phong VIP: 1-1 voi DiningTables, CHI ton tai
-- khi TableType = 'VIP' (kiem tra bang trigger ben duoi, vi CHECK
-- constraint cua SQL Server khong the tham chieu bang khac).
CREATE TABLE VIPRoomDetails (
    TableId             INT PRIMARY KEY,
    RoomFee             DECIMAL(18,2) NOT NULL DEFAULT 0,  -- phi thue phong / luot (hoac / gio, tuy quan)
    MinimumBillAmount   DECIMAL(18,2) NOT NULL DEFAULT 0,  -- hoa don toi thieu phai dat; thieu se bi thu them phan chenh lech
    DepositAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,  -- tien coc bat buoc khi dat truoc phong nay
    Amenities           NVARCHAR(500) NULL,                 -- vd: TV, dan karaoke, may lanh rieng, view song...
    Description         NVARCHAR(500) NULL,
    CONSTRAINT FK_VIPRoomDetails_DiningTables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId)
);
GO

-- Trigger dam bao chi ban co TableType = 'VIP' moi duoc phep co dong
-- trong VIPRoomDetails (khong the dung CHECK constraint vi phai tham
-- chieu sang bang DiningTables).
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
        RAISERROR (N'Chi ban/phong co TableType = ''VIP'' moi duoc khai bao VIPRoomDetails.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

CREATE INDEX IX_DiningTables_TableType ON DiningTables(TableType);
GO

-- Trigger CHIEU NGUOC LAI cua trigger tren: chan viec doi TableType cua
-- 1 ban TU 'VIP' SANG loai khac khi ban do van con dong du lieu trong
-- VIPRoomDetails (tranh de lai du lieu "mo coi" - VIPRoomDetails van ton
-- tai nhung ban lien quan da khong con la VIP nua). Muon doi loai ban
-- thi phai XOA VIPRoomDetails cua ban do TRUOC.
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
        RAISERROR (N'Khong the doi TableType khoi ''VIP'' vi ban/phong nay van con du lieu trong VIPRoomDetails. Hay xoa VIPRoomDetails cua ban nay truoc.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

CREATE TABLE Reservations (
    ReservationId   INT IDENTITY(1,1) PRIMARY KEY,
    TableId         INT NOT NULL,
    CustomerId      INT NULL,                        -- co the null neu khach vang lai
    CustomerName    NVARCHAR(100) NULL,
    CustomerPhone   NVARCHAR(20) NULL,
    ReservationTime DATETIME2 NOT NULL,
    NumberOfGuests  INT NOT NULL DEFAULT 1,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DaXacNhan', -- DaXacNhan, DaNhanBan, DaHuy, KhongDen
    DepositAmount   DECIMAL(18,2) NOT NULL DEFAULT 0, -- tien coc yeu cau (thuong > 0 neu dat phong VIP)
    DepositPaid     BIT NOT NULL DEFAULT 0,
    DepositPaidAt   DATETIME2 NULL,
    Note            NVARCHAR(255) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Reservations_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId)
);

-- Khach vang lai xep hang cho ban (KHONG dat truoc, khac Reservations)
CREATE TABLE WaitingQueue (
    QueueId             INT IDENTITY(1,1) PRIMARY KEY,
    QueueDate           DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE), -- so thu tu reset moi ngay
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

-- Trigger tu sinh so thu tu tang dan theo TUNG NGAY.
-- TOI UU: dung WITH (UPDLOCK, HOLDLOCK) de khoa doc khi tinh MAX(QueueNumber),
-- tranh 2 khach bam lay so CUNG LUC bi cap trung 1 so (race condition giua
-- CAC TRANSACTION khac nhau).
-- FIX THEM: dung ROW_NUMBER() de danh so tang dan NGAY BEN TRONG cung 1
-- batch insert (truong hop 1 cau lenh INSERT chen NHIEU dong cung luc,
-- vd insert hang loat) - neu khong co ROW_NUMBER, ca hai dong trong CUNG
-- 1 lenh se cung nhin thay 1 gia tri MAX(QueueNumber) cu va bi cap TRUNG
-- 1 so, vi pham UQ_WaitingQueue_DateNumber.
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
        mp.BaseNumber + ni.RowSeq,          -- moi dong trong batch cong don tiep theo, khong bao gio trung nhau
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

-- Vi du dung: chi can INSERT binh thuong, KHONG can tu dien QueueNumber
-- INSERT INTO WaitingQueue (CustomerName, CustomerPhone, NumberOfGuests) VALUES (N'Anh Tu', N'0909xxxxxx', 4);

CREATE INDEX IX_WaitingQueue_Date_Status ON WaitingQueue(QueueDate, Status);
GO

/* ======================================================================
   NHOM 3: THUC DON (Station, Category, MenuItem, Combo)
   ====================================================================== */

CREATE TABLE Stations (                              -- Bo phan che bien: Bep, Quay nuoc...
    StationId   INT IDENTITY(1,1) PRIMARY KEY,
    StationName NVARCHAR(50) NOT NULL UNIQUE          -- "Bep", "QuayNuoc"
);

CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName    NVARCHAR(50) NOT NULL,            -- Mon khai vi, Mon chinh, Do uong...
    CategoryType    NVARCHAR(20) NOT NULL DEFAULT N'MonAn' -- MonAn, ThucUong
);

CREATE TABLE MenuItems (
    MenuItemId      INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId      INT NOT NULL,
    StationId       INT NULL,                         -- mon nay do bo phan nao che bien (Bep/Quay nuoc)
    ItemName        NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    Price           DECIMAL(18,2) NOT NULL,
    Unit            NVARCHAR(20) NULL DEFAULT N'Phan',
    ImageUrl        NVARCHAR(500) NULL,
    IsAvailable     BIT NOT NULL DEFAULT 1,           -- con ban / het mon (Module Nguyen lieu tu dong cap nhat)
    IsFreeRefill    BIT NOT NULL DEFAULT 0,           -- co ap dung chinh sach goi them (refill) khong
    FreeRefillLimit INT NOT NULL DEFAULT 2,           -- so lan goi them MIEN PHI
    RefillPrice     DECIMAL(18,2) NULL,               -- gia tinh tu lan vuot FreeRefillLimit
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

CREATE TABLE ComboItems (                            -- chi tiet mon trong combo
    ComboItemId     INT IDENTITY(1,1) PRIMARY KEY,
    ComboId         INT NOT NULL,
    MenuItemId      INT NOT NULL,
    Quantity        INT NOT NULL DEFAULT 1,
    CONSTRAINT FK_ComboItems_Combos FOREIGN KEY (ComboId) REFERENCES Combos(ComboId),
    CONSTRAINT FK_ComboItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId)
);

-- Nay moi gan StationId cho Employees (dat sau vi Stations moi vua tao)
ALTER TABLE Employees ADD StationId INT NULL
    CONSTRAINT FK_Employees_Stations FOREIGN KEY REFERENCES Stations(StationId);
GO

/* ======================================================================
   NHOM 3B: KHO NGUYEN LIEU & CONG THUC MON (Ingredient, Recipe, Stock)
   Muc dich: nhan vien phuc vu thay ngay mon nao con/het khi goi mon,
             khong can doi bep bao lai (yeu cau tu GVHD).
   ====================================================================== */

CREATE TABLE Ingredients (
    IngredientId    INT IDENTITY(1,1) PRIMARY KEY,
    IngredientName  NVARCHAR(100) NOT NULL,
    Unit            NVARCHAR(20)  NOT NULL,           -- "gram", "ml", "cai"...
    CurrentStock    DECIMAL(18,2) NOT NULL DEFAULT 0,
    MinStockAlert   DECIMAL(18,2) NOT NULL DEFAULT 0, -- nguong canh bao sap het
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Cong thuc mon an (BOM). 1 mon can NHIEU nguyen lieu, 1 nguyen lieu dung cho NHIEU mon
CREATE TABLE MenuItemIngredients (
    MenuItemIngredientId INT IDENTITY(1,1) PRIMARY KEY,
    MenuItemId      INT NOT NULL,
    IngredientId    INT NOT NULL,
    QuantityRequired DECIMAL(18,2) NOT NULL,          -- can bao nhieu Unit cho 1 PHAN mon nay
    CONSTRAINT FK_MII_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT FK_MII_Ingredients FOREIGN KEY (IngredientId) REFERENCES Ingredients(IngredientId),
    CONSTRAINT UQ_MenuItem_Ingredient UNIQUE (MenuItemId, IngredientId)
);

-- So nhat ky nhap/xuat kho - nguon du lieu goc, KHONG sua truc tiep CurrentStock
CREATE TABLE StockTransactions (
    StockTransactionId INT IDENTITY(1,1) PRIMARY KEY,
    IngredientId    INT NOT NULL,
    ChangeType      NVARCHAR(20) NOT NULL,            -- NhapKho, XuatBanMon, DieuChinh, HuyMon
    QuantityChange  DECIMAL(18,2) NOT NULL,            -- duong = nhap them, am = xuat di
    RelatedOrderItemId INT NULL,
    Note            NVARCHAR(255) NULL,
    CreatedBy       INT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_StockTx_Ingredients FOREIGN KEY (IngredientId) REFERENCES Ingredients(IngredientId),
    CONSTRAINT FK_StockTx_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
    -- Luu y: FK toi OrderItems duoc them BEN DUOI, sau khi bang OrderItems ton tai.
);
GO

/* ======================================================================
   NHOM 4: ORDER & BEP (Order, OrderItem, KitchenTicket, DishStatus)
   ====================================================================== */

CREATE TABLE Orders (
    OrderId         INT IDENTITY(1,1) PRIMARY KEY,
    TableId         INT NOT NULL,
    EmployeeId      INT NOT NULL,
    CustomerId      INT NULL,
    OrderTime       DATETIME2 NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DangMo', -- DangMo, DaThanhToan, DaHuy
    Note            NVARCHAR(255) NULL,
    TotalAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,  -- duoc trigger trg_OrderItems_SyncOrderTotal tu dong cap nhat
    RowVer          ROWVERSION NOT NULL,               -- kiem soat concurrency cho Order (vd 2 thu ngan cung mo thanh toan 1 ban)
    CONSTRAINT FK_Orders_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT FK_Orders_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId)
);

CREATE TABLE OrderItems (
    OrderItemId     INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,
    MenuItemId      INT NULL,                         -- 1 trong 2: MenuItemId hoac ComboId
    ComboId         INT NULL,
    Quantity        INT NOT NULL DEFAULT 1,
    UnitPrice       DECIMAL(18,2) NOT NULL,            -- gia tai thoi diem order
    Note            NVARCHAR(255) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'MoiGoi', -- MoiGoi, DangCheBien, DaCheBien, DaPhucVu, DaHuy
    IsRefillRequest      BIT NOT NULL DEFAULT 0,       -- true = day la lan "goi them" cua mon da goi truoc do
    RefillSequenceNumber INT NULL,                     -- lan goi them thu may (trigger tu tinh)
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    RowVer          ROWVERSION NOT NULL,               -- kiem soat concurrency: dam bao 2 nguoi (vd Bep vs
                                                         -- PhucVu) khong ghi de trang thai cua nhau tren CUNG
                                                         -- 1 mon; app phai gui kem RowVer da doc truoc do vao
                                                         -- menh de WHERE khi UPDATE Status
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(MenuItemId),
    CONSTRAINT FK_OrderItems_Combos FOREIGN KEY (ComboId) REFERENCES Combos(ComboId),
    CONSTRAINT CK_OrderItems_ItemType CHECK (
        (MenuItemId IS NOT NULL AND ComboId IS NULL) OR
        (MenuItemId IS NULL AND ComboId IS NOT NULL)
    )
);

-- Bo sung FK con thieu tu StockTransactions den OrderItems (dat o day vi
-- OrderItems moi vua duoc tao, khong the tao truoc do)
ALTER TABLE StockTransactions
    ADD CONSTRAINT FK_StockTx_OrderItems FOREIGN KEY (RelatedOrderItemId) REFERENCES OrderItems(OrderItemId);
GO

CREATE TABLE KitchenTickets (
    KitchenTicketId INT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId     INT NOT NULL,
    StationId       INT NULL,                          -- phieu gui toi bo phan nao (Bep/Quay nuoc)
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
GO

/* ----------------------------------------------------------------------
   TRIGGER 1 (MOI - TOI UU): tu dong dong bo Orders.TotalAmount moi khi
   OrderItems thay doi (them/sua/xoa). Ban goc KHONG co trigger nay nen
   TotalAmount de bi lech neu app quen tinh lai.
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
              AND oi.Status <> N'DaHuy'          -- mon da huy khong tinh vao tong tien
        ), 0)
    FROM Orders o
    INNER JOIN AffectedOrders ao ON ao.OrderId = o.OrderId;
END;
GO

/* ----------------------------------------------------------------------
   TRIGGER 2 (SUA - TOI UU): tru kho nguyen lieu khi khach goi mon,
   XU LY CA COMBO (ban goc chi tru kho khi goi mon le, bo sot combo).
   Logic combo: 1 combo -> nhieu MenuItem (qua ComboItems) -> nhieu
   nguyen lieu (qua MenuItemIngredients), nhan don voi so luong combo.
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_OrderItems_DeductStock
ON OrderItems
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Truong hop 1: goi mon le truc tiep
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'XuatBanMon',
        -1 * bom.QuantityRequired * ins.Quantity,
        ins.OrderItemId,
        N'Tu dong tru khi order mon le'
    FROM inserted ins
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ins.MenuItemId
    WHERE ins.MenuItemId IS NOT NULL;

    -- Truong hop 2 (TOI UU - bo sung): goi COMBO -> tru kho theo tung
    -- mon con trong combo do, nhan them so luong mon trong ComboItems
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'XuatBanMon',
        -1 * bom.QuantityRequired * ci.Quantity * ins.Quantity,
        ins.OrderItemId,
        N'Tu dong tru khi order combo'
    FROM inserted ins
    INNER JOIN ComboItems ci ON ci.ComboId = ins.ComboId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ci.MenuItemId
    WHERE ins.ComboId IS NOT NULL;
END;
GO

/* ----------------------------------------------------------------------
   TRIGGER 3: ap dung chinh sach refill (2 lan dau mien phi, tu lan 3
   tinh phi) cho cac mon co IsFreeRefill = 1.
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

-- QUAN TRONG: co 3 trigger AFTER INSERT tren cung bang OrderItems
-- (trg_OrderItems_SyncOrderTotal, trg_OrderItems_DeductStock,
-- trg_OrderItems_ApplyRefillPolicy). SQL Server KHONG dam bao thu tu
-- chay giua nhieu AFTER trigger cung loai neu khong khai bao ro rang.
-- Phai ep trg_OrderItems_ApplyRefillPolicy (sua UnitPrice cho refill)
-- chay TRUOC TIEN, va trg_OrderItems_SyncOrderTotal (tinh lai TotalAmount
-- dua tren UnitPrice) chay SAU CUNG, neu khong TotalAmount co the bi
-- tinh sai gia refill.
EXEC sp_settriggerorder @triggername = N'dbo.trg_OrderItems_ApplyRefillPolicy', @order = 'First', @stmttype = 'INSERT';
EXEC sp_settriggerorder @triggername = N'dbo.trg_OrderItems_SyncOrderTotal', @order = 'Last', @stmttype = 'INSERT';
GO

/* ----------------------------------------------------------------------
   TRIGGER 4: khi ton kho thay doi (StockTransactions) -> cap nhat lai
   CurrentStock va tu dong tinh lai IsAvailable cho cac mon lien quan.
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

-- View tra nhanh "con du lam bao nhieu phan". TOI UU: lam ro NULL nghia
-- la "mon khong duoc quan ly theo cong thuc nguyen lieu" (luon coi la
-- con hang, phu thuoc co IsAvailable thu cong), KHAC voi 0 nghia la
-- "het nguyen lieu that su".
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
    ) AS EstimatedServingsLeft   -- NULL = khong quan ly theo cong thuc; 0 = het hang that
FROM MenuItems mi;
GO

/* ======================================================================
   NHOM 5: THANH TOAN (Invoice, Payment, chiet khau thanh vien)
   ====================================================================== */

CREATE TABLE Invoices (
    InvoiceId       INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,                    -- KHONG con UNIQUE: 1 Order co the tach thanh NHIEU Invoice (chia bill)
    InvoiceDate     DATETIME2 NOT NULL DEFAULT GETDATE(),
    SubTotal        DECIMAL(18,2) NOT NULL,
    DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    AppliedCampaignId INT NULL,                        -- chien dich giam gia (neu co) da ap dung cho hoa don nay, de sau nay truy vet/thong ke
    VoucherId       INT NULL,
    TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    VIPRoomFee      DECIMAL(18,2) NOT NULL DEFAULT 0, -- phi thue phong VIP (neu ban thuoc TableType='VIP')
    VIPMinimumGap   DECIMAL(18,2) NOT NULL DEFAULT 0, -- phan chenh lech thu them neu SubTotal chua dat muc toi thieu
    TotalAmount     DECIMAL(18,2) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ChuaThanhToan', -- ChuaThanhToan, DaThanhToan, DaHuy
    CreatedBy       INT NULL,
    RowVer          ROWVERSION NOT NULL,               -- kiem soat concurrency khi thanh toan (tranh 2 nguoi cung xu ly 1 hoa don)
    CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_Invoices_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,
    PaymentMethod   NVARCHAR(20) NOT NULL,           -- TienMat, TheNganHang, ViDienTu, ChuyenKhoan
    Amount          DECIMAL(18,2) NOT NULL,
    PaymentTime     DATETIME2 NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20) NOT NULL DEFAULT N'ThanhCong', -- ThanhCong, ThatBai, HoanTien
    TransactionRef  NVARCHAR(100) NULL,
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId)
);

-- Chien dich giam gia theo KHOANG NGAY (thay the co che bac hoa don co
-- dinh 3%/5% truoc day). Quan ly tu cau hinh tren giao dien: chon ngay
-- bat dau, ngay ket thuc, va % giam. Co 2 loai chien dich:
--   1) Ap dung cho TAT CA (RequireMembership = 0): moi hoa don, khong
--      phan biet khach vang lai hay thanh vien.
--   2) Chi ap dung cho "khach quen" (RequireMembership = 1): phai LA
--      thanh vien (co CustomerId) VA da quay lai quan >= MinVisitCount
--      lan trong VisitLookbackDays ngay gan nhat. Ca 2 nguong nay CUNG
--      cau hinh duoc tu giao dien theo tung chien dich, khong hardcode.
CREATE TABLE DiscountCampaigns (
    CampaignId          INT IDENTITY(1,1) PRIMARY KEY,
    CampaignName        NVARCHAR(100) NOT NULL,        -- vd: "Khuyen mai he 2026", "Tri an khach quen"
    StartDate           DATETIME2 NOT NULL,
    EndDate             DATETIME2 NOT NULL,
    DiscountPercent     DECIMAL(5,2) NOT NULL,         -- 10.00 nghia la 10%
    RequireMembership   BIT NOT NULL DEFAULT 0,        -- 1 = chi danh cho thanh vien dat dieu kien quay lai ben duoi
    MinVisitCount       INT NULL,                      -- can toi thieu bao nhieu don hang trong khoang ngay duoi day (chi dung khi RequireMembership = 1)
    VisitLookbackDays   INT NULL,                      -- tinh so lan quay lai trong bao nhieu ngay gan nhat (chi dung khi RequireMembership = 1)
    IsActive            BIT NOT NULL DEFAULT 1,        -- tat nhanh 1 chien dich ma khong can xoa (giu lich su)
    Note                NVARCHAR(255) NULL,
    CreatedBy           INT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_DiscountCampaigns_DateRange CHECK (EndDate > StartDate),
    CONSTRAINT CK_DiscountCampaigns_Percent   CHECK (DiscountPercent BETWEEN 0 AND 100),
    -- Neu chien dich yeu cau thanh vien "khach quen" thi BAT BUOC phai
    -- khai bao ca 2 nguong (so lan + so ngay); neu chien dich ap dung
    -- chung cho tat ca thi KHONG duoc khai bao 2 nguong nay (tranh nhap
    -- nhang cau hinh thua khong dung toi).
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

-- Trigger chan tao/sua 1 chien dich active ma KHOANG NGAY bi giao voi 1
-- chien dich active KHAC nhung CUNG doi tuong ap dung (cung la
-- RequireMembership = 0, hoac cung la = 1). 2 chien dich khac doi tuong
-- (1 cho tat ca + 1 rieng cho khach quen) duoc phep chay CUNG luc, vi khi
-- tinh hoa don ham fn_GetActiveDiscountPercent se uu tien chien dich
-- "khach quen" truoc neu khach hang thoa dieu kien.
CREATE TRIGGER trg_DiscountCampaigns_PreventOverlap
ON DiscountCampaigns
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted WHERE IsActive = 1) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN DiscountCampaigns c
            ON c.CampaignId <> i.CampaignId
           AND c.IsActive = 1
           AND i.IsActive = 1
           AND c.RequireMembership = i.RequireMembership   -- chi so sanh trong CUNG 1 nhom doi tuong
           AND i.StartDate < c.EndDate
           AND i.EndDate   > c.StartDate                   -- 2 khoang ngay giao nhau
    )
    BEGIN
        RAISERROR (N'Khoang ngay cua chien dich giam gia nay dang trung voi 1 chien dich khac CUNG doi tuong ap dung.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

-- Ham lay % giam gia dang ap dung cho 1 hoa don, tai 1 thoi diem cu the,
-- cua 1 khach hang cu the (@CustomerId = NULL neu khach vang lai).
-- Uu tien: neu khach la "khach quen" (thoa dieu kien 1 chien dich
-- RequireMembership=1 dang active) thi lay % cua chien dich do; neu
-- khong, lay % cua chien dich ap dung chung (RequireMembership=0) dang
-- active tai thoi diem do; neu khong co gi ca thi tra ve 0.
CREATE FUNCTION fn_GetActiveDiscountPercent
(
    @CustomerId INT,
    @CheckDate  DATETIME2
)
RETURNS DECIMAL(5,2)
AS
BEGIN
    DECLARE @DiscountPercent DECIMAL(5,2) = 0;

    -- Uu tien 1: chien dich rieng cho khach quen (phai la thanh vien VA
    -- du so lan quay lai trong khung ngay chien dich yeu cau)
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

    -- Uu tien 2: neu khong thuoc dien "khach quen", thu chien dich ap
    -- dung chung cho tat ca
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
-- Cach dung khi tao hoa don:
-- SELECT dbo.fn_GetActiveDiscountPercent(@CustomerId, GETDATE());
-- -> @CustomerId = NULL neu khach vang lai (se luon roi vao nhanh "ap
--    dung chung" neu co chien dich dang chay, hoac tra ve 0).
-- -> nhan % nay, tinh Invoices.DiscountAmount = SubTotal * %/100, va luu
--    lai Invoices.AppliedCampaignId (xem cot moi them ben duoi) de sau
--    nay truy vet hoa don nao da duoc ap dung chien dich nao.

-- Ham tinh phu phi VIP cho 1 hoa don, dua tren ban dang su dung va SubTotal:
-- = RoomFee (phi thue phong co dinh) + phan con thieu neu SubTotal chua
-- dat MinimumBillAmount cua phong do. Ban thuong (TableType='Thuong')
-- luon tra ve 0 cho ca 2 gia tri.
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
-- Cach dung khi tao hoa don (Backend C# hoac T-SQL truc tiep):
-- SELECT * FROM dbo.fn_GetVIPRoomExtraCharge(@TableId, @SubTotal);
-- -> lay RoomFee va MinimumGap de cong vao Invoices.VIPRoomFee / VIPMinimumGap,
--    roi tinh: TotalAmount = SubTotal - DiscountAmount + TaxAmount + VIPRoomFee + VIPMinimumGap

/* ======================================================================
   NHOM 6: NHAN SU (Shift, ShiftAssignment, Task)
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
    WorkDate        DATE NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'DaXepLich', -- DaXepLich, DaDiemDanh, VangMat, DaHoanThanh
    CheckInTime     DATETIME2 NULL,
    CheckOutTime    DATETIME2 NULL,
    CONSTRAINT FK_ShiftAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_ShiftAssignments_Shifts FOREIGN KEY (ShiftId) REFERENCES Shifts(ShiftId),
    CONSTRAINT UQ_Employee_Shift_Date UNIQUE (EmployeeId, ShiftId, WorkDate)
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
GO

/* ======================================================================
   NHOM 7: KHACH HANG & KHUYEN MAI (Customer, LoyaltyPoint, Promotion, Voucher)
   ====================================================================== */

CREATE TABLE Customers (
    CustomerId      INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100) NOT NULL,
    Phone           NVARCHAR(20)  NOT NULL UNIQUE,
    Email           NVARCHAR(100) NULL,
    DateOfBirth     DATE NULL,
    LoyaltyPoint    INT NOT NULL DEFAULT 0,
    MemberLevel     NVARCHAR(20) NOT NULL DEFAULT N'ThanhVien', -- ThanhVien, Bac, Vang, KimCuong
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE LoyaltyPointHistory (
    HistoryId       INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT NOT NULL,
    Points          INT NOT NULL,
    Type            NVARCHAR(20) NOT NULL,            -- TichDiem, DoiDiem
    RelatedInvoiceId INT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_LoyaltyHistory_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_LoyaltyHistory_Invoices FOREIGN KEY (RelatedInvoiceId) REFERENCES Invoices(InvoiceId)
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

ALTER TABLE Reservations
    ADD CONSTRAINT FK_Reservations_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId);

ALTER TABLE Orders
    ADD CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId);
GO

/* ======================================================================
   NHOM 8: HE THONG - THONG BAO REALTIME & AUDIT LOG
   ====================================================================== */

CREATE TABLE Notifications (
    NotificationId  INT IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(200) NOT NULL,
    Content         NVARCHAR(1000) NOT NULL,
    NotificationType NVARCHAR(30) NOT NULL,           -- DonHang, DatBan, CaLam, HeThong, KhuyenMai
    TargetRole      NVARCHAR(50) NULL,                -- FK toi Roles(RoleName) o duoi; NULL neu thong bao nham thang toi 1 user cu the qua TargetUserId
    TargetUserId    INT NULL,
    RelatedEntityType NVARCHAR(50) NULL,
    RelatedEntityId INT NULL,
    IsRead          BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (TargetUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Notifications_Roles FOREIGN KEY (TargetRole) REFERENCES Roles(RoleName)
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
GO

/* ======================================================================
   NHOM 9 (BO SUNG): GHEP BAN, CHIA HOA DON, HOAN KHO, LICH SU GIA,
                     FEEDBACK, CANH BAO TON KHO
   ====================================================================== */

/* ----------------------------------------------------------------------
   9A. GHEP BAN: 1 Order co the trai tren NHIEU ban (nhom khach ngoi 2
   ban ke nhau nhung goi chung 1 order). Orders.TableId van giu lai lam
   "ban chinh / ban dai dien" de tuong thich nguoc; bang OrderTables
   duoi day liet ke DAY DU tat ca ban thuc su thuoc order do.
   ---------------------------------------------------------------------- */
CREATE TABLE OrderTables (
    OrderTableId    INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL,
    TableId         INT NOT NULL,
    IsPrimaryTable  BIT NOT NULL DEFAULT 0,           -- ban chinh (trung voi Orders.TableId) hay ban ghep them
    CONSTRAINT FK_OrderTables_Orders FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderTables_Tables FOREIGN KEY (TableId) REFERENCES DiningTables(TableId),
    CONSTRAINT UQ_OrderTables_Order_Table UNIQUE (OrderId, TableId)
);
GO
-- Vi du ghep ban: khach ngoi ca ban A1 va A2, order chinh tao tren A1
-- INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable) VALUES (1, 1, 1); -- A1 la ban chinh
-- INSERT INTO OrderTables (OrderId, TableId, IsPrimaryTable) VALUES (1, 2, 0); -- A2 la ban ghep them
-- Khi ghep ban, nen cap nhat DiningTables.Status = 'DangPhucVu' cho CA HAI ban.

/* ----------------------------------------------------------------------
   9B. CHIA HOA DON: 1 Order co the tach thanh NHIEU Invoice (vd: 4
   khach an chung 1 ban nhung moi nguoi tra tien rieng). Bang InvoiceItems
   ghi ro moi Invoice lay nhung OrderItem nao, voi so luong bao nhieu
   (ho tro chia theo MON, khong chi chia deu theo SO TIEN).
   ---------------------------------------------------------------------- */
CREATE TABLE InvoiceItems (
    InvoiceItemId   INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,
    OrderItemId     INT NOT NULL,
    SplitQuantity   INT NOT NULL,                     -- so luong cua OrderItem nay duoc tinh vao Invoice nay
    SplitAmount     DECIMAL(18,2) NOT NULL,            -- SplitQuantity * UnitPrice (luu san de truy van nhanh)
    CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_InvoiceItems_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems(OrderItemId)
);
GO
-- Vi du: OrderItem "Tra sua tran chau" so luong 4 (1 dong OrderItems, Quantity=4)
-- duoc 2 khach chia doi -> 2 Invoice, moi Invoice 1 dong InvoiceItems voi
-- SplitQuantity = 2. Tong SplitQuantity cua 1 OrderItem tren tat ca Invoice
-- KHONG duoc vuot qua Quantity goc (kiem tra o tang ung dung khi tao hoa don).

CREATE INDEX IX_InvoiceItems_OrderItemId ON InvoiceItems(OrderItemId);
CREATE INDEX IX_InvoiceItems_InvoiceId   ON InvoiceItems(InvoiceId);
GO

/* ----------------------------------------------------------------------
   9C. HUY MON -> HOAN KHO: khi mot OrderItems chuyen Status sang 'DaHuy'
   MA VAN CON O trang thai 'MoiGoi' (bep CHUA bat dau che bien), tu dong
   hoan lai dung so nguyen lieu da tru truoc do. Neu mon da 'DangCheBien'
   tro len thi KHONG hoan (nguyen lieu coi nhu da dung, tranh gian lan).
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_OrderItems_RestoreStockOnCancel
ON OrderItems
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Status) RETURN;

    -- Truong hop 1: huy mon le (MenuItemId), chi hoan neu truoc do dang 'MoiGoi'
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'HuyMon',
        bom.QuantityRequired * ins.Quantity,           -- duong = hoan lai kho
        ins.OrderItemId,
        N'Tu dong hoan kho khi huy mon truoc khi che bien'
    FROM inserted ins
    INNER JOIN deleted del ON del.OrderItemId = ins.OrderItemId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ins.MenuItemId
    WHERE ins.Status = N'DaHuy'
      AND del.Status = N'MoiGoi'                       -- chi hoan neu TRUOC DO chua che bien
      AND ins.MenuItemId IS NOT NULL;

    -- Truong hop 2: huy combo -> hoan kho theo tung mon con trong combo
    INSERT INTO StockTransactions (IngredientId, ChangeType, QuantityChange, RelatedOrderItemId, Note)
    SELECT
        bom.IngredientId,
        N'HuyMon',
        bom.QuantityRequired * ci.Quantity * ins.Quantity,
        ins.OrderItemId,
        N'Tu dong hoan kho khi huy combo truoc khi che bien'
    FROM inserted ins
    INNER JOIN deleted del ON del.OrderItemId = ins.OrderItemId
    INNER JOIN ComboItems ci ON ci.ComboId = ins.ComboId
    INNER JOIN MenuItemIngredients bom ON bom.MenuItemId = ci.MenuItemId
    WHERE ins.Status = N'DaHuy'
      AND del.Status = N'MoiGoi'
      AND ins.ComboId IS NOT NULL;
END;
GO
-- Luu y: trigger nay chi XU LY KHO. Viec Orders.TotalAmount giam theo
-- da duoc trg_OrderItems_SyncOrderTotal (Nhom 4) tu dong xu ly san, vi
-- trigger do da loai tru cac dong Status = 'DaHuy' khoi tong tien.

/* ----------------------------------------------------------------------
   9D. LICH SU GIA BAN: moi lan Price trong MenuItems thay doi, tu dong
   luu lai gia cu/gia moi. Gia tai thoi diem ban van lay tu OrderItems.
   UnitPrice (da luu san luc order) nen KHONG anh huong don don hang cu.
   ---------------------------------------------------------------------- */
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

CREATE INDEX IX_MenuItemPriceHistory_MenuItemId ON MenuItemPriceHistory(MenuItemId, ChangedAt);
GO

/* ----------------------------------------------------------------------
   9E. DANH GIA / PHAN HOI SAU KHI THANH TOAN (Feedback)
   ---------------------------------------------------------------------- */
CREATE TABLE Feedbacks (
    FeedbackId      INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL,                     -- chi cho phep danh gia sau khi da co hoa don
    CustomerId      INT NULL,                         -- null neu khach vang lai khong dang nhap
    Rating          TINYINT NOT NULL,                 -- 1 den 5 sao
    Comment         NVARCHAR(1000) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Feedbacks_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_Feedbacks_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_Feedbacks_Rating CHECK (Rating BETWEEN 1 AND 5)
);

-- Danh gia chi tiet tung mon trong hoa don (tuy chon, dung khi can biet
-- CU THE mon nao khach khong hai long thay vi chi 1 diem chung chung)
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

CREATE INDEX IX_Feedbacks_InvoiceId ON Feedbacks(InvoiceId);
CREATE INDEX IX_Feedbacks_Rating    ON Feedbacks(Rating);
GO

/* ----------------------------------------------------------------------
   9F. CANH BAO NGUYEN LIEU SAP HET: tan dung cot MinStockAlert da co san
   trong Ingredients. Moi khi CurrentStock duoc cap nhat (do trigger
   trg_StockTransactions_UpdateStock o Nhom 3B thuc hien) va tut xuong
   duoi nguong -> tu dong tao 1 Notification bao QuanLy, se duoc
   NotificationService day qua SignalR/Email/FCM nhu cac thong bao khac.
   ---------------------------------------------------------------------- */
CREATE TRIGGER trg_Ingredients_LowStockAlert
ON Ingredients
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(CurrentStock) RETURN;

    INSERT INTO Notifications (Title, Content, NotificationType, TargetRole, RelatedEntityType, RelatedEntityId)
    SELECT
        N'Canh bao sap het nguyen lieu',
        N'Nguyen lieu "' + i.IngredientName + N'" chi con ' + CAST(i.CurrentStock AS NVARCHAR(20))
            + N' ' + i.Unit + N', duoi nguong canh bao (' + CAST(i.MinStockAlert AS NVARCHAR(20)) + N').',
        N'CanhBaoKho',
        N'QuanLy',                                    -- gui cho toan bo user co role QuanLy
        N'Ingredient',
        i.IngredientId
    FROM inserted i
    INNER JOIN deleted d ON d.IngredientId = i.IngredientId
    WHERE i.MinStockAlert > 0
      AND i.CurrentStock < i.MinStockAlert
      AND d.CurrentStock >= i.MinStockAlert;           -- chi bao 1 LAN DUY NHAT luc vua tut qua nguong,
                                                        -- tranh spam thong bao moi lan tru kho nho le
END;
GO

/* ----------------------------------------------------------------------
   9G. GOI Y MON DUA TREN LICH SU KHACH HANG: khong can bang moi, chi can
   1 VIEW tong hop tu du lieu da co (Orders + OrderItems). App goi view
   nay, loc theo CustomerId, sap xep giam dan theo TimesOrdered de lay
   ra "mon khach hay goi nhat" hien thi goi y khi khach dat mon lai.
   ---------------------------------------------------------------------- */
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
-- Cach dung: goi y 5 mon khach hay goi nhat khi khach quen (CustomerId=5) dat ban:
-- SELECT TOP 5 * FROM vw_CustomerFavoriteItems WHERE CustomerId = 5 ORDER BY TimesOrdered DESC;

/* ======================================================================
   INDEX PHU TRO CHO TRUY VAN NHANH VA CHO TRIGGER CHAY NHANH
   ====================================================================== */

-- Index cu (giu nguyen tu ban goc)
CREATE INDEX IX_UserRoles_UserId_IsActive  ON UserRoles(UserId, IsActive);
CREATE INDEX IX_DiningTables_Status        ON DiningTables(Status);
CREATE INDEX IX_Orders_Status_TableId      ON Orders(Status, TableId);
CREATE INDEX IX_Orders_CustomerId_OrderTime ON Orders(CustomerId, OrderTime) WHERE CustomerId IS NOT NULL;
CREATE INDEX IX_OrderItems_OrderId_Status  ON OrderItems(OrderId, Status);
CREATE INDEX IX_KitchenTickets_Status      ON KitchenTickets(Status);
CREATE INDEX IX_Invoices_Status            ON Invoices(Status);
CREATE INDEX IX_Reservations_Time_Status   ON Reservations(ReservationTime, Status);
CREATE INDEX IX_Notifications_TargetUser_IsRead ON Notifications(TargetUserId, IsRead);
CREATE INDEX IX_ShiftAssignments_WorkDate  ON ShiftAssignments(WorkDate);
CREATE INDEX IX_AuditLogs_TableName_RecordId ON AuditLogs(TableName, RecordId);

-- Index MOI (TOI UU - bo sung cho cac cot duoc JOIN trong trigger,
-- vien tan suat cao khi order/tru kho, tranh table scan khi du lieu lon)
CREATE INDEX IX_OrderItems_MenuItemId      ON OrderItems(MenuItemId) WHERE MenuItemId IS NOT NULL;
CREATE INDEX IX_OrderItems_ComboId         ON OrderItems(ComboId) WHERE ComboId IS NOT NULL;
CREATE INDEX IX_MenuItemIngredients_IngredientId ON MenuItemIngredients(IngredientId);
CREATE INDEX IX_StockTransactions_IngredientId   ON StockTransactions(IngredientId);
CREATE INDEX IX_StockTransactions_RelatedOrderItemId ON StockTransactions(RelatedOrderItemId);
CREATE INDEX IX_ComboItems_ComboId         ON ComboItems(ComboId);
CREATE INDEX IX_MenuItems_StationId        ON MenuItems(StationId);
CREATE INDEX IX_KitchenTickets_StationId   ON KitchenTickets(StationId);
CREATE INDEX IX_OrderTables_TableId        ON OrderTables(TableId);
GO


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
(N'nhanvien_hybrid', N'$2b$11$ELlHJFrq5892n/CITm5CDOBrFsFEDtIfFrfe9Gc0ntaeg2xsbDy4S', N'Hoàng Kiêm Nhiệm', N'0905555666', N'hybrid@quanan.com', 1);

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
(5, N'NV005', N'PhucVu', '2024-06-01', 7000000, N'DangLamViec', NULL);
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

-- Thêm cột đánh dấu món MANG VỀ cho OrderItems.
-- Backend cũng tự thêm cột này khi khởi động (WinFormsCompatibilitySchemaService);
-- script này dùng để chạy tay trên DB có sẵn. Chạy nhiều lần an toàn (idempotent).
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.OrderItems', N'IsTakeaway') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItems
        ADD IsTakeaway BIT NOT NULL
            CONSTRAINT DF_OrderItems_IsTakeaway DEFAULT 0 WITH VALUES;
END;
GO

-- (Tùy chọn) Đánh dấu các món của đơn MANG VỀ cũ là mang về
UPDATE oi
SET oi.IsTakeaway = 1
FROM dbo.OrderItems oi
INNER JOIN dbo.Orders o ON o.OrderId = oi.OrderId
WHERE o.OrderType = N'MangVe' AND oi.IsTakeaway = 0;
GO