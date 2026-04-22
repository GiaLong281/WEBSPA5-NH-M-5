using System.ComponentModel.DataAnnotations.Schema;
using SpaN5.Models;

namespace SpaN5.Models
{
    public class Service
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; }
        public string? Description { get; set; }

        public int Duration { get; set; } // phút

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? Image { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsPopular { get; set; } = false;
        public bool IsVip { get; set; } = false;
        public int MaxCapacity { get; set; } = 10;
        public string? VideoUrl { get; set; }

        public int CategoryId { get; set; }
        public ServiceCategory Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ServiceMaterial> ServiceMaterials { get; set; } = new List<ServiceMaterial>();
        public ICollection<ServiceStep> ServiceSteps { get; set; } = new List<ServiceStep>();
        public ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}