using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

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
            if (RequestBodyMethods.Contains(context.ApiDescription.HttpMethod ?? string.Empty))
            {
                operation.Responses.TryAdd("400", new OpenApiResponse
                {
                    Description = "Bad Request - validation_error or business_error response body."
                });
            }

            operation.Responses.TryAdd("500", new OpenApiResponse
            {
                Description = "Internal Server Error - unexpected server-side failure."
            });
        }
    }
}
