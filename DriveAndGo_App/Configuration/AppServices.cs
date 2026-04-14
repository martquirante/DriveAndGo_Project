namespace DriveAndGo_App.Configuration;

public static class AppServices
{
    public static IServiceProvider Services { get; private set; } = default!;

    public static void Configure(IServiceProvider services)
    {
        Services = services;
    }

    public static T GetRequiredService<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }
}
