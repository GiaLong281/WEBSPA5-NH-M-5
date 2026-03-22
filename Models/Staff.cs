namespace SpaN5.Models
{
    public class Staff
    {
        public int StaffId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? Position { get; set; }
        public int? BranchId { get; set; }

        public string? Avatar { get; set; }
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Branch? Branch { get; set; }
    }
}