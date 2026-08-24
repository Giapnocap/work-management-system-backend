namespace WorkManagementSystem.Application.Exceptions
{
    public sealed class InvalidCredentialsException : ApiException
    {
        public InvalidCredentialsException()
            : base(
                "Tên đăng nhập hoặc mật khẩu không đúng.",
                StatusCodes.Status401Unauthorized,
                "invalid_credentials")
        {
        }
    }
}
