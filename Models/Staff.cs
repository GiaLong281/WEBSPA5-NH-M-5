using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class Staff
    {
        public int StaffId { get; set; }
        public string? FullName { get; set; }
        
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? Position { get; set; }
        public int? BranchId { get; set; }

        public string? Avatar { get; set; }
        public string? Status { get; set; }

        public DateTime? CreatedAt { get; set; }
        
        public int? SpecializationId { get; set; }
        public Service? Specialization { get; set; }

        public Branch? Branch { get; set; }
    }
}