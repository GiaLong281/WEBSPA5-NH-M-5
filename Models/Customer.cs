namespace SpaN5.Models
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Address { get; set; }

        public int? MaDiaChi { get; set; }
        public DiaChi? DiaChi { get; set; }

        public int LoyaltyPoints { get; set; } = 0;
        public decimal TotalSpent { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}