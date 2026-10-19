using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Models;

namespace QuanAnBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Area> Areas => Set<Area>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Station> Stations => Set<Station>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<KitchenTicket> KitchenTickets => Set<KitchenTicket>();
    public DbSet<DishStatusHistory> DishStatusHistories => Set<DishStatusHistory>();

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<StaffTask> StaffTasks => Set<StaffTask>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<DiscountCampaign> DiscountCampaigns => Set<DiscountCampaign>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyPointHistory> LoyaltyPointHistories => Set<LoyaltyPointHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiningTable>().ToTable("DiningTables", tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<DishStatusHistory>().ToTable("DishStatusHistory");
        modelBuilder.Entity<StaffTask>().ToTable("Tasks");
        modelBuilder.Entity<LoyaltyPointHistory>().ToTable("LoyaltyPointHistory");

        modelBuilder.Entity<OrderItem>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<MenuItem>().ToTable(tb => tb.UseSqlOutputClause(false));
        // DiscountCampaigns có trigger kiểm tra giao nhau; SQL Server không cho
        // EF dùng OUTPUT trực tiếp khi bảng có trigger.
        modelBuilder.Entity<DiscountCampaign>().ToTable(tb => tb.UseSqlOutputClause(false));

        modelBuilder.Entity<Role>().HasKey(r => r.RoleId);
        modelBuilder.Entity<User>().HasKey(u => u.UserId);
        modelBuilder.Entity<UserRole>().HasKey(ur => ur.UserRoleId);
        modelBuilder.Entity<Employee>().HasKey(e => e.EmployeeId);
        modelBuilder.Entity<Area>().HasKey(a => a.AreaId);
        modelBuilder.Entity<DiningTable>().HasKey(t => t.TableId);
        modelBuilder.Entity<Station>().HasKey(s => s.StationId);
        modelBuilder.Entity<Category>().HasKey(c => c.CategoryId);
        modelBuilder.Entity<MenuItem>().HasKey(m => m.MenuItemId);
        modelBuilder.Entity<Order>().HasKey(o => o.OrderId);
        modelBuilder.Entity<OrderItem>().HasKey(oi => oi.OrderItemId);
        modelBuilder.Entity<KitchenTicket>().HasKey(kt => kt.KitchenTicketId);
        modelBuilder.Entity<DishStatusHistory>().HasKey(h => h.HistoryId);
        modelBuilder.Entity<Shift>().HasKey(s => s.ShiftId);
        modelBuilder.Entity<ShiftAssignment>().HasKey(sa => sa.ShiftAssignmentId);
        modelBuilder.Entity<StaffTask>().HasKey(t => t.TaskId);
        modelBuilder.Entity<Invoice>().HasKey(i => i.InvoiceId);
        modelBuilder.Entity<Payment>().HasKey(p => p.PaymentId);
        modelBuilder.Entity<Reservation>().HasKey(r => r.ReservationId);
        modelBuilder.Entity<DiscountCampaign>().HasKey(c => c.CampaignId);
        modelBuilder.Entity<Customer>().HasKey(c => c.CustomerId);
        modelBuilder.Entity<LoyaltyPointHistory>().HasKey(h => h.HistoryId);
        modelBuilder.Entity<Notification>().HasKey(n => n.NotificationId);

        modelBuilder.Entity<Employee>().Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DiningTable>().Property(t => t.TableType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DiningTable>().Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Category>().Property(c => c.CategoryType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Order>().Property(o => o.ClientRequestId).HasMaxLength(64);
        modelBuilder.Entity<Order>().Property(o => o.Source).HasMaxLength(20);
        modelBuilder.Entity<Order>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<OrderItem>().Property(oi => oi.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<KitchenTicket>().Property(kt => kt.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DishStatusHistory>().Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Invoice>().Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Payment>().Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Payment>().Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ShiftAssignment>().Property(sa => sa.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<StaffTask>().Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Reservation>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Reservation>().Property(r => r.Source).HasMaxLength(20);
        modelBuilder.Entity<Customer>().Property(c => c.MemberLevel).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<LoyaltyPointHistory>().Property(h => h.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Notification>().Property(n => n.NotificationType).HasConversion<string>().HasMaxLength(30);

        modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<MenuItem>().Property(m => m.RefillPrice).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Employee>().Property(e => e.Salary).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Order>().Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<OrderItem>().Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.SubTotal).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.DiscountAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.VIPRoomFee).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.VIPMinimumGap).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>().Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Payment>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Reservation>().Property(r => r.DepositAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<DiscountCampaign>().Property(c => c.DiscountPercent).HasColumnType("decimal(5,2)");

        modelBuilder.Entity<Order>().Property(o => o.RowVer).IsRowVersion();
        modelBuilder.Entity<OrderItem>().Property(oi => oi.RowVer).IsRowVersion();
        modelBuilder.Entity<Invoice>().Property(i => i.RowVer).IsRowVersion();

        modelBuilder.Entity<Role>().HasIndex(r => r.RoleName).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Station>().HasIndex(s => s.StationName).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.UserId).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.EmployeeCode).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
        modelBuilder.Entity<ShiftAssignment>()
            .HasIndex(sa => new { sa.EmployeeId, sa.ShiftId, sa.WorkDate }).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(c => c.Phone).IsUnique();

        modelBuilder.Entity<DishStatusHistory>()
            .HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.CancelledByUser)
            .WithMany()
            .HasForeignKey(oi => oi.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.CancelledByUser)
            .WithMany()
            .HasForeignKey(r => r.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(sa => sa.Area)
            .WithMany()
            .HasForeignKey(sa => sa.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.CreatedByUser)
            .WithMany()
            .HasForeignKey(i => i.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DiscountCampaign>()
            .HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LoyaltyPointHistory>()
            .HasOne(h => h.Customer)
            .WithMany(c => c.LoyaltyPointHistories)
            .HasForeignKey(h => h.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LoyaltyPointHistory>()
            .HasOne(h => h.RelatedInvoice)
            .WithMany()
            .HasForeignKey(h => h.RelatedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
