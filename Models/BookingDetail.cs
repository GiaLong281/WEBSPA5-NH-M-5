using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class BookingDetail
    {
        [Key] // thêm dòng này cho chắc
        public int DetailId { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }

        public decimal PriceAtTime { get; set; }
        public int? RoomNumber { get; set; }
    }
}