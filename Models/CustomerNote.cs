using System;

namespace SpaN5.Models
{
    public class CustomerNote
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int StaffId { get; set; }
        public Staff? Staff { get; set; }
        
        public string Note { get; set; } = string.Empty;
        
        // --- New Structured Assessment Fields ---
        public string? SkinType { get; set; } // e.g., Dry, Oily, Acne, Sensitive
        public string? ImprovementStatus { get; set; } // Progress description
        public string? RecommendedService { get; set; } // Advice for next time
        public int? NextServiceAfterDays { get; set; } // Recommendation interval
        public string? InternalNote { get; set; } // Secret notes for staff only
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
