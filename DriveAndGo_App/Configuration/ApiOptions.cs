namespace DriveAndGo_App.Configuration;

public static class ApiOptions
{
    // Local development network LAN address (Same Wi-Fi Router / Modem)
    public const string ServerLanIp = "192.168.1.6";
    public const int ServerPort = 5233;

    public static string BaseUrl
    {
        get
        {
            var envUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envUrl)) return envUrl.TrimEnd('/') + "/api/";

            // If running on a physical Android or iOS device over local Wi-Fi router
            if (DeviceInfo.DeviceType == DeviceType.Physical)
            {
                return $"http://{ServerLanIp}:{ServerPort}/api/";
            }

            // Android Emulator loopback alias
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                return $"http://10.0.2.2:{ServerPort}/api/";
            }

            // Desktop Windows / Mac / iOS Simulator
            return $"http://localhost:{ServerPort}/api/";
        }
    }

    public static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }
}
