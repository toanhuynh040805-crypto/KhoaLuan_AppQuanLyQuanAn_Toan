# QuanAnWinForm — Da chuyen doi tu thiet ke Stitch AI (pattern InitializeComponent + .Designer.cs)

## Kien truc moi
1 shell duy nhat (`MainForm`) gom:
- `Controls/SidebarControl` — menu trai, TU DONG LOC theo Role dang nhap (Huong 1 da chot: 1 app, khong tach rieng app Quan ly/Thu ngan)
- `Controls/HeaderBar` — thanh tim kiem + thong bao + avatar
- Vung noi dung giua, thay doi UserControl tuong ung khi bam sidebar

Moi man hinh la 1 cap file `XxxControl.cs` (logic) + `XxxControl.Designer.cs`
(khai bao control, co the mo bang WinForms Designer trong Visual Studio).

## Danh sach 8 man hinh da chuyen doi
| Man hinh | UserControl | Role duoc xem |
|---|---|---|
| Tong quan (Dashboard) | `DashboardControl` | QuanLy |
| Ban hang / Thanh toan (POS) | `SalesControl` | QuanLy, ThuNgan |
| Quan ly so do ban | `TableManagementControl` | QuanLy, ThuNgan |
| Quan ly Thuc don | `MenuManagementControl` | QuanLy |
| Quan ly Nhan vien | `StaffControl` | QuanLy |
| Man hinh Bep | `KitchenBoardControl("Bep", ...)` | QuanLy, Bep |
| Man hinh Quay nuoc | `KitchenBoardControl("QuayNuoc", ...)` | QuanLy, QuayNuoc |
| Thong ke doanh thu | `RevenueControl` | QuanLy, ThuNgan |
| Cai dat he thong | `SettingsControl` | QuanLy |

## Control tai su dung (Controls/)
- `SidebarControl`, `HeaderBar` — khung shell
- `StatCard` — the thong ke (Dashboard, Doanh thu, Nhan vien)
- `MiniBarChartPanel` — bieu do cot GDI+ (Dashboard, Doanh thu)

## Cach chay
1. Mo bang Visual Studio 2022 (workload .NET desktop development)
2. Sua `Services/ApiConfig.cs` cho dung dia chi Backend
3. Dam bao Backend dang chay + da co user mau (xem seed_users.sql tu buoc truoc)
4. F5 — dang nhap bang 1 trong cac tai khoan da tao (admin/thungan/bep/quaynuoc, mat khau 123456)

## Viec quan trong can lam tiep (theo do uu tien)
1. **CRUD Menu/Ban/Nhan vien chua co API that**: `MenuManagementControl`,
   `StaffControl` dang chi hien thi (GET) hoac demo (Them/Sua/Xoa bang
   MessageBox). Backend can bo sung POST/PUT/DELETE cho `/api/menu`,
   `/api/tables`, `/api/users`.
2. **SalesControl chua tao Order that**: nut Thanh toan hien la demo.
   Can noi dung luong: gui `/api/orders/add-items` truoc, roi moi goi
   `/api/payments` voi OrderId that tra ve.
3. **KitchenBoardControl**: nut "Hoan thanh tat ca" va checkbox tung mon
   chua goi API cap nhat `KitchenStatus` — can bo sung endpoint tuong ung.
4. **RevenueControl dang dung du lieu mau**: can bo sung
   `GET /api/reports/revenue` va `GET /api/reports/invoices` ben Backend.
5. **SettingsControl chua luu duoc gi**: can thiet ke bang cau hinh rieng
   (VD bang `SystemSettings` key-value) ben Backend.
6. **Phan quyen chi tiet hon**: hien Sidebar da loc theo Role, nhung ben
   trong tung man (VD nut Xoa trong MenuManagementControl) chua kiem tra
   lai quyen — nen bo sung neu can chat che hon.
