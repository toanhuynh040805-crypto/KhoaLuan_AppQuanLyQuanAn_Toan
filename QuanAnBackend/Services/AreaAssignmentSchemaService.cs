using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

/// <summary>
/// Chỉ đảm bảo schema phân công khu vực tồn tại.
/// Không tự gán nhân viên/khu vực mẫu: toàn bộ phân công là dữ liệu nghiệp vụ
/// do quản trị viên cấu hình.
/// </summary>
public class AreaAssignmentSchemaService
{
    private readonly AppDbContext _db;

    public AreaAssignmentSchemaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
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

            IF OBJECT_ID(N'dbo.ShiftAssignmentAreas', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ShiftAssignmentAreas
                (
                    ShiftAssignmentId INT NOT NULL,
                    AreaId INT NOT NULL,
                    CONSTRAINT PK_ShiftAssignmentAreas
                        PRIMARY KEY (ShiftAssignmentId, AreaId),
                    CONSTRAINT FK_ShiftAssignmentAreas_ShiftAssignments
                        FOREIGN KEY (ShiftAssignmentId)
                        REFERENCES dbo.ShiftAssignments(ShiftAssignmentId)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_ShiftAssignmentAreas_Areas
                        FOREIGN KEY (AreaId)
                        REFERENCES dbo.Areas(AreaId)
                );

                CREATE INDEX IX_ShiftAssignmentAreas_Area
                    ON dbo.ShiftAssignmentAreas(AreaId);
            END;

            -- Chuyển dữ liệu một-khu cũ sang bảng nhiều-khu.
            INSERT INTO dbo.ShiftAssignmentAreas
                (ShiftAssignmentId, AreaId)
            SELECT sa.ShiftAssignmentId, sa.AreaId
            FROM dbo.ShiftAssignments sa
            WHERE sa.AreaId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.ShiftAssignmentAreas saa
                  WHERE saa.ShiftAssignmentId = sa.ShiftAssignmentId
                    AND saa.AreaId = sa.AreaId
              );
            """);
    }
}
