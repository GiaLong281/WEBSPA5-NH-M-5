namespace SpaN5.Models
{
    public class DiaChi
    {
        public int MaDiaChi { get; set; }  // Primary key
        public string? SoNha { get; set; }
        public string? Duong { get; set; }
        public int MaQuan { get; set; }
        public Quan Quan { get; set; } = null!;

        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    }
}