using Microsoft.EntityFrameworkCore;
using SpaN5.Models.SpaN5.Models;

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

        public DbSet<Payment> Payments { get; set; }

        public DbSet<Material> Materials { get; set; }
        public DbSet<ServiceMaterial> ServiceMaterials { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Cấu hình quan hệ ServiceMaterial
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

            // ✅ FIX LỖI DECIMAL (QUAN TRỌNG)
            modelBuilder.Entity<ServiceMaterial>()
                .Property(sm => sm.Quantity)
                .HasPrecision(18, 2);
            modelBuilder.Entity<StockTransaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId);  // ← Đây là fix chính

                // Optional: đặt tên cột nếu khác với property
                // entity.Property(e => e.TransactionId).HasColumnName("TransactionId");

                // Quan hệ với Material (nếu chưa có)
                entity.HasOne(e => e.Material)
                      .WithMany()  // nếu Material không có collection StockTransactions thì để WithMany()
                      .HasForeignKey(e => e.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict); // hoặc Cascade tùy logic
            });
        }
        public DbSet<AuditLog> AuditLogs { get; set; }
    }
}