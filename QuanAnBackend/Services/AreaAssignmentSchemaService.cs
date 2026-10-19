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
            """);
    }
}
