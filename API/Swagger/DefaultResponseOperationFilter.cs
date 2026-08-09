using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WorkManagementSystem.API.Contracts;

namespace WorkManagementSystem.API.Swagger
{
    public class DefaultResponseOperationFilter : IOperationFilter
    {
        private static readonly HashSet<string> RequestBodyMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "POST",
            "PUT",
            "PATCH"
        };

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var errorSchema = context.SchemaGenerator.GenerateSchema(
                typeof(ApiProblemDetails),
                context.SchemaRepository);

            if (RequestBodyMethods.Contains(context.ApiDescription.HttpMethod ?? string.Empty))
            {
                AddErrorResponse(operation, "400", "Bad Request - validation_error or business_error.", errorSchema);
            }

            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            var isAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any() && !isAnonymous;
            if (requiresAuthorization)
            {
                AddErrorResponse(operation, "401", "Unauthorized - missing, invalid, or expired credentials.", errorSchema);
                AddErrorResponse(operation, "403", "Forbidden - authenticated user lacks permission.", errorSchema);
            }

            AddErrorResponse(operation, "404", "Not Found - requested resource does not exist.", errorSchema);

            if (!HttpMethods.IsGet(context.ApiDescription.HttpMethod ?? string.Empty))
                AddErrorResponse(operation, "409", "Conflict - duplicate or concurrently modified data.", errorSchema);

            AddErrorResponse(operation, "500", "Internal Server Error - unexpected server-side failure.", errorSchema);
        }

        private static void AddErrorResponse(
            OpenApiOperation operation,
            string statusCode,
            string description,
            OpenApiSchema schema)
        {
            operation.Responses.TryAdd(statusCode, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = schema }
                }
            });
        }
    }
}
