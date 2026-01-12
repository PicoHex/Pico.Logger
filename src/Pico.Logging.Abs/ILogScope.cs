namespace Pico.Logging.Abs;

public interface ILogScope : IDisposable
{
    object State { get; }
}
