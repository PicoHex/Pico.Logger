namespace Pico.Logging.Abs;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
