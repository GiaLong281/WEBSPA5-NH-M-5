using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class Quan
    {
        public int MaQuan { get; set; }
        [Required]
        public string TenQuan { get; set; } = string.Empty;

        public int MaThanhPho { get; set; }
        public ThanhPho ThanhPho { get; set; } = null!;

        public ICollection<DiaChi> DiaChis { get; set; } = new List<DiaChi>();
    }
}