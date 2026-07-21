using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DriveAndGo_API.Hubs
{
    public class AdminHub : Hub
    {
        public async Task BroadcastDashboardUpdate()
        {
            await Clients.All.SendAsync("ReceiveDashboardUpdate");
        }

        public async Task BroadcastVehicleUpdate()
        {
            await Clients.All.SendAsync("ReceiveVehicleUpdate");
        }

        public async Task BroadcastAccountsUpdate()
        {
            await Clients.All.SendAsync("ReceiveAccountsUpdate");
        }
    }
}
