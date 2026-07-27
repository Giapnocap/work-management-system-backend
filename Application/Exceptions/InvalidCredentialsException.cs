namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class InvalidCredentialsException : ApiException
    {
        public InvalidCredentialsException()
            : base(
                "Ten dang nhap hoac mat khau khong dung.",
                StatusCodes.Status401Unauthorized,
                "invalid_credentials")
        {
        }
    }
}
