using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Web.Models;

namespace PersonalFinanceTracker.Web.Data;

/// <summary>
/// The EF Core database context. It inherits the ASP.NET Identity tables
/// (AspNetUsers, AspNetRoles, ...) from IdentityDbContext and adds the finance tables.
///
/// Every finance table has a foreign key to AspNetUsers with ON DELETE CASCADE:
/// deleting an account (Settings > Delete account) removes all of that person's data
/// in one step, and a row can never point at a user that doesn't exist.
///
/// If you change anything here, create a migration:
///   dotnet ef migrations add &lt;Name&gt; --output-dir Data/Migrations
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own table configuration must run first.
        base.OnModelCreating(builder);

        builder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Description).HasMaxLength(200);

            // Enums are stored as their names ("Food"), not numbers, so the data stays
            // readable and reordering the enum can't silently re-categorise old rows.
            e.Property(t => t.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);

            // Almost every query is "this user's transactions, newest first".
            e.HasIndex(t => new { t.UserId, t.Date });

            e.HasOne<IdentityUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Budget>(e =>
        {
            e.Property(b => b.Category).HasConversion<string>().HasMaxLength(32);

            // One budget per category per user; saving again updates the limit.
            e.HasIndex(b => new { b.UserId, b.Category }).IsUnique();

            e.HasOne<IdentityUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SavingsGoal>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(80);
            e.HasIndex(g => g.UserId);
            e.HasOne<IdentityUser>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSettings>(e =>
        {
            e.Property(s => s.Theme).HasMaxLength(10);
            e.Property(s => s.Currency).HasMaxLength(3);
            e.HasIndex(s => s.UserId).IsUnique();
            e.HasOne<IdentityUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // A note on money and SQLite: SQLite has no decimal type, so EF stores decimals as
        // TEXT to keep them exact. The trade-off is that SQLite can't SUM or ORDER BY them,
        // which is why FinanceService totals amounts in memory after filtering by user.
        // On SQL Server or PostgreSQL that limitation disappears.
    }
}
