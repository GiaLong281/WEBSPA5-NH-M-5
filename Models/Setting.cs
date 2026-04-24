namespace SpaN5.Models
{
    public class Setting
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}