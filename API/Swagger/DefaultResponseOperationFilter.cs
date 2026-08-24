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
                AddErrorResponse(operation, "400", "Yêu cầu không hợp lệ - validation_error hoặc business_error.", errorSchema);
            }

            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            var isAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any() && !isAnonymous;
            if (requiresAuthorization)
            {
                AddErrorResponse(operation, "401", "Chưa xác thực - thông tin xác thực bị thiếu, không hợp lệ hoặc đã hết hạn.", errorSchema);
                AddErrorResponse(operation, "403", "Bị từ chối - người dùng đã xác thực nhưng không đủ quyền.", errorSchema);
            }

            AddErrorResponse(operation, "404", "Không tìm thấy tài nguyên được yêu cầu.", errorSchema);

            if (!HttpMethods.IsGet(context.ApiDescription.HttpMethod ?? string.Empty))
                AddErrorResponse(operation, "409", "Xung đột do dữ liệu trùng lặp hoặc bị sửa đổi đồng thời.", errorSchema);

            AddErrorResponse(operation, "500", "Lỗi máy chủ nội bộ không mong đợi.", errorSchema);
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
