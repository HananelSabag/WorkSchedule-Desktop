using System.IO;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Data;

// EF Core DbContext = Android's RoomDatabase
// One instance shared across the app via DI
public class AppDbContext : DbContext
{
    // DbSet<T> = Android's @Dao — EF Core uses it for all CRUD queries
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<ShiftTemplate> ShiftTemplates { get; set; }
    public DbSet<ShiftRow> ShiftRows { get; set; }
    public DbSet<DayColumn> DayColumns { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbFolder = Path.Combine(appData, "WorkSchedule");
        Directory.CreateDirectory(dbFolder);
        var dbPath = Path.Combine(dbFolder, "workschedule.db");

        options.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ShiftTemplate → ShiftRows (one-to-many)
        modelBuilder.Entity<ShiftRow>()
            .HasOne(r => r.Template)
            .WithMany(t => t.ShiftRows)
            .HasForeignKey(r => r.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // ShiftTemplate → DayColumns (one-to-many)
        modelBuilder.Entity<DayColumn>()
            .HasOne(c => c.Template)
            .WithMany(t => t.DayColumns)
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
