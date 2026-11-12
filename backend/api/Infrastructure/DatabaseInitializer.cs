using Data;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure
{
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Applies pending EF Core migrations and optionally seeds sample data, controlled by
        /// Database:MigrateOnStartup and Database:SeedOnStartup. Retries while the database
        /// container is still starting.
        /// </summary>
        public static async Task InitializeDatabaseAsync(this WebApplication app, bool forceSeed)
        {
            var migrate = app.Configuration.GetValue("Database:MigrateOnStartup", false);
            var seed = forceSeed || app.Configuration.GetValue("Database:SeedOnStartup", false);
            if (!migrate && !seed) return;

            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

            if (migrate)
            {
                const int maxAttempts = 10;
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        await context.Database.MigrateAsync();
                        break;
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(3));
                    }
                }
            }

            if (seed)
            {
                scope.ServiceProvider.GetRequiredService<Seed>().SeedDataContext();
                logger.LogInformation("Sample data seeded");
            }
        }
    }
}
