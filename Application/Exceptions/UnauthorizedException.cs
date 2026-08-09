namespace WorkManagementSystem.Application.Exceptions;

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Khong xac dinh duoc nguoi dung.")
        : base(message, StatusCodes.Status401Unauthorized, "unauthorized")
    {
    }
}
