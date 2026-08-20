-- Chạy sau khi đã Migration/EnsureCreated xong (bảng Departments, Roles đã có sẵn qua HasData)
-- Script này chỉ thêm dữ liệu mẫu để demo: bàn, danh mục, món ăn

INSERT INTO Tables (TableName, Capacity, Status) VALUES
('Ban 01', 4, 0), ('Ban 02', 4, 0), ('Ban 03', 6, 0), ('Ban 04', 2, 0);

INSERT INTO Categories (Name) VALUES ('Mon chinh'), ('Khai vi'), ('Nuoc uong');

-- DepartmentId: 1 = Bep, 2 = QuayNuoc (đã seed sẵn qua HasData trong AppDbContext)
INSERT INTO MenuItems (Name, Price, IsAvailable, CategoryId, DepartmentId) VALUES
('Com tam suon', 45000, 1, 1, 1),
('Pho bo', 50000, 1, 1, 1),
('Goi cuon', 35000, 1, 2, 1),
('Tra da', 10000, 1, 3, 2),
('Nuoc cam ep', 25000, 1, 3, 2),
('Coca Cola', 15000, 1, 3, 2);

-- Tai khoan dang nhap mau (mat khau cho ca 4 tai khoan: 123456)
-- Hash duoi day la SHA256("123456") viet HOA, dung khop voi Services/PasswordHasher.cs
-- RoleId: 1=QuanLy, 2=ThuNgan, 3=Bep, 4=QuayNuoc (da seed san qua HasData trong AppDbContext)
INSERT INTO Users (FullName, Username, PasswordHash, IsActive, RoleId) VALUES
(N'Quan Ly', 'admin', '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', 1, 1),
(N'Thu Ngan', 'thungan', '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', 1, 2),
(N'Nhan Vien Bep', 'bep', '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', 1, 3),
(N'Nhan Vien Quay Nuoc', 'quaynuoc', '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', 1, 4);
