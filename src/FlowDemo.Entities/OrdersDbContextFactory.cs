using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowDemo.Entities;

// dotnet ef migrations --project .\src\FlowDemo.Entities\ add InitialCreate -- "Host=localhost;Username=test;Password=Test!123"
// dotnet ef database --project .\src\FlowDemo.Entities\ update -- "Host=localhost;Username=test;Password=Test!123"

public class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrdersDbContext>();
        if (args.Length < 1)
            throw new InvalidOperationException("No connection string provided.");

        var connectionString = args[0];
        Console.WriteLine($"Using connection string: {connectionString}");
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("FlowDemo.Entities"));
        return new OrdersDbContext(optionsBuilder.Options);
    }
}
