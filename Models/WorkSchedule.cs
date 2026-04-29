using System;
using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class WorkSchedule
    {
        [Key]
        public int Id { get; set; }
        
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        
        public DateTime Date { get; set; }
        
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }
        
        public WorkStatus Status { get; set; } = WorkStatus.Working;
        public string? Note { get; set; }
    }

    public enum WorkStatus
    {
        Working,
        Leave,      // Nghỉ phép
        Sick,       // Nghỉ ốm
        Off,        // Nghỉ tuần
        Overtime    // Tăng ca
    }
}
