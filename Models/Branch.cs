using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class Branch
    {
        public int BranchId { get; set; }

        [Required]
        public string BranchName { get; set; } = string.Empty;

        public string? BranchCode { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;

        public string? District { get; set; }

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? GoogleMapLink { get; set; }
        public string? FacebookLink { get; set; }

        public TimeSpan OpeningTime { get; set; }
        public TimeSpan ClosingTime { get; set; }

        public string? Workday { get; set; }

        public string? Image { get; set; }
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Quan hệ
        public ICollection<Staff> Staffs { get; set; } = new List<Staff>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}