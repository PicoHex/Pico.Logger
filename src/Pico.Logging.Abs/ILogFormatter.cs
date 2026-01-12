namespace Pico.Logging.Abs;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
