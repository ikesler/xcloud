namespace XCloud.Core;

public class XCloudException: Exception
{
    public XCloudException()
    {
    }

    public XCloudException(string? message) : base(message)
    {
    }

    public XCloudException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
