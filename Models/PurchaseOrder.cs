using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public enum POStatus { Pending, Approved, Received, Cancelled }

    public class PurchaseOrder
    {
        [Key]
        public int POId { get; set; }

        public string POCode { get; set; } = null!; // VD: PO-20240420-001

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDate { get; set; }

        public POStatus Status { get; set; } = POStatus.Pending;

        public decimal TotalAmount { get; set; }

        public string? Note { get; set; }

        public int? ApprovedById { get; set; } // Liên kết đến User/Staff Admin
        
        public ICollection<PurchaseOrderDetail> Details { get; set; } = new List<PurchaseOrderDetail>();
    }
}
