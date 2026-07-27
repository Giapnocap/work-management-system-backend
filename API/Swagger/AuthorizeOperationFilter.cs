using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WorkManagementSystem.API.Swagger
{
    public class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowAnonymous = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            if (allowAnonymous)
                return;

            var authorizeAttributes = context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Concat(context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>())
                .ToList() ?? new List<AuthorizeAttribute>();

            if (!authorizeAttributes.Any())
                return;

            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized - missing or invalid JWT token." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden - authenticated user does not have permission." });

            var roles = authorizeAttributes
                .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roles.Any())
            {
                var roleText = $"Required role(s): {string.Join(", ", roles)}.";
                operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                    ? roleText
                    : $"{operation.Description}{Environment.NewLine}{Environment.NewLine}{roleText}";
            }

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        }
    }
}
