# GUSTOPOS WEB - PROJECT CONTEXT & RULES

## 1. Định vị nền tảng (Platform Role)
- Đây là ứng dụng WEB dành riêng cho KHÁCH HÀNG (B2C)[cite: 16].
- CÁC CHỨC NĂNG ĐƯỢC PHÉP: Đăng nhập/Đăng ký khách hàng, Xem thực đơn, Đặt bàn, Đặt món trước, Xem lịch sử đơn hàng, Hồ sơ cá nhân[cite: 16]. Thanh toán online chỉ được bật sau khi backend tích hợp nhà cung cấp có xác minh giao dịch.
- CÁC CHỨC NĂNG CẤM THỰC HIỆN TRÊN WEB: Không làm tính năng Quản lý nhân viên, Quản lý bếp, Báo cáo doanh thu, Thêm/Sửa/Xóa món ăn (những cái này thuộc về WinForms/Mobile)[cite: 16].

## 2. Kiến trúc Frontend (Architecture)
- Tech Stack: Vanilla JS (ES6 Modules), Tailwind CSS v4, HTML5. KHÔNG dùng React, Vue hay jQuery[cite: 16].
- Pattern bắt buộc: 
  + `api/`: Chứa các file kết nối API (chỉ fetch data)[cite: 16].
  + `components/`: Chứa các hàm render UI tĩnh (tạo DOM element), không gọi API ở đây[cite: 16].
  + `pages/`: Đóng vai trò Controller, gọi API và truyền data cho components để render[cite: 16].

## 3. Quy tắc Backend & Dữ liệu (.NET 8 & WinForms Contract)
- Base URL: `http://localhost:5000/api`[cite: 16]
- Backend viết bằng .NET 8 (C#) và SQL Server[cite: 16]. Dữ liệu trả về có thể viết hoa chữ cái đầu (VD: `Id`, `Name`), Frontend luôn phải map linh hoạt (ví dụ: `dish.id ?? dish.Id`)[cite: 16].
- **ID & Kiểu dữ liệu:** Kiến trúc backend sử dụng định dạng khóa chính là số nguyên (Integer IDs, VD: `ReservationId`), Frontend phải xử lý đồng bộ chuỗi/số khi truyền qua API.
- **Trạng thái & Phân loại:** Các trường dữ liệu kiểu boolean/bit từ backend (VD: `isAvailable`) có thể trả về số (1/0) hoặc boolean, cần chuẩn hóa rõ ràng bằng `Boolean(...)` trước khi kiểm tra logic.

## 4. Xử lý lỗi & Phản hồi hệ thống (Error Handling)
- Lỗi từ ASP.NET Core API phải được bóc tách triệt để (bắt trọn vẹn cấu trúc `ValidationProblemDetails` từ các thuộc tính `errors`, `title` hoặc `message`) để hiển thị thông báo chính xác cho người dùng.
- Trạng thái giỏ hàng (`cartStore.js`) và dữ liệu mặt hàng phải được kiểm tra tính hợp lệ của giá tiền (`Number(price)`) trước khi ghi vào storage.