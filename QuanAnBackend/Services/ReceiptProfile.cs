namespace QuanAnBackend.Services;

public static class ReceiptProfile
{
    public const string ShopName = "Quán Ăn GustoPOS";
    public const string ShopAddress = "TP. Hồ Chí Minh, Việt Nam";

    public const string BankCode = "BIDV";
    public const string BankAccountNo = "5811704638";
    public const string BankAccountName = "HUYNH DUC TOAN";
    public const string BankBranch = "BIDV - PGD Phú Phong";

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
            $"https://img.vietqr.io/image/{BankCode}-{BankAccountNo}-{template}.png" +
            $"?amount={roundedAmount}&addInfo={description}&accountName={accountName}";
    }
}
