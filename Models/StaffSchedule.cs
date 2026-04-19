using System;
using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class StaffSchedule
    {
        [Key]
        public int Id { get; set; }
        
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        
        public DayOfWeek DayOfWeek { get; set; } // 0=Sunday, 1=Monday...
        
        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }
        
        public bool IsOff { get; set; } = false;
    }
}
