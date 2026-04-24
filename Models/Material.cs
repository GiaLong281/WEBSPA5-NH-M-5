using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SpaN5.Models;

namespace SpaN5.Models
{
    public class Material
    {
        public int MaterialId { get; set; }

        public string MaterialName { get; set; }
        public string Unit { get; set; }

        public int CurrentStock { get; set; }
        public int MinStock { get; set; }

        public decimal PurchasePrice { get; set; }

        public string? Supplier { get; set; }
        public int? SupplierId { get; set; }
        public Supplier? SupplierEntity { get; set; }

        public bool IsActive { get; set; } = true;

        public byte[]? RowVersion { get; set; } // Chống tranh chấp dữ liệu

        public ICollection<ServiceMaterial> ServiceMaterials { get; set; } = new List<ServiceMaterial>();
        public ICollection<MaterialBatch> Batches { get; set; } = new List<MaterialBatch>();
        public ICollection<MaterialConversion> Conversions { get; set; } = new List<MaterialConversion>();
    }
}