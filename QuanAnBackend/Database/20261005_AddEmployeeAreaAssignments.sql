/*
  Phan cong nhan vien PhucVu theo khu vuc ban.
  Script idempotent: co the chay lai an toan.
  Backend cung tu dong dam bao bang nay khi khoi dong.
*/
IF OBJECT_ID(N'dbo.EmployeeAreaAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeAreaAssignments
    (
        EmployeeAreaAssignmentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        AreaId INT NOT NULL,
        WorkDate DATE NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_EmployeeAreaAssignments_IsActive DEFAULT(1),
        AssignedBy INT NULL,
        AssignedAt DATETIME2 NOT NULL CONSTRAINT DF_EmployeeAreaAssignments_AssignedAt DEFAULT(GETDATE()),
        CONSTRAINT FK_EmployeeAreaAssignments_Employees
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId),
        CONSTRAINT FK_EmployeeAreaAssignments_Areas
            FOREIGN KEY (AreaId) REFERENCES dbo.Areas(AreaId),
        CONSTRAINT FK_EmployeeAreaAssignments_AssignedBy
            FOREIGN KEY (AssignedBy) REFERENCES dbo.Users(UserId)
    );

    CREATE INDEX IX_EmployeeAreaAssignments_EmployeeDate
        ON dbo.EmployeeAreaAssignments(EmployeeId, WorkDate, IsActive);

    CREATE INDEX IX_EmployeeAreaAssignments_Area
        ON dbo.EmployeeAreaAssignments(AreaId, IsActive);
END;
GO

/*
  Vi du phan cong mac dinh:

  INSERT INTO dbo.EmployeeAreaAssignments(EmployeeId, AreaId, WorkDate, IsActive, AssignedBy)
  SELECT e.EmployeeId, a.AreaId, NULL, 1, adminUser.UserId
  FROM dbo.Employees e
  CROSS JOIN dbo.Areas a
  OUTER APPLY (
      SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'admin'
  ) adminUser
  WHERE e.EmployeeCode = N'NV003'
    AND a.AreaName = N'Tầng 1 - Khu A';

  API QuanLy nen duoc dung de cap nhat phan cong thay vi INSERT thu cong.
*/
