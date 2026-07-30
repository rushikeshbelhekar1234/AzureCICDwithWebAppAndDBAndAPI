using System;

namespace WebApplication2.Exceptions
{
    // Simple application exception that can carry an HTTP status code
    public class AppException : Exception
    {
        public int StatusCode { get; }

        public AppException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
