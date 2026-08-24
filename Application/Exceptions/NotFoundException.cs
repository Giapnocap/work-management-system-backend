namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class NotFoundException : ApiException
    {
        public NotFoundException(string message = "Không tìm thấy tài nguyên.")
            : base(message, StatusCodes.Status404NotFound, "not_found")
        {
        }
    }
}
