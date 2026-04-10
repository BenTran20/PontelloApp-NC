using Microsoft.AspNetCore.SignalR;

namespace PontelloApp.Models
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(string userId, string title, string message, string link = null)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", new { title, message, link });
        }
    }
}
