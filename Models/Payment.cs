using SpaN5.Models;

namespace SpaN5.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; }

        public decimal Amount { get; set; }

        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}