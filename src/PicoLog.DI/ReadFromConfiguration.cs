namespace PicoLog.DI;

public sealed class ReadFromConfiguration
{
    internal bool IncludeRegisteredSinks { get; private set; }

    public ReadFromConfiguration RegisteredSinks()
    {
        IncludeRegisteredSinks = true;
        return this;
    }

    internal ReadFromConfiguration CreateCopy()
    {
        var copy = new ReadFromConfiguration();
        copy.CopyFrom(this);

        return copy;
    }

    internal void CopyFrom(ReadFromConfiguration source)
    {
        if (source.IncludeRegisteredSinks)
            RegisteredSinks();
    }
}
