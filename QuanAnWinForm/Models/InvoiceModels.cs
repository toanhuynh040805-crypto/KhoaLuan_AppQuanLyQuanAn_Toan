namespace QuanAnWinForm.Models;

public class InvoiceSummaryDto
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string TableName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int Status { get; set; } // 0=ChuaThanhToan, 1=DaThanhToan, 2=DaHuy
    public string WaiterName { get; set; } = "";
    public string CashierName { get; set; } = "";
}

public class InvoiceLineDto
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsTakeaway { get; set; } // món mang về
}

public class InvoicePaymentInfoDto
{
    public int Method { get; set; } // 0=TienMat,1=TheNganHang,2=ViDienTu,3=ChuyenKhoan
    public decimal Amount { get; set; }
    public DateTime PaymentTime { get; set; }
}

public class InvoiceDetailDto
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? OrderTime { get; set; }
    public string TableName { get; set; } = "";
    public string? OrderType { get; set; } // "TaiBan" hoặc "MangVe"
    public string WaiterName { get; set; } = "";
    public string CashierName { get; set; } = "";
    public List<InvoiceLineDto> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CampaignName { get; set; } // ten chien dich khuyen mai da ap dung (neu co)
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
    public List<InvoicePaymentInfoDto> Payments { get; set; } = new();

    public string ShopName { get; set; } = "";
    public string ShopAddress { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccountNo { get; set; } = "";
    public string BankAccountName { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public string TransferContent { get; set; } = "";
    public string QrImageUrl { get; set; } = "";
}