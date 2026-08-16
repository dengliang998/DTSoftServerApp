namespace DTSoft.Core.Exceptions;

public class DtSoftException : Exception
{
    public DtSoftException(
        string message,
        int statusCode = 400,
        string errorCode = "business.error",
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public static DtSoftException BadRequest(string message, string errorCode = "business.badRequest")
        => new(message, 400, errorCode);

    public static DtSoftException NotFound(string message, string errorCode = "business.notFound")
        => new(message, 404, errorCode);

    public static DtSoftException Conflict(string message, string errorCode = "business.conflict")
        => new(message, 409, errorCode);

    public static DtSoftException Forbidden(string message, string errorCode = "business.forbidden")
        => new(message, 403, errorCode);

    public static DtSoftException BadGateway(string message, string errorCode = "business.badGateway")
        => new(message, 502, errorCode);
}
