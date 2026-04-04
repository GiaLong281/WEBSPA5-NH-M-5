using System;

namespace SpaN5.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        public DateTime Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? Note { get; set; }
    }
}
