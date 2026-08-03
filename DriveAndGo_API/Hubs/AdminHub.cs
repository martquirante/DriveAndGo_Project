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

        /// <summary>
        /// Broadcasts a message delivery/seen status change to all connected clients.
        /// Clients filter by senderId to update only their own outgoing bubbles.
        /// </summary>
        /// <param name="messageId">The DB message_id that changed status.</param>
        /// <param name="status">New status: "sent" | "delivered" | "seen"</param>
        /// <param name="senderId">Who sent the original message (used by client to filter).</param>
        /// <param name="receiverId">Who received the original message.</param>
        public async Task BroadcastMessageStatus(string messageId, string status, string senderId, string receiverId)
        {
            await Clients.All.SendAsync("MessageStatusChanged", messageId, status, senderId, receiverId);
        }

        public async Task BroadcastMessageEdited(string messageId, string newText, string editHistoryJson, string receiverId)
        {
            await Clients.All.SendAsync("MessageEdited", messageId, newText, editHistoryJson, receiverId);
        }

        public async Task BroadcastMessageUnsent(string messageId, string receiverId)
        {
            await Clients.All.SendAsync("MessageUnsent", messageId, receiverId);
        }

        public async Task BroadcastMessageReaction(string messageId, string reactionsJson, string receiverId)
        {
            await Clients.All.SendAsync("MessageReactionChanged", messageId, reactionsJson, receiverId);
        }
    }
}
