namespace PicoLog.Abs;

public interface ILogScope : IDisposable
{
    object State { get; }
}
