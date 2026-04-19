using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Models
{
    public class SpaDbContext : DbContext
    {
        public SpaDbContext(DbContextOptions<SpaDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Branch> Branches { get; set; }

        public DbSet<Service> Services { get; set; }
        public DbSet<ServiceCategory> ServiceCategories { get; set; }

        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingDetail> BookingDetails { get; set; }
        
        public DbSet<CustomerNote> CustomerNotes { get; set; }
        public DbSet<Attendance> Attendances { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<Material> Materials { get; set; }
        public DbSet<ServiceMaterial> ServiceMaterials { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<StaffSchedule> StaffSchedules { get; set; }
        public DbSet<WorkSchedule> WorkSchedules { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; } 

        public DbSet<Review> Reviews { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        
        // Địa chỉ (HEAD)
        public DbSet<ThanhPho> ThanhPhos { get; set; }
        public DbSet<Quan> Quans { get; set; }
        public DbSet<DiaChi> DiaChis { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. Cấu hình quan hệ ServiceMaterial
            modelBuilder.Entity<ServiceMaterial>()
                .HasKey(sm => sm.Id);

            modelBuilder.Entity<ServiceMaterial>()
                .HasOne(sm => sm.Service)
                .WithMany(s => s.ServiceMaterials)
                .HasForeignKey(sm => sm.ServiceId);

            modelBuilder.Entity<ServiceMaterial>()
                .HasOne(sm => sm.Material)
                .WithMany(m => m.ServiceMaterials)
                .HasForeignKey(sm => sm.MaterialId);

            // 2. Cấu hình quan hệ User (HEAD)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Customer)
                .WithOne()
                .HasForeignKey<User>(u => u.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Staff)
                .WithOne()
                .HasForeignKey<User>(u => u.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. FIX LỖI DECIMAL & PRECISION (Long)
            modelBuilder.Entity<ServiceMaterial>()
                .Property(sm => sm.Quantity)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                .Property(b => b.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BookingDetail>()
                .Property(bd => bd.PriceAtTime)
                .HasPrecision(18, 2);

            // 4. StockTransaction (HEAD)
            modelBuilder.Entity<StockTransaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId);
                entity.HasOne(e => e.Material)
                      .WithMany()
                      .HasForeignKey(e => e.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 5. Địa chỉ relationships (HEAD)
            modelBuilder.Entity<ThanhPho>(entity =>
            {
                entity.HasKey(tp => tp.MaThanhPho);
            });

            modelBuilder.Entity<Quan>(entity =>
            {
                entity.HasKey(q => q.MaQuan);
                entity.HasOne(q => q.ThanhPho)
                    .WithMany(tp => tp.Quans)
                    .HasForeignKey(q => q.MaThanhPho);
            });

            modelBuilder.Entity<DiaChi>(entity =>
            {
                entity.HasKey(d => d.MaDiaChi);
                entity.HasOne(d => d.Quan)
                    .WithMany(q => q.DiaChis)
                    .HasForeignKey(d => d.MaQuan);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasOne(c => c.DiaChi)
                    .WithMany(d => d.Customers)
                    .HasForeignKey(c => c.MaDiaChi);
            });

            // 6. Review relationships (HEAD)
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(r => r.ReviewId);

                entity.HasOne(r => r.Service)
                      .WithMany(s => s.Reviews)
                      .HasForeignKey(r => r.ServiceId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Customer)
                      .WithMany(c => c.Reviews)
                      .HasForeignKey(r => r.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}