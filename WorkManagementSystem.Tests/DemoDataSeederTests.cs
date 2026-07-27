using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Tests;

public class DemoDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenEnabled_CreatesDemoDataIdempotently()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoSeed:Enabled"] = "true",
                ["DemoSeed:ApplyMigrations"] = "false"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await DemoDataSeeder.SeedAsync(scope.ServiceProvider);

        using (var scope = provider.CreateScope())
            await DemoDataSeeder.SeedAsync(scope.ServiceProvider);

        using var assertScope = provider.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(4, await context.Users.IgnoreQueryFilters()
            .CountAsync(u => u.Username.StartsWith("demo.")));
        Assert.Equal(4, await context.Users.IgnoreQueryFilters()
            .CountAsync(u => u.Username.StartsWith("demo.") && u.TokenVersion == 0));
        Assert.Single(await context.Units.IgnoreQueryFilters()
            .Where(u => u.Name == "Demo Engineering")
            .ToListAsync());
        Assert.Single(await context.Projects.IgnoreQueryFilters()
            .Where(p => p.Name == "Demo Workflow Project")
            .ToListAsync());
        Assert.Equal(4, await context.Tasks.IgnoreQueryFilters()
            .CountAsync(t => t.Title.StartsWith("Demo -")));
        Assert.Equal(3, await context.UserUnits.IgnoreQueryFilters()
            .CountAsync(uu => uu.User!.Username.StartsWith("demo.") && uu.User.Role != "Admin"));
    }
}
