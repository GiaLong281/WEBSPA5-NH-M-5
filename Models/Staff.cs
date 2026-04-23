using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class Staff
    {
        public int StaffId { get; set; }
        public string FullName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Số điện thoại phải đúng 10 số")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại chỉ được chứa 10 chữ số")]
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? Position { get; set; }
        public int? BranchId { get; set; }

        public string? Avatar { get; set; }
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public int? SpecializationId { get; set; }
        public Service? Specialization { get; set; }

        public Branch? Branch { get; set; }
    }
}