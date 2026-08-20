# QuanAnMobile — Da chuyen doi tu thiet ke Stitch AI

## Nguon goc thiet ke
Toan bo giao dien duoc chuyen doi tu 7 man hinh thiet ke boi Stitch AI
(Design System "Kitchen Precision"), dong bo mau sac/font qua `lib/theme/`.

## Cac man hinh da hoan thanh
| Man hinh | File | Ghi chu |
|---|---|---|
| Dang nhap | `screens/login_screen.dart` | Gradient cam, goi that API `/api/auth/login` |
| Khung dieu huong 4 tab | `screens/main_shell.dart` | So do / Thuc don / Thong bao / Ca nhan |
| So do ban | `screens/table_map_screen.dart` | Filter chip Tat ca/Trong/Dang dung/Da dat |
| Chi tiet ban | `screens/table_detail_screen.dart` | Trang thai tung mon, du lieu mau (TODO noi API that) |
| Thuc don / Goi mon | `screens/menu_screen.dart` | Tim kiem + chip danh muc + gio hang noi |
| Thong bao | `screens/notifications_screen.dart` | Du lieu mau, TODO noi SignalR realtime |
| Ca nhan | `screens/profile_screen.dart` | Thong tin NV + dang xuat |
| Diem danh vao ca | `screens/checkin_screen.dart` | Tinh nang moi tu thiet ke, TODO tich hop GPS that |

## Cach chay
1. `flutter pub get`
2. Sua `lib/services/api_config.dart` cho dung dia chi Backend
3. `flutter run`

## Viec quan trong can lam tiep (theo do uu tien)
1. **Dang nhap that**: da noi san `POST /api/auth/login` — chi can Backend co du user
   mau (xem huong dan tao mat khau BCrypt da lam o cac buoc truoc).
2. **Chi tiet ban dung du lieu that**: hien `TableDetailScreen` dang dung
   `_sampleLines` hardcode. Can noi `GET /api/orders/{id}` va bo sung endpoint
   cap nhat `KitchenStatus` khi nhan vien bam "danh dau da mang ra".
3. **Thong bao realtime**: noi SignalR client (package `signalr_netcore` da co
   san trong pubspec) de `NotificationsScreen` nhan du lieu that thay vi mau.
4. **Diem danh GPS that**: can them package `geolocator`, va Backend can bo sung
   endpoint `POST /api/attendance/checkin` (chua co trong scaffold Backend).
5. **So do ban voi du lieu tinh toan san** (elapsedMinutes, totalAmount...):
   hien Model `RestaurantTable` da co san cac truong nay nhung Backend chua
   tra ve — can bo sung logic tinh toan ben Backend hoac tinh tam thoi ben
   Flutter tu danh sach OrderItems.
