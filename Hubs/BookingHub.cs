using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SpaN5.Hubs
{
    public class BookingHub : Hub
    {
        // Nhận tín hiệu từ Client (nếu cần)
        public async Task SendBookingNotification(string bookingCode, string customerName)
        {
            await Clients.All.SendAsync("ReceiveNewBooking", bookingCode, customerName);
        }
    }
}
