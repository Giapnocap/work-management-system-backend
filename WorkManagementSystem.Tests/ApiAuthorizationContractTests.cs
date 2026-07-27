using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using WorkManagementSystem.API.Controllers;

namespace WorkManagementSystem.Tests;

public class ApiAuthorizationContractTests
{
    [Fact]
    public void Controllers_AreApiControllersWithRoutes()
    {
        foreach (var controllerType in GetControllerTypes())
        {
            Assert.NotNull(controllerType.GetCustomAttribute<ApiControllerAttribute>());
            Assert.NotNull(controllerType.GetCustomAttribute<RouteAttribute>());
        }
    }

    [Fact]
    public void PublicEndpoints_AreLimitedAndExplicit()
    {
        var publicEndpoints = GetActionMethods()
            .Where(action => IsPublicEndpoint(action.ControllerType, action.Method))
            .Select(action => $"{action.ControllerType.Name}.{action.Method.Name}")
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(new[]
        {
            "AuthController.Login",
            "AuthController.Register",
            "UnitController.GetPublic"
        }, publicEndpoints);
    }

    [Theory]
    [InlineData(typeof(TaskController), nameof(TaskController.Create))]
    [InlineData(typeof(TaskController), nameof(TaskController.Update))]
    [InlineData(typeof(TaskController), nameof(TaskController.Delete))]
    [InlineData(typeof(TaskController), nameof(TaskController.Remind))]
    [InlineData(typeof(ProjectController), nameof(ProjectController.GetProjects))]
    [InlineData(typeof(ProjectController), nameof(ProjectController.Create))]
    [InlineData(typeof(ProjectController), nameof(ProjectController.Update))]
    [InlineData(typeof(ProjectController), nameof(ProjectController.Archive))]
    [InlineData(typeof(ReviewController), nameof(ReviewController.Review))]
    public void ManagerWorkflowEndpoints_RequireManagerRole(Type controllerType, string actionName)
    {
        var roles = GetEffectiveRoles(controllerType, actionName);

        Assert.Contains("Manager", roles);
    }

    [Theory]
    [InlineData(typeof(AuthController), nameof(AuthController.ResetPassword))]
    [InlineData(typeof(AuthController), nameof(AuthController.GetPendingUsers))]
    [InlineData(typeof(AuthController), nameof(AuthController.ApproveUser))]
    [InlineData(typeof(AuthController), nameof(AuthController.RejectUser))]
    [InlineData(typeof(KpiController), nameof(KpiController.Create))]
    [InlineData(typeof(KpiController), nameof(KpiController.Lock))]
    [InlineData(typeof(UnitController), nameof(UnitController.Create))]
    [InlineData(typeof(UnitController), nameof(UnitController.Update))]
    [InlineData(typeof(UnitController), nameof(UnitController.Delete))]
    [InlineData(typeof(UnitController), nameof(UnitController.AddMember))]
    [InlineData(typeof(UnitController), nameof(UnitController.RemoveMember))]
    [InlineData(typeof(UserController), nameof(UserController.Update))]
    [InlineData(typeof(UserController), nameof(UserController.Delete))]
    [InlineData(typeof(AuditController), nameof(AuditController.Get))]
    public void AdminWorkflowEndpoints_RequireAdminRole(Type controllerType, string actionName)
    {
        var roles = GetEffectiveRoles(controllerType, actionName);

        Assert.Contains("Admin", roles);
    }

    [Fact]
    public void ProjectController_DoesNotExposeBoardEndpoints()
    {
        var methods = GetDeclaredActionMethods(typeof(ProjectController));

        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Board", StringComparison.OrdinalIgnoreCase) ||
            method.GetCustomAttributes<HttpMethodAttribute>().Any(attribute =>
                (attribute.Template ?? string.Empty).Contains("board", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void TaskController_DoesNotExposeGenericStatusMutationEndpoint()
    {
        var methods = GetDeclaredActionMethods(typeof(TaskController));

        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("UpdateStatus", StringComparison.OrdinalIgnoreCase) ||
            method.GetCustomAttributes<HttpMethodAttribute>().Any(attribute =>
                (attribute.Template ?? string.Empty).Contains("status", StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<Type> GetControllerTypes()
    {
        return typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) &&
                           type is { IsAbstract: false, IsPublic: true } &&
                           type.Name.EndsWith("Controller", StringComparison.Ordinal));
    }

    private static IEnumerable<(Type ControllerType, MethodInfo Method)> GetActionMethods()
    {
        return GetControllerTypes()
            .SelectMany(controllerType => GetDeclaredActionMethods(controllerType)
                .Select(method => (controllerType, method)));
    }

    private static List<MethodInfo> GetDeclaredActionMethods(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes<NonActionAttribute>().Any() == false)
            .ToList();
    }

    private static bool IsPublicEndpoint(Type controllerType, MethodInfo method)
    {
        if (method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            return true;

        return !controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() &&
               !method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }

    private static List<string> GetEffectiveRoles(Type controllerType, string actionName)
    {
        var method = GetDeclaredActionMethods(controllerType).Single(method => method.Name == actionName);

        return controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
