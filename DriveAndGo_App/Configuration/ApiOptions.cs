namespace DriveAndGo_App.Configuration;

public static class ApiOptions
{
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5233/api/"
            : "http://localhost:5233/api/";

    public static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }
}
