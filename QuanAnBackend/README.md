# QuanAnBackend — Backend API dùng chung cho Mobile (Flutter) & WinForm

## Kiến trúc
- ASP.NET Core Web API (.NET 8) + Entity Framework Core + SQL Server
- SignalR để đẩy dữ liệu realtime (phân luồng order, cập nhật trạng thái bàn)
- Mobile và WinForm đều là client độc lập, gọi cùng 1 bộ API này

## ⚠️ Đã vá các lỗi khiến WinForm không đăng nhập được
1. **Thiếu hẳn endpoint đăng nhập** — WinForm gọi `POST /api/auth/login` nhưng
   trước đây project không có `AuthController` nào cả (chỉ mới có Menu/Tables/
   Orders/Payments). Đã thêm `Controllers/AuthController.cs` +
   `Services/PasswordHasher.cs` (băm SHA256).
2. **Lệch tên Role** — WinForm lọc Sidebar/điều hướng theo 4 role
   `QuanLy, ThuNgan, Bep, QuayNuoc` (xem `Controls/SidebarControl.cs` và
   `Forms/MainForm.cs` bên WinForm), nhưng backend lại seed
   `QuanLy, ThuNgan, PhucVu`. Đã sửa `Data/AppDbContext.cs` cho khớp.
3. **Chưa có tài khoản nào trong DB** — đã bổ sung 4 tài khoản mẫu vào
   `Database/seed_sample_data.sql` (xem bảng bên dưới).
4. **Thiếu `Properties/launchSettings.json`** — có thể khiến Visual Studio
   chạy project ở port ngẫu nhiên, không khớp `http://localhost:5000` mà
   WinForm đang trỏ tới (`Services/ApiConfig.cs`). Đã thêm file này, cố định
   `applicationUrl = http://localhost:5000`.

## Tài khoản đăng nhập mẫu (đều dùng mật khẩu `123456`)
| Username | Role | Vào thẳng màn hình |
|---|---|---|
| `admin` | QuanLy | Tổng quan |
| `thungan` | ThuNgan | Bán hàng |
| `bep` | Bep | Màn hình Bếp |
| `quaynuoc` | QuayNuoc | Màn hình Quầy nước |

## Cách chạy (Visual Studio hoặc CLI)

1. Cài .NET 8 SDK và SQL Server (Express là đủ)
2. Sửa connection string trong `appsettings.json` cho đúng máy bạn
3. Tạo database bằng migration (chạy trong thư mục `QuanAnBackend/`):
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
4. Chạy `Database/seed_sample_data.sql` (bằng SSMS hoặc Azure Data Studio,
   kết nối vào database `QuanAnDB` vừa tạo) để có dữ liệu demo + **4 tài
   khoản đăng nhập ở trên**
5. Chạy project:
   - CLI: `dotnet run` (sẽ chạy ở `http://localhost:5000`, xem log console)
   - Visual Studio: bấm F5, chọn profile **http** (đã cấu hình sẵn port 5000)
   - Swagger UI kiểm tra API tại `http://localhost:5000/swagger`
6. Mở WinForm (`QuanAnWinForm`) và đăng nhập bằng 1 trong 4 tài khoản ở trên.
   Nếu WinForm chạy trên **máy khác** trong mạng LAN (không phải máy đang
   chạy backend), phải sửa `Services/ApiConfig.cs` bên WinForm từ
   `http://localhost:5000` thành `http://<IP-máy-chạy-backend>:5000`, đồng
   thời đổi `applicationUrl` trong `launchSettings.json` này thành
   `http://0.0.0.0:5000` để lắng nghe từ máy khác.

## Các endpoint chính đã scaffold
| Endpoint | Mô tả |
|---|---|
| `POST /api/auth/login` | Đăng nhập, trả về `{ UserId, FullName, Username, RoleName }` |
| `GET /api/tables` | Lấy sơ đồ bàn (Mobile dùng để vẽ) |
| `PUT /api/tables/{id}/status` | Cập nhật trạng thái bàn |
| `GET /api/menu` | Danh sách món (Mobile gọi món) |
| `POST /api/orders/add-items` | Nhân viên order món → **kích hoạt thuật toán phân luồng** |
| `GET /api/orders/{id}` | Chi tiết 1 đơn hàng |
| `POST /api/payments` | Thu ngân tạo hóa đơn + sinh VietQR |
| SignalR Hub: `/hubs/order` | WinForm join group theo bộ phận để nhận phiếu realtime |

## Việc quan trọng cần làm tiếp
1. **`Services/OrderRoutingService.cs`** — đây là core của nghiệp vụ #2, đọc kỹ và tuỳ biến
   (ví dụ: gộp món giống nhau, ưu tiên món nấu lâu...) để làm phong phú thêm cho báo cáo.
2. **`GenerateVietQrPayload`** trong `PaymentsController.cs` hiện là placeholder —
   cần tích hợp API thật của VietQR.io (miễn phí, có docs rõ ràng) hoặc ngân hàng bạn chọn.
3. **Bảo mật đăng nhập**: `PasswordHasher` hiện dùng SHA256 thuần (không có
   salt) — đủ cho demo/đồ án, nhưng cho sản phẩm thật nên đổi sang
   `BCrypt.Net-Next` hoặc ASP.NET Core Identity. Cũng nên thêm JWT (package
   `Microsoft.AspNetCore.Authentication.JwtBearer` đã có sẵn trong .csproj
   nhưng chưa được cấu hình/dùng) để bảo vệ các endpoint còn lại — hiện tất
   cả endpoint (Menu, Orders, Payments...) đều đang mở, không kiểm tra đăng
   nhập.
4. Viết Migration đầu tiên và kiểm tra schema khớp với ERD.

