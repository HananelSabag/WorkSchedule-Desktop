using System.Windows;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Data;

namespace WorkSchedule;

public partial class App : Application
{
    // Shared DbContext instance — equivalent to Android's Room database singleton
    public static AppDbContext Database { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Database = new AppDbContext();

        // EnsureCreated = creates the SQLite file + all tables if they don't exist yet
        // (for production we'll switch to proper migrations)
        await Database.Database.EnsureCreatedAsync();

        // Seed default shift template on first launch
        await DatabaseSeeder.SeedAsync(Database);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Database?.Dispose();
        base.OnExit(e);
    }
}
