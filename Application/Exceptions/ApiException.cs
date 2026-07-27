namespace WorkManagementSystem.Application.Exceptions
{
    public abstract class ApiException : Exception
    {
        protected ApiException(string message, int statusCode, string code, IReadOnlyDictionary<string, string[]>? errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            Errors = errors;
        }

        public int StatusCode { get; }
        public string Code { get; }
        public IReadOnlyDictionary<string, string[]>? Errors { get; }
    }
}
