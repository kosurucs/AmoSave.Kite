using AmoSave.Kite.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AmoSave.Kite.API.Data;

public class KiteDbContext : DbContext
{
    public KiteDbContext(DbContextOptions<KiteDbContext> options) : base(options) { }

    public DbSet<KiteSession> Sessions => Set<KiteSession>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserMargin> UserMargins => Set<UserMargin>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<GttOrder> GttOrders => Set<GttOrder>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<MarketQuote> MarketQuotes => Set<MarketQuote>();
    public DbSet<HistoricalCandle> HistoricalCandles => Set<HistoricalCandle>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertWebhookPayload> AlertWebhookPayloads => Set<AlertWebhookPayload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KiteSession>(e =>
        {
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => new { s.ApiKey, s.IsActive });
        });

        var listComparer = new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? new List<string>() : v.ToList());

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasIndex(u => u.UserId);
            e.Property(u => u.Exchanges).HasConversion(
                v => string.Join(",", v),
                v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList(),
                listComparer);
            e.Property(u => u.Products).HasConversion(
                v => string.Join(",", v),
                v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList(),
                listComparer);
            e.Property(u => u.OrderTypes).HasConversion(
                v => string.Join(",", v),
                v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList(),
                listComparer);
        });

        modelBuilder.Entity<UserMargin>(e =>
        {
            e.HasIndex(u => new { u.UserId, u.Segment });
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderId);
            e.HasIndex(o => o.UserId);
        });

        modelBuilder.Entity<Trade>(e =>
        {
            e.HasIndex(t => t.TradeId);
            e.HasIndex(t => t.OrderId);
            e.HasIndex(t => t.UserId);
        });

        modelBuilder.Entity<GttOrder>(e =>
        {
            e.HasIndex(g => g.TriggerId);
            e.HasIndex(g => g.UserId);
        });

        modelBuilder.Entity<Holding>(e =>
        {
            e.HasIndex(h => h.UserId);
            e.HasIndex(h => new { h.UserId, h.TradingSymbol, h.Exchange }).IsUnique();
        });

        modelBuilder.Entity<Position>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => new { p.UserId, p.TradingSymbol, p.Exchange, p.Product });
        });

        modelBuilder.Entity<Instrument>(e =>
        {
            e.HasIndex(i => i.InstrumentToken).IsUnique();
            e.HasIndex(i => new { i.Exchange, i.TradingSymbol });
        });

        modelBuilder.Entity<MarketQuote>(e =>
        {
            e.HasIndex(q => q.InstrumentToken);
            e.HasIndex(q => new { q.Exchange, q.TradingSymbol });
        });

        modelBuilder.Entity<HistoricalCandle>(e =>
        {
            e.HasIndex(c => new { c.InstrumentToken, c.Interval, c.Timestamp }).IsUnique();
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.AlertId);
        });

        modelBuilder.Entity<AlertWebhookPayload>(e =>
        {
            e.HasIndex(a => a.OrderId);
            e.HasIndex(a => a.UserId);
        });
    }
}
