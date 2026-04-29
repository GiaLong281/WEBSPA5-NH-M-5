using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class ThanhPho
    {
        public int MaThanhPho { get; set; }
        [Required]
        public string TenThanhPho { get; set; } = string.Empty;

        public ICollection<Quan> Quans { get; set; } = new List<Quan>();
    }
}