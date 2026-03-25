namespace SpaN5.Models
{
    namespace SpaN5.Models
    {
        public class ServiceCategory
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public ICollection<Service> Services { get; set; } = new List<Service>();
        }
    }
}
