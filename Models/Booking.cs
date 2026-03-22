using System.ComponentModel.DataAnnotations.Schema;

namespace SpaN5.Models
{
    public class Booking
    {
        public int BookingId { get; set; }

        public string BookingCode { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        public DateTime BookingDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public decimal TotalAmount { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}