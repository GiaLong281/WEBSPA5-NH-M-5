namespace SpaN5.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; // Lưu mật khẩu đã mã hóa
        public string Role { get; set; } = string.Empty; // Admin, Staff, Customer

        public string? FullName { get; set; }
        public string? Email { get; set; }

        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}