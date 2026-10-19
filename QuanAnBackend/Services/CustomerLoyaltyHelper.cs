namespace QuanAnBackend.Services;

using QuanAnBackend.Models;

// Các hằng số + quy tắc tính điểm khách hàng thân thiết - tách riêng ra đây để
// CustomersController và PaymentsController dùng CHUNG, tránh lệch quy tắc giữa
// 2 nơi (vd 1 nơi tính hạng theo ngưỡng khác nơi kia).
public static class CustomerLoyaltyHelper
{
    // Cứ 10.000đ chi tiêu (SAU khi trừ khuyến mãi) tích 1 điểm.
    public const decimal EarnRateVndPerPoint = 10_000m;

    // 1 điểm quy đổi = 1.000đ giảm giá khi khách dùng điểm thanh toán.
    public const decimal PointValueVnd = 1_000m;

    // Ngưỡng nâng/hạ hạng dựa trên SỐ DƯ ĐIỂM HIỆN CÓ (LoyaltyPoint) - mô hình
    // "hạng theo số dư" đơn giản, dễ giải thích trong báo cáo khóa luận: khách
    // đổi hết điểm sẽ tụt lại hạng Thành viên. Nếu muốn giữ hạng vĩnh viễn, cần
    // thêm 1 cột riêng kiểu "TotalEarnedPoint" để theo dõi độc lập.
    public static MemberLevel CalculateMemberLevel(int currentPoints) => currentPoints switch
    {
        >= 1000 => MemberLevel.KimCuong,
        >= 400 => MemberLevel.Vang,
        >= 100 => MemberLevel.Bac,
        _ => MemberLevel.ThanhVien
    };

    public static int CalculateEarnedPoints(decimal amountAfterDiscount)
        => (int)Math.Floor(amountAfterDiscount / EarnRateVndPerPoint);
}