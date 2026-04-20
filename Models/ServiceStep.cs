using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class ServiceStep
    {
        [Key]
        public int Id { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string StepName { get; set; } = null!; // VD: Rửa mặt, Massage, Đắp mặt nạ

        public int Order { get; set; } // Thứ tự thực hiện

        public int? Duration { get; set; } // Thời gian dự kiến của bước này (phút)

        // Navigation
        public ICollection<ServiceMaterial> Materials { get; set; } = new List<ServiceMaterial>();
    }
}
