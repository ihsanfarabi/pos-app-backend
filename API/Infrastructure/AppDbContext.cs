using Microsoft.EntityFrameworkCore;
using PosApi.Domain;

namespace PosApi.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> Menu => Set<MenuItem>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketLine> TicketLines => Set<TicketLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Price).HasPrecision(9, 2);
        });

        b.Entity<Ticket>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Open");
            e.HasMany(x => x.Lines)
             .WithOne()
             .HasForeignKey(l => l.TicketId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TicketLine>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(9, 2);
            e.Property(x => x.Qty).HasDefaultValue(1);
        });
    }
}