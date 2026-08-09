using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class DatabaseModelTests
{
    [Fact]
    public void KpiTables_HaveDataIntegrityCheckConstraints()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertContainsCheckConstraint<Progress>(model, "CK_Progress_Percent_Range");
        AssertContainsCheckConstraint<Progress>(model, "CK_Progress_HoursSpent_NonNegative");
        AssertContainsCheckConstraint<TaskItem>(model, "CK_Tasks_ActualHours_NonNegative");
        AssertContainsCheckConstraint<TaskItem>(model, "CK_Tasks_Status_Range");
        AssertContainsCheckConstraint<TaskItem>(model, "CK_Tasks_Date_Range");
        AssertContainsCheckConstraint<TaskAssignee>(model, "CK_TaskAssignee_One_Target");
        AssertContainsCheckConstraint<KpiPeriod>(model, "CK_KpiPeriods_Date_Range");
        AssertContainsCheckConstraint<KpiResult>(model, "CK_KpiResults_Effective_Range");
        AssertContainsCheckConstraint<KpiResult>(model, "CK_KpiResults_NonNegative");
    }

    [Fact]
    public void CriticalTables_HaveUniqueIndexes()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertHasUniqueIndex<User>(model, nameof(User.Username));
        AssertHasUniqueIndex<User>(model, nameof(User.EmployeeCode));
        AssertHasUniqueIndex<Unit>(model, nameof(Unit.Name));
        AssertHasUniqueIndex<UserUnit>(model, nameof(UserUnit.UserId));
        AssertHasUniqueIndex<TaskAssignee>(model, nameof(TaskAssignee.TaskId), nameof(TaskAssignee.UserId));
        AssertHasUniqueIndex<TaskAssignee>(model, nameof(TaskAssignee.TaskId), nameof(TaskAssignee.UnitId));
        AssertHasUniqueIndex<Project>(model, nameof(Project.UnitId), nameof(Project.Name));
        AssertHasUniqueIndex<KpiPeriod>(model, nameof(KpiPeriod.StartDate), nameof(KpiPeriod.EndDate));
        AssertHasUniqueIndex<KpiResult>(model, nameof(KpiResult.PeriodId), nameof(KpiResult.UserId));
        AssertHasUniqueFilteredIndex<UserWorkHistory>(
            model,
            "[EffectiveTo] IS NULL",
            nameof(UserWorkHistory.UserId));
    }

    [Fact]
    public void MutableAggregates_HaveRowVersionConcurrencyTokens()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertIsConcurrencyToken<TaskItem>(model, nameof(TaskItem.RowVersion));
        AssertIsConcurrencyToken<Progress>(model, nameof(Progress.RowVersion));
        AssertIsConcurrencyToken<User>(model, nameof(User.RowVersion));
        AssertIsConcurrencyToken<Unit>(model, nameof(Unit.RowVersion));
        AssertIsConcurrencyToken<Project>(model, nameof(Project.RowVersion));
        AssertIsConcurrencyToken<KpiPeriod>(model, nameof(KpiPeriod.RowVersion));
    }

    [Fact]
    public void OperationalQueries_HaveSupportingIndexes()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertHasIndex<TaskAssignee>(model, nameof(TaskAssignee.UserId), nameof(TaskAssignee.TaskId));
        AssertHasIndex<TaskAssignee>(model, nameof(TaskAssignee.UnitId), nameof(TaskAssignee.TaskId));
        AssertHasIndex<Progress>(model, nameof(Progress.TaskId), nameof(Progress.UpdatedAt));
        AssertHasIndex<Progress>(
            model,
            nameof(Progress.UserId),
            nameof(Progress.Status),
            nameof(Progress.UpdatedAt),
            nameof(Progress.TaskId));
        AssertHasIndex<KpiResult>(model, nameof(KpiResult.PeriodId), nameof(KpiResult.UnitId));
        AssertHasIndex<UserWorkHistory>(
            model,
            nameof(UserWorkHistory.UnitId),
            nameof(UserWorkHistory.EffectiveFrom));
    }

    [Fact]
    public void CriticalRelationships_AvoidCascadeDeletes()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertDeleteBehavior<TaskItem>(model, DeleteBehavior.NoAction, nameof(TaskItem.CreatedBy));
        AssertDeleteBehavior<TaskItem>(model, DeleteBehavior.NoAction, nameof(TaskItem.UnitId));
        AssertDeleteBehavior<TaskItem>(
            model,
            DeleteBehavior.NoAction,
            nameof(TaskItem.ProjectId),
            nameof(TaskItem.UnitId));
        AssertDeleteBehavior<TaskAssignee>(model, DeleteBehavior.NoAction, nameof(TaskAssignee.TaskId));
        AssertDeleteBehavior<TaskAssignee>(model, DeleteBehavior.NoAction, nameof(TaskAssignee.UserId));
        AssertDeleteBehavior<TaskAssignee>(model, DeleteBehavior.NoAction, nameof(TaskAssignee.UnitId));
        AssertDeleteBehavior<User>(model, DeleteBehavior.NoAction, nameof(User.UnitId));
        AssertDeleteBehavior<KpiResult>(model, DeleteBehavior.NoAction, nameof(KpiResult.PeriodId));
        AssertDeleteBehavior<KpiResult>(model, DeleteBehavior.NoAction, nameof(KpiResult.UserId));
        AssertDeleteBehavior<UserWorkHistory>(model, DeleteBehavior.NoAction, nameof(UserWorkHistory.UserId));
        AssertDeleteBehavior<UserWorkHistory>(model, DeleteBehavior.NoAction, nameof(UserWorkHistory.UnitId));
        AssertDeleteBehavior<UploadFile>(model, DeleteBehavior.NoAction, nameof(UploadFile.TaskId));
        AssertDeleteBehavior<UploadFile>(
            model,
            DeleteBehavior.NoAction,
            nameof(UploadFile.ProgressId),
            nameof(UploadFile.TaskId));
        AssertDeleteBehavior<UploadFile>(model, DeleteBehavior.NoAction, nameof(UploadFile.UploadedBy));
    }

    [Fact]
    public void ProjectAndTaskScopes_RequireUnitAndUseCompositeRelationship()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertRequired<Project>(model, nameof(Project.UnitId));
        AssertRequired<TaskItem>(model, nameof(TaskItem.UnitId));
        AssertRequired<UploadFile>(model, nameof(UploadFile.TaskId));
        AssertDeleteBehavior<TaskItem>(
            model,
            DeleteBehavior.NoAction,
            nameof(TaskItem.ProjectId),
            nameof(TaskItem.UnitId));
    }

    [Fact]
    public void KpiResult_StoresRequiredBoundedIdentitySnapshots()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertRequiredWithMaxLength<KpiResult>(model, nameof(KpiResult.FullNameSnapshot), 200);
        AssertRequiredWithMaxLength<KpiResult>(model, nameof(KpiResult.EmployeeCodeSnapshot), 50);
        AssertRequiredWithMaxLength<KpiResult>(model, nameof(KpiResult.UnitNameSnapshot), 200);
    }

    [Fact]
    public void UploadFile_StoresBoundedRelativeStorageMetadata()
    {
        using var context = TestFactory.CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertRequiredWithMaxLength<UploadFile>(model, nameof(UploadFile.FileName), 200);
        AssertRequiredWithMaxLength<UploadFile>(model, nameof(UploadFile.StorageKey), 255);
    }

    private static void AssertContainsCheckConstraint<TEntity>(IModel model, string constraintName)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        Assert.Contains(entityType.GetCheckConstraints(), c => c.Name == constraintName);
    }

    private static void AssertHasUniqueIndex<TEntity>(IModel model, params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
    }

    private static void AssertHasIndex<TEntity>(IModel model, params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        Assert.Contains(entityType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }

    private static void AssertHasUniqueFilteredIndex<TEntity>(
        IModel model,
        string filter,
        params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique &&
            index.GetFilter() == filter &&
            index.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
    }

    private static void AssertDeleteBehavior<TEntity>(IModel model, DeleteBehavior behavior, params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

        Assert.Equal(behavior, foreignKey.DeleteBehavior);
    }

    private static void AssertIsConcurrencyToken<TEntity>(IModel model, string propertyName)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    private static void AssertRequired<TEntity>(IModel model, string propertyName)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");

        Assert.False(property.IsNullable);
    }

    private static void AssertRequiredWithMaxLength<TEntity>(
        IModel model,
        string propertyName,
        int maxLength)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found.");

        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");

        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }
}
