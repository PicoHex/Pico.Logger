namespace PicoLog;

internal static class ConsoleWriteCoordinator
{
    public static Lock OutputLock { get; } = new();
}
