using System.ComponentModel.DataAnnotations.Schema;
using SpaN5.Models;

namespace SpaN5.Models
{
    public class Service
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }

        public int Duration { get; set; } // phút

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? Image { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsPopular { get; set; } = false;

        public int CategoryId { get; set; }
        public ServiceCategory Category { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ServiceMaterial> ServiceMaterials { get; set; } = new List<ServiceMaterial>();
        public ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
    }
}