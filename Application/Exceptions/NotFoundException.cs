namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class NotFoundException : ApiException
    {
        public NotFoundException(string message = "Khong tim thay tai nguyen.")
            : base(message, StatusCodes.Status404NotFound, "not_found")
        {
        }
    }
}
