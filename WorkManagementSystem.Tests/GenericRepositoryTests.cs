using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class GenericRepositoryTests
{
    [Fact]
    public async Task AddAsync_DoesNotCommitUntilUnitOfWorkSaves()
    {
        var saveCounter = new SaveChangesCounterInterceptor();
        await using var context = TestFactory.CreateDbContext(saveCounter);
        var repository = TestFactory.Repo<User>(context);

        await repository.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Username = "repository-user",
            FullName = "Repository User",
            EmployeeCode = "REP001",
            PasswordHash = "hash",
            Role = "User",
            IsApproved = true
        });

        Assert.Equal(0, saveCounter.Count);

        await context.SaveChangesAsync();

        Assert.Equal(1, saveCounter.Count);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityDoesNotExist_ReturnsNull()
    {
        await using var context = TestFactory.CreateDbContext();
        var repository = TestFactory.Repo<User>(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
