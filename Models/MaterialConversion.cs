using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class MaterialConversion
    {
        [Key]
        public int Id { get; set; }

        public int MaterialId { get; set; }
        public Material Material { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string FromUnit { get; set; } = null!; // VD: Tuýp, Chai, ml

        [Required]
        public string ToUnit { get; set; } = "gram";

        [Required]
        public double Ratio { get; set; } // Tỷ lệ quy đổi (1 Tuýp = 200 gram)

        public string? Note { get; set; }
    }
}
