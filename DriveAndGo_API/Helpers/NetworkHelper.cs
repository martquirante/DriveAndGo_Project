using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace DriveAndGo_API.Helpers
{
    public static class NetworkHelper
    {
        /// <summary>
        /// Dynamically resolves the active server Base URL for QR codes, email verification links, and mobile clients.
        /// Automatically detects when switching between Ethernet, Wi-Fi, or different router IP assignments (.6, .11, etc.).
        /// </summary>
        public static string GetServerBaseUrl(IConfiguration? config = null, int port = 5233)
        {
            // 1. Explicit Tunnel / Domain Config (Use if public tunnel or custom domain; skip localhost for mobile/QR safety)
            string? configuredUrl = config?["ApiSettings:BaseUrl"] ?? config?["API_BASE_URL"];
            if (!string.IsNullOrWhiteSpace(configuredUrl) && 
                !configuredUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) && 
                !configuredUrl.Contains("127.0.0.1"))
            {
                return configuredUrl.TrimEnd('/');
            }

            // 2. Environment Variable (skip localhost)
            string? envUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envUrl) && 
                !envUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) && 
                !envUrl.Contains("127.0.0.1"))
            {
                return envUrl.TrimEnd('/');
            }

            // 3. Dynamic Local LAN IP Discovery via Active Routing Probe (Ethernet / Wi-Fi)
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint && 
                    !IPAddress.IsLoopback(endPoint.Address) &&
                    endPoint.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return $"http://{endPoint.Address}:{port}";
                }
            }
            catch { }

            // 4. Physical Network Interface Scan (Prioritizes Active Ethernet & Wi-Fi, filters out virtual/WSL adapters)
            try
            {
                var activePhysicalInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                 !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 2 : 
                                            ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
                    .FirstOrDefault();

                if (activePhysicalInterface != null)
                {
                    var ipProp = activePhysicalInterface.GetIPProperties();
                    var ipv4 = ipProp.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address));

                    if (ipv4 != null)
                    {
                        return $"http://{ipv4.Address}:{port}";
                    }
                }
            }
            catch { }

            // 5. Host DNS Fallback
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && 
                                                             !IPAddress.IsLoopback(a) &&
                                                             !a.ToString().StartsWith("192.168.48.")); // ignore WSL subnet
                if (ip != null)
                {
                    return $"http://{ip}:{port}";
                }
            }
            catch { }

            return $"http://192.168.1.6:{port}";
        }
    }
}
