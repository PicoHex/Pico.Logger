namespace PicoLog.Abs;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
