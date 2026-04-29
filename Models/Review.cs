using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaN5.Models
{
    public class Review
    {
        public int ReviewId { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsApproved { get; set; } = true; // nếu cần duyệt trước khi hiển thị
    }
}