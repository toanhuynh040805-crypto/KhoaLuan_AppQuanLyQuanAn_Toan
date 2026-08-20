using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Models;

namespace QuanAnBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PrintTicket> PrintTickets => Set<PrintTicket>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Giá tiền cần precision rõ ràng cho SQL Server
        modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Payment>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");

        // PrintTickets có 2 FK cascade (DepartmentId và OrderItemId) tạo ra 2 đường xóa
        // cascade khác nhau cùng xuất phát từ Departments (Departments->PrintTickets truc tiep,
        // va Departments->MenuItems->OrderItems->PrintTickets gian tiep) -> SQL Server tu choi
        // tao FK vi loi "multiple cascade paths". Bo cascade o canh DepartmentId de pha vong nay;
        // giu cascade o OrderItemId (xoa mon trong don thi xoa luon phieu in cua no la hop ly).
        modelBuilder.Entity<PrintTicket>()
            .HasOne(pt => pt.Department)
            .WithMany()
            .HasForeignKey(pt => pt.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed dữ liệu cơ bản: 2 bộ phận cố định
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Bep" },
            new Department { Id = 2, Name = "QuayNuoc" }
        );

        modelBuilder.Entity<Role>().HasData(
            // Khop voi 4 Role ma WinForm dang dung de loc Sidebar/dieu huong
            // (xem Controls/SidebarControl.cs va Forms/MainForm.cs ben WinForm):
            // QuanLy, ThuNgan, Bep, QuayNuoc.
            new Role { Id = 1, Name = "QuanLy" },
            new Role { Id = 2, Name = "ThuNgan" },
            new Role { Id = 3, Name = "Bep" },
            new Role { Id = 4, Name = "QuayNuoc" }
        );

        base.OnModelCreating(modelBuilder);
    }
}