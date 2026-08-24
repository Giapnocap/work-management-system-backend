namespace WorkManagementSystem.Application.Exceptions;

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Không xác định được người dùng.")
        : base(message, StatusCodes.Status401Unauthorized, "unauthorized")
    {
    }
}
