using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.API.Controllers;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Tests;

public class ArchitectureDependencyTests
{
    [Fact]
    public void ApplicationTypes_DoNotDependOnOuterLayerTypes()
    {
        var applicationTypes = typeof(ITaskService).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("WorkManagementSystem.Application", StringComparison.Ordinal) == true)
            .ToList();

        var violations = applicationTypes
            .SelectMany(type => GetDeclaredDependencies(type)
                .Select(dependency => new { Owner = type, Dependency = dependency }))
            .Where(item =>
                item.Dependency.Namespace?.StartsWith(
                    "WorkManagementSystem.Infrastructure",
                    StringComparison.Ordinal) == true ||
                item.Dependency.Namespace?.StartsWith(
                    "WorkManagementSystem.API",
                    StringComparison.Ordinal) == true)
            .Select(item => $"{item.Owner.FullName} -> {item.Dependency.FullName}")
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Application must not depend on API or Infrastructure:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Controllers_DoNotDependOnDataAccess()
    {
        var controllerTypes = typeof(AuthController).Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                type.Namespace == "WorkManagementSystem.API.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToList();

        var violations = controllerTypes
            .SelectMany(controller => controller.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .SelectMany(parameter => ExpandType(parameter.ParameterType))
                .Select(dependency => new { Controller = controller, Dependency = dependency }))
            .Where(item => IsDataAccessType(item.Dependency))
            .Select(item => $"{item.Controller.Name} -> {item.Dependency.FullName}")
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Controllers must use application services instead of data access:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static IEnumerable<Type> GetDeclaredDependencies(Type type)
    {
        const BindingFlags flags = BindingFlags.Public |
                                   BindingFlags.NonPublic |
                                   BindingFlags.Instance |
                                   BindingFlags.Static |
                                   BindingFlags.DeclaredOnly;

        var dependencies = type.GetConstructors(flags)
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)))
            .Concat(type.GetInterfaces());

        if (type.BaseType != null)
            dependencies = dependencies.Append(type.BaseType);

        return dependencies.SelectMany(ExpandType).Distinct();
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                foreach (var expanded in ExpandType(elementType))
                    yield return expanded;
            }

            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
            yield break;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var expanded in ExpandType(argument))
                yield return expanded;
        }
    }

    private static bool IsDataAccessType(Type type)
    {
        if (type.Namespace?.StartsWith("WorkManagementSystem.Infrastructure", StringComparison.Ordinal) == true)
            return true;

        if (type == typeof(IAppDbContext) || typeof(DbContext).IsAssignableFrom(type))
            return true;

        return type.IsGenericType &&
               type.GetGenericTypeDefinition() == typeof(IGenericRepository<>);
    }
}
