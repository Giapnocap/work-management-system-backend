namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class ForbiddenException : ApiException
    {
        public ForbiddenException(string message = "Ban khong co quyen thuc hien thao tac nay.")
            : base(message, StatusCodes.Status403Forbidden, "forbidden")
        {
        }
    }
}
