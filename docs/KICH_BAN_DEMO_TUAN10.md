# Kịch bản demo và checklist test tuần 10

Tài liệu này bám theo góp ý của giảng viên: demo phải chạy xuyên suốt từ khách hàng đến nhân viên, bếp, thu ngân/quản lý và email; đồng thời kiểm tra UI không bị đè/chồng nội dung.

## 1. Chuẩn bị trước khi demo

- Backend chạy và truy cập được từ các máy trong cùng LAN/Wi-Fi.
- Mobile, WinForms và Web cùng trỏ về **một Backend** và **một QuanAnManagementDB**.
- Kiểm tra ảnh thực đơn hiển thị đúng tên món trên cả 3 nền tảng.
- Chuẩn bị tài khoản: Khách hàng, Phục vụ, Bếp/Quầy nước, Thu ngân, Quản lý.
- Xóa/đóng các đơn test cũ nếu chúng làm ảnh hưởng dữ liệu demo.
- Mở sẵn terminal Backend để quan sát lỗi trong lúc demo.

## 2. Kịch bản demo end-to-end

### Bước 1 - Khách hàng
1. Khách chưa có tài khoản -> đăng ký.
2. Đăng nhập.
3. Xem thực đơn.
4. Đặt bàn.
5. Kiểm tra đặt bàn xuất hiện ở phía nhân viên/WinForms.
6. Nếu khách đổi ý trước ngày nhận bàn: thử hủy đặt bàn và xác nhận bàn được giải phóng đúng.
7. Tạo lại đặt bàn để tiếp tục kịch bản.

### Bước 2 - Nhân viên phục vụ
1. Nhận bàn/cho khách vào bàn.
2. Gọi món.
3. Thử ghi chú món.
4. Kiểm tra ghi chú gợi ý gần đây nếu có.
5. Thử sửa/điều chỉnh món theo nghiệp vụ hiện có.
6. Kiểm tra đơn xuất hiện ở Bếp/Quầy nước.
7. Thử chuyển bàn và xác nhận phía WinForms nhận đúng thông tin bàn cũ -> bàn mới.
8. Thử ghép bàn và kiểm tra hai bàn thể hiện quan hệ ghép chính xác.

### Bước 3 - Bếp / Quầy nước
1. Món ở trạng thái **Chờ chế biến**.
2. Nhấn **Bắt đầu chế biến** -> trạng thái thành **Đang chế biến**.
3. Khi đang chế biến, nút phải là **Xác nhận hoàn thành**; không dùng màu xanh/dấu check khiến người xem hiểu nhầm món đã xong.
4. Nhấn **Xác nhận hoàn thành** -> lúc này mới hiện trạng thái **Đã hoàn thành** màu xanh/dấu check.
5. Kiểm tra nhân viên phục vụ nhận được thay đổi trạng thái.

### Bước 4 - Phục vụ món và gọi thanh toán
1. Nhân viên phục vụ xác nhận đã phục vụ món theo luồng hiện có.
2. Nhân viên phục vụ **gọi thanh toán**, không tự thanh toán thay thu ngân nếu tài khoản không có quyền.
3. Thu ngân nhận thông báo/yêu cầu thanh toán.

### Bước 5 - Thu ngân
1. Mở đúng hóa đơn.
2. Kiểm tra món, số lượng, giảm giá và tổng tiền.
3. Thanh toán.
4. Kiểm tra trạng thái hóa đơn đã thanh toán.
5. Nếu là bàn ghép, xác nhận các bàn liên quan được trả về trạng thái trống đúng nghiệp vụ.
6. Kiểm tra hóa đơn nằm trong lịch sử.
7. Thử gửi hóa đơn qua email khách hàng nếu đã có email.

### Bước 6 - Quản lý / Admin Mobile
Kiểm tra màn **Thống kê**:
- chọn hôm nay;
- chọn khoảng ngày bất kỳ;
- doanh thu theo ngày;
- doanh thu theo nhân viên;
- món bán chạy;
- số hóa đơn / giá trị trung bình / tổng giảm giá nếu có dữ liệu.

## 3. Checklist đồng bộ 3 nền tảng

| Nghiệp vụ | Mobile | WinForms | Web | Kết quả cần đạt |
|---|---|---|---|---|
| Thực đơn | ✓ | ✓ | ✓ | Tên/giá/ảnh/trạng thái thống nhất |
| Đặt bàn | ✓ | ✓ | ✓ nếu có màn tương ứng | Cùng trạng thái đặt bàn |
| Gọi món | ✓ | ✓ | - | Cùng Order/OrderItems |
| Bếp đổi trạng thái | ✓ | ✓ | - | Cập nhật cùng trạng thái |
| Chuyển/ghép bàn | ✓ | ✓ | - | Không tạo bàn/đơn ma |
| Gọi thanh toán | ✓ | ✓ | - | Thu ngân nhận đúng bàn/hóa đơn |
| Thanh toán | nhận kết quả | ✓ | ✓ nếu có | Hóa đơn chỉ thanh toán một lần |
| Email hóa đơn | hiển thị trạng thái nếu có | gửi | - | Đúng email khách |
| Thống kê | ✓ quản lý | ✓ nếu có | - | Tổng tiền khớp dữ liệu thật |

## 4. Checklist UI trước buổi demo

- Không có chữ, badge hoặc dữ liệu đè lên nhau.
- Nội dung dài phải xuống dòng hoặc ellipsis hợp lý.
- Sidebar/Bottom navigation không che nội dung.
- Popup Thông báo/Tìm kiếm phải đóng khi chuyển màn hình WinForms.
- Ảnh thực đơn không méo, không đen, không gắn nhầm món.
- Nút thao tác phải thể hiện đúng trạng thái: chưa hoàn thành không được dùng hình thức giống đã hoàn thành.
- Không để thông báo lỗi đỏ kỹ thuật lộ ra nếu có thể hiển thị thông báo thân thiện.
- Test ít nhất một lần trên kích thước Mobile nhỏ trước demo.

## 5. Quy trình test buổi tối trước demo

1. Khởi động Backend.
2. Khởi động Web.
3. Khởi động WinForms.
4. Khởi động Mobile.
5. Chạy **toàn bộ kịch bản từ Bước 1 đến Bước 6**, không bỏ qua.
6. Ghi lại lỗi theo mẫu:
   - nền tảng;
   - tài khoản/role;
   - thao tác;
   - kết quả mong muốn;
   - kết quả thực tế;
   - ảnh/terminal nếu có.
7. Sửa lỗi rồi chạy lại từ đầu các bước có liên quan.

## 6. Báo cáo

Nhóm nên làm Word và PowerPoint song song với việc hoàn thiện phần mềm. Các ảnh chụp trong báo cáo/slide nên lấy từ bản UI đã chỉnh cuối cùng để tránh phải thay lại toàn bộ sau này.
