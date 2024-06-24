using Microsoft.EntityFrameworkCore;

namespace FlowDemo.Entities;

public class OrdersDbContext: DbContext
{
    public DbSet<OrderEntity> Orders { get; set; }

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options): base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var order = modelBuilder.Entity<OrderEntity>();
        order.HasKey(o => o.OrderId);
        order.Property(o => o.CreatedBy).IsRequired().HasMaxLength(1024);
        order.Property(o => o.CreatedOn)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        order.Property(o => o.RowVersion).IsConcurrencyToken();
    }
}