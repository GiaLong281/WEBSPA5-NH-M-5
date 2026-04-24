using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaN5.Models
{
    public class PurchaseOrderDetail
    {
        [Key]
        public int Id { get; set; }

        public int POId { get; set; }
        [ForeignKey("POId")]
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public Material Material { get; set; } = null!;

        public int Quantity { get; set; } // Số lượng đặt (grams)
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Trường quy đổi nếu cần (VD: 5 tuýp -> 1000g)
        public string? Note { get; set; }
    }
}
