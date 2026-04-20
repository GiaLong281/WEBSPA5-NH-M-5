using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaN5.Models
{
    public class MaterialConsumption
    {
        [Key]
        public int Id { get; set; }

        public int BookingDetailId { get; set; }
        [ForeignKey("BookingDetailId")]
        public BookingDetail BookingDetail { get; set; } = null!;

        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public Material Material { get; set; } = null!;

        public int StaffId { get; set; }
        [ForeignKey("StaffId")]
        public Staff Staff { get; set; } = null!;

        public double StandardQuantity { get; set; } // Lượng định mức dự kiến (gram)
        public double ActualQuantity { get; set; }   // Lượng thực tế KTV nhập (gram)

        public int? BatchId { get; set; } // Lô hàng đã trừ (FEFO)
        public MaterialBatch? Batch { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
