using Microsoft.EntityFrameworkCore;

namespace FlowDemo.Backend;

public static class Extensions
{
    public static void ApplyMigrations<T>(this IServiceProvider provider) where T: DbContext
    {
        var log = provider.GetRequiredService<ILogger<T>>();
        var errors = 0;
        while (true)
        {
            try
            {
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<T>();
                context.Database.Migrate();
                return;
            }
            catch (Npgsql.NpgsqlException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
            {
                errors++;
                if (errors > 5) throw;
                log.LogWarning("Database is not ready yet. Retrying in 1 second...");
                Thread.Sleep(1000);
            }
        }
    }
}
