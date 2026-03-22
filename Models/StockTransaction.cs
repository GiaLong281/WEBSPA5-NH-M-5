using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaN5.Models
{
    public class StockTransaction
    {
        public int TransactionId { get; set; }

        public int MaterialId { get; set; }
        public Material Material { get; set; }

        public string Type { get; set; } // import, export
        public int Quantity { get; set; }

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}