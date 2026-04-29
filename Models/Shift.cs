using System;
using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class Shift
    {
        [Key]
        public int ShiftId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string ShiftName { get; set; } = string.Empty; // Sáng, Chiều, Tối, Full
        
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
