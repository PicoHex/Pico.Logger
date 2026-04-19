namespace PicoLog.Tests;

public sealed class AssemblySurfaceTests
{
    [Test]
    public async Task PicoLogAbs_ContainsPublicContractSurface()
    {
        var absAssembly = typeof(ILogger).Assembly;
        var loggerMethods = typeof(ILogger).GetMethods().Where(method => method.Name == nameof(ILogger.Log)).ToArray();
        var logAsyncMethods = typeof(ILogger).GetMethods().Where(method => method.Name == nameof(ILogger.LogAsync)).ToArray();

        await Assert.That(absAssembly.GetType("PicoLog.Abs.ILogSink")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IFlushableLogSink")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.ILogFormatter")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.LogEntry")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IFlushableLoggerFactory")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.FlushExtensions")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IPicoLogControl")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IStructuredLogger")).IsNull();
        await Assert.That(loggerMethods.Any(method => method.GetParameters().Length == 4)).IsTrue();
        await Assert.That(logAsyncMethods.Any(method => method.GetParameters().Length == 5)).IsTrue();
    }

    [Test]
    public async Task PicoLog_ContainsRuntimeImplementationTypesOnly()
    {
        var picoLogAssembly = typeof(LoggerFactory).Assembly;

        await Assert.That(picoLogAssembly.GetType("PicoLog.ILogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.IFlushableLogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.ILogFormatter")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.LogEntry")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.IFlushableLoggerFactory")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.FlushExtensions")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.ILogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.IFlushableLogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.ILogFormatter")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.LogEntry")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.IFlushableLoggerFactory")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.FlushExtensions")).IsNull();
    }
}
