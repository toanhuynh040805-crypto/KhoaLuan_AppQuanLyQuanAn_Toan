namespace QuanAnBackend.Services;

public static class ReceiptProfile
{
    // Thông tin cửa hàng trên bill.
    // GustoPOS là tên phần mềm, không phải tên quán.
    public const string ShopName = "Lẩu Bò Nhà Gỗ Sài Gòn";
    public const string ShopAddress =
        "Chi nhánh Lê Trọng Tấn - 479A Lê Trọng Tấn, Sơn Kỳ, Tân Phú, TP. Hồ Chí Minh";
    public const string SoftwareName = "GustoPOS";

    public const string BankName = "BIDV";
    public const string BankId = "970418";
    public const string BankAccountNo = "5811704638";
    public const string BankAccountName = "HUYNH DUC TOAN";
    public const string BankBranch = "BIDV - PGD Phú Phong";

    // Giữ alias cũ để các client/DTO hiện tại không bị gãy.
    public const string BankCode = BankName;

    public static string TransferContent(int orderId) =>
        $"THANH TOAN HD{orderId}";

    public static string BuildVietQrImageUrl(
        decimal amount,
        int orderId,
        string template = "compact2")
    {
        var roundedAmount = decimal.ToInt64(
            decimal.Round(amount, 0, MidpointRounding.AwayFromZero));

        var description = Uri.EscapeDataString(TransferContent(orderId));
        var accountName = Uri.EscapeDataString(BankAccountName);

        return
            $"https://img.vietqr.io/image/{BankId}-{BankAccountNo}-{template}.png" +
            $"?amount={roundedAmount}&addInfo={description}&accountName={accountName}";
    }
}
