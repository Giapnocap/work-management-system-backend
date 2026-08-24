namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class ForbiddenException : ApiException
    {
        public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
            : base(message, StatusCodes.Status403Forbidden, "forbidden")
        {
        }
    }
}
