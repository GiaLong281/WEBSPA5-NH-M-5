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

        public bool IsActive { get; set; } = true;

        public ICollection<ServiceMaterial> ServiceMaterials { get; set; } = new List<ServiceMaterial>();
    }
}