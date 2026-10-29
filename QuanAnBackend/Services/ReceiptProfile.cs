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
        orderId > 0
            ? $"THANH TOAN HD{orderId}"
            : "THANH TOAN TAM TINH";

    public static string BuildVietQrPayload(
        decimal amount,
        int orderId)
    {
        static string Tlv(string id, string value)
        {
            var length = System.Text.Encoding.UTF8.GetByteCount(value);
            return $"{id}{length:D2}{value}";
        }

        var roundedAmount = decimal.ToInt64(
            decimal.Round(amount, 0, MidpointRounding.AwayFromZero));

        var beneficiary = string.Concat(
            Tlv("00", BankId),
            Tlv("01", BankAccountNo));

        var merchantAccountInfo = string.Concat(
            Tlv("00", "A000000727"),
            Tlv("01", beneficiary),
            Tlv("02", "QRIBFTTA"));

        var additionalData = Tlv(
            "08",
            TransferContent(orderId));

        var payload = string.Concat(
            "000201",
            "010212",
            Tlv("38", merchantAccountInfo),
            Tlv("53", "704"),
            Tlv(
                "54",
                roundedAmount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            Tlv("58", "VN"),
            Tlv("62", additionalData));

        var crcInput = payload + "6304";
        return crcInput + ComputeCrc16Ccitt(crcInput);
    }

    private static string ComputeCrc16Ccitt(string input)
    {
        ushort crc = 0xFFFF;
        var bytes = System.Text.Encoding.ASCII.GetBytes(input);

        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (ushort)(
                    (crc & 0x8000) != 0
                        ? (crc << 1) ^ 0x1021
                        : crc << 1);
            }
        }

        return crc.ToString("X4");
    }

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
