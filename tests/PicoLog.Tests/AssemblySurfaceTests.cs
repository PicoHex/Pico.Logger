namespace PicoLog.Tests;

public sealed class AssemblySurfaceTests
{
    [Test]
    public async Task PicoLogAbs_ContainsOnlyConsumerFacingContracts()
    {
        var absAssembly = typeof(ILogger).Assembly;

        await Assert.That(absAssembly.GetType("PicoLog.Abs.ILogSink")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IFlushableLogSink")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.ILogFormatter")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.LogEntry")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IFlushableLoggerFactory")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.FlushExtensions")).IsNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IPicoLogControl")).IsNotNull();
        await Assert.That(absAssembly.GetType("PicoLog.Abs.IStructuredLogger")).IsNotNull();
    }

    [Test]
    public async Task PicoLog_ContainsRuntimeAndExtensibilityContracts()
    {
        var picoLogAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .Single(assembly => string.Equals(assembly.GetName().Name, "PicoLog", StringComparison.Ordinal));

        await Assert.That(picoLogAssembly.GetType("PicoLog.ILogSink")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.IFlushableLogSink")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.ILogFormatter")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.LogEntry")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.IFlushableLoggerFactory")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.FlushExtensions")).IsNotNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.ILogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.IFlushableLogSink")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.ILogFormatter")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.LogEntry")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.IFlushableLoggerFactory")).IsNull();
        await Assert.That(picoLogAssembly.GetType("PicoLog.Abs.FlushExtensions")).IsNull();
    }
}
