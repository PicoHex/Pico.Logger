namespace PicoLog.Sample;

public static class ServiceRegistrations
{
    public static ISvcContainer ConfigureServices(this ISvcContainer container)
    {
        container.RegisterScoped<IService, Service>();
        return container;
    }
}
