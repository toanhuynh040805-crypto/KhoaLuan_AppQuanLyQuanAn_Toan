namespace QuanAnWinForm.Services;

// Luu thong tin phien dang nhap hien tai - dung STATIC vi ApiService duoc
// khoi tao MOI (new ApiService()) o rat nhieu man hinh khac nhau (khong phai
// singleton), can 1 noi TRUNG TAM de moi ApiService instance deu doc duoc
// Token/Role/UserId hien tai ma khong phai truyen tham so qua tung Form/Control.
public static class SessionContext
{
    public static string? Token { get; set; }
    public static int UserId { get; set; }
    public static string FullName { get; set; } = "";
    public static string EmployeeCode { get; set; } = "";
    public static string RoleName { get; set; } = "";
    public static int? StationId { get; set; }
    public static string StationName { get; set; } = "";

    public static void Clear()
    {
        Token = null;
        UserId = 0;
        FullName = "";
        EmployeeCode = "";
        RoleName = "";
        StationId = null;
        StationName = "";
    }
}