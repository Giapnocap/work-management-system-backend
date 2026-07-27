namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class BusinessException : ApiException
    {
        public BusinessException(string message)
            : base(message, StatusCodes.Status400BadRequest, "business_error")
        {
        }
    }
}
