namespace PicoLog.Abs;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
