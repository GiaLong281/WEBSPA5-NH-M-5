using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class MaterialBatch
    {
        [Key]
        public int BatchId { get; set; }

        public int MaterialId { get; set; }
        public Material Material { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string BatchCode { get; set; } = null!; // Số lô hàng

        public DateTime? ManufactureDate { get; set; }
        
        [Required]
        public DateTime ExpiryDate { get; set; } // Ngày hết hạn (Quan trọng cho FEFO)

        public int OriginalQuantity { get; set; } // Số lượng nhập ban đầu (grams)
        public int CurrentQuantity { get; set; } // Số lượng còn lại trong lô này (grams)

        public decimal UnitCost { get; set; } // Giá nhập của lô này

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
