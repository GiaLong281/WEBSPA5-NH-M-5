using System;
using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }
        
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
        
        public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? AdminNote { get; set; }
    }

    public enum LeaveRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }
}
