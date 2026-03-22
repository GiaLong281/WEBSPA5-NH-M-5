namespace SpaN5.Models
{
    public class ServiceMaterial
    {
        public int Id { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public int MaterialId { get; set; }
        public Material Material { get; set; }

        public int Quantity { get; set; }
    }
}