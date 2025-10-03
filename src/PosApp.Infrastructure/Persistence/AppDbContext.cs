using Microsoft.EntityFrameworkCore;
using PosApp.Domain.Entities;

namespace PosApp.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> Menu => Set<MenuItem>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketLine> TicketLines => Set<TicketLine>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Price).HasPrecision(9, 2);
            e.HasIndex(x => x.Name);
        });

        b.Entity<Ticket>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Open");
            e.HasMany(x => x.Lines)
             .WithOne()
             .HasForeignKey(l => l.TicketId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CreatedAt);

            var navigation = e.Metadata.FindNavigation(nameof(Ticket.Lines));
            if (navigation is not null)
            {
                navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
                navigation.SetField("_lines");
            }
        });

        b.Entity<TicketLine>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(9, 2);
            e.Property(x => x.Qty).HasDefaultValue(1);
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).HasDefaultValue("user");
        });
    }
}
