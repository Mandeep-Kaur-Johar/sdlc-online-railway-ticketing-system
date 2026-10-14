using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Data;

public class RailwayDbContext : DbContext
{
    public RailwayDbContext(DbContextOptions<RailwayDbContext> options) : base(options)
    {
    }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Train> Trains => Set<Train>();
    public DbSet<TrainSchedule> TrainSchedules => Set<TrainSchedule>();
    public DbSet<SeatInventory> SeatInventories => Set<SeatInventory>();
    public DbSet<PassengerProfile> PassengerProfiles => Set<PassengerProfile>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
    public DbSet<SupportUpdate> SupportUpdates => Set<SupportUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Station>().HasIndex(s => s.Code).IsUnique();
        modelBuilder.Entity<Train>().HasIndex(t => t.Number).IsUnique();

        modelBuilder.Entity<Booking>().HasIndex(b => b.BookingReference).IsUnique();
        modelBuilder.Entity<Booking>().HasIndex(b => b.IdempotencyKey);
        modelBuilder.Entity<Booking>().Property(b => b.TotalFare).HasPrecision(18, 2);

        modelBuilder.Entity<SeatInventory>().Property(s => s.Fare).HasPrecision(18, 2);
        modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Refund>().Property(r => r.Amount).HasPrecision(18, 2);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Ticket)
            .WithOne(t => t.Booking)
            .HasForeignKey<Ticket>(t => t.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Refund)
            .WithOne(r => r.Booking)
            .HasForeignKey<Refund>(r => r.BookingId);

        modelBuilder.Entity<Ticket>().HasIndex(t => t.TicketNumber).IsUnique();
        modelBuilder.Entity<SupportRequest>().HasIndex(s => s.Reference).IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
