using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

// Tự nâng cấp database khi Backend khởi động (idempotent, chạy nhiều lần vẫn an toàn):
//  1. Gỡ trigger trg_DiscountCampaigns_PreventOverlap - trigger này cấm 2 chiến dịch
//     cùng loại trùng khoảng ngày, không còn phù hợp vì nay nhiều chiến dịch có thể
//     cùng chạy (mỗi hóa đơn chỉ áp dụng 1 chiến dịch do thu ngân chọn).
//  2. Tạo bảng DiscountCampaignConditions (1 chiến dịch - nhiều điều kiện).
//  3. Chuyển chiến dịch "khách quen" kiểu cũ (RequireMembership = 1) thành 1 điều kiện
//     VisitCount, rồi đưa 3 cột cũ về false/null.
public class DiscountCampaignSchemaService
{
    private readonly AppDbContext _db;

    public DiscountCampaignSchemaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        // BƯỚC 1: kiểm tra bảng gốc + gỡ trigger cũ.
        const string dropTriggerSql = """
            IF OBJECT_ID(N'dbo.DiscountCampaigns', N'U') IS NULL
                THROW 51010, N'Required table dbo.DiscountCampaigns does not exist.', 1;

            IF OBJECT_ID(N'dbo.trg_DiscountCampaigns_PreventOverlap', N'TR') IS NOT NULL
                DROP TRIGGER dbo.trg_DiscountCampaigns_PreventOverlap;
            """;
        await _db.Database.ExecuteSqlRawAsync(dropTriggerSql);

        // BƯỚC 2: tạo bảng điều kiện (mỗi loại điều kiện chỉ xuất hiện 1 lần / chiến dịch).
        const string createTableSql = """
            IF OBJECT_ID(N'dbo.DiscountCampaignConditions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DiscountCampaignConditions (
                    ConditionId    INT IDENTITY(1,1) PRIMARY KEY,
                    CampaignId     INT NOT NULL,
                    ConditionType  NVARCHAR(30)  NOT NULL,
                    Value1         NVARCHAR(100) NOT NULL,
                    Value2         NVARCHAR(100) NULL,
                    CONSTRAINT FK_DiscountCampaignConditions_Campaigns
                        FOREIGN KEY (CampaignId) REFERENCES dbo.DiscountCampaigns(CampaignId)
                        ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX UX_DiscountCampaignConditions_CampaignType
                    ON dbo.DiscountCampaignConditions (CampaignId, ConditionType);
            END
            """;
        await _db.Database.ExecuteSqlRawAsync(createTableSql);

        // BƯỚC 3: chuyển dữ liệu "khách quen" kiểu cũ sang bảng điều kiện.
        const string migrateSql = """
            INSERT INTO dbo.DiscountCampaignConditions (CampaignId, ConditionType, Value1, Value2)
            SELECT c.CampaignId,
                   N'VisitCount',
                   CAST(c.MinVisitCount AS NVARCHAR(100)),
                   CAST(c.VisitLookbackDays AS NVARCHAR(100))
            FROM dbo.DiscountCampaigns c
            WHERE c.RequireMembership = 1
              AND c.MinVisitCount IS NOT NULL
              AND c.VisitLookbackDays IS NOT NULL
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.DiscountCampaignConditions x
                    WHERE x.CampaignId = c.CampaignId
                      AND x.ConditionType = N'VisitCount');

            UPDATE dbo.DiscountCampaigns
            SET RequireMembership = 0,
                MinVisitCount = NULL,
                VisitLookbackDays = NULL
            WHERE RequireMembership = 1;
            """;
        await _db.Database.ExecuteSqlRawAsync(migrateSql);
    }
}