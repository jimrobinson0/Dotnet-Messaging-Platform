namespace Messaging.Api.Exceptions;

public sealed class ApiContractException : Exception
{
    public string ErrorCode { get; }

    public ApiContractException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
