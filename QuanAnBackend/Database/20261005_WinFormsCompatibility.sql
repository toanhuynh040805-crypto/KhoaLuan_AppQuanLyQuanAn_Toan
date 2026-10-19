-- WinForms compatibility patch (idempotent)
-- Generated while synchronizing the independently modified WinForms backend
-- into the shared Mobile/WinForms backend. QuanAnManagementDB_Final.sql remains unchanged.

IF COL_LENGTH(N'dbo.OrderItems', N'CancelReason') IS NULL
    ALTER TABLE dbo.OrderItems ADD CancelReason NVARCHAR(255) NULL;
GO
IF COL_LENGTH(N'dbo.OrderItems', N'CancelledByUserId') IS NULL
    ALTER TABLE dbo.OrderItems ADD CancelledByUserId INT NULL;
GO
IF COL_LENGTH(N'dbo.Reservations', N'DurationMinutes') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations
        ADD DurationMinutes INT NOT NULL
            CONSTRAINT DF_Reservations_DurationMinutes DEFAULT(30) WITH VALUES;
END;
GO
IF COL_LENGTH(N'dbo.Reservations', N'CancelledByUserId') IS NULL
    ALTER TABLE dbo.Reservations ADD CancelledByUserId INT NULL;
GO
IF COL_LENGTH(N'dbo.Reservations', N'CancelledAt') IS NULL
    ALTER TABLE dbo.Reservations ADD CancelledAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.ShiftAssignments', N'AreaId') IS NULL
    ALTER TABLE dbo.ShiftAssignments ADD AreaId INT NULL;
GO
IF OBJECT_ID(N'dbo.FK_Reservations_CancelledByUser', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations
        ADD CONSTRAINT FK_Reservations_CancelledByUser
        FOREIGN KEY (CancelledByUserId) REFERENCES dbo.Users(UserId);
END;
GO
IF OBJECT_ID(N'dbo.FK_ShiftAssignments_Areas', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ShiftAssignments
        ADD CONSTRAINT FK_ShiftAssignments_Areas
        FOREIGN KEY (AreaId) REFERENCES dbo.Areas(AreaId);
END;
GO
