using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infracstructure.DBContext
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Id)
                      .ValueGeneratedOnAdd();

                entity.Property(o => o.TotalPrice).HasColumnType("decimal(18,2)");
                entity.HasMany(o => o.OrderItems)
                      .WithOne(oi => oi.Order)
                      .HasForeignKey(oi => oi.OrderId)
                      .IsRequired(true)
                      .OnDelete(DeleteBehavior.Cascade);

                // Cấu hình mối quan hệ 1-n (PaymentTransaction-Order)
                entity.HasOne(o => o.PaymentTransaction) // Order thuộc về 1 PaymentTransaction (hoặc null)
                      .WithMany(pt => pt.Orders) // PaymentTransaction có nhiều Orders
                      .HasForeignKey(o => o.PaymentTransactionId)
                      .IsRequired(false) // PaymentTransactionId là nullable
                      .OnDelete(DeleteBehavior.Restrict); // Không xóa Order khi xóa Transaction
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(oi => oi.TotalPrice).HasColumnType("decimal(18,2)");
            });

            // Cấu hình cho PaymentTransaction (Đảm bảo TotalAmount là decimal)
            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.Property(pt => pt.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(pt => pt.TransactionId).IsUnique();
            });

            // delete
            modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<Order>();

            // Xử lý PaymentTransaction trước
            var txEntries = ChangeTracker.Entries<PaymentTransaction>();
            foreach (var entry in txEntries)
            {
                if (entry.State == EntityState.Modified)
                {
                    // Tự động cập nhật PaidDate cho PaymentTransaction khi trạng thái chuyển sang Paid
                    if (entry.Entity.PaymentStatus == PaymentStatus.Paid && entry.Entity.PaidDate == null)
                    {
                        entry.Entity.PaidDate = DateTime.UtcNow;
                    }
                }
            }

            // Xử lý Order
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }

                // auto update date everytime update order
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;

                    // Tự động cập nhật DeliveriedDate khi trạng thái chuyển sang Delivered
                    if (entry.Entity.OrderStatus == OrderStatus.Delivered && entry.Entity.DeliveriedDate == null)
                        entry.Entity.DeliveriedDate = DateTime.UtcNow;
                }
            }
            // SaveChangesAsync MỘT LẦN SAU KHI XỬ LÝ HẾT TẤT CẢ CÁC BẢN GHI
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}

