using System;

namespace SpaN5.Models
{
    public class CustomerNote
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
