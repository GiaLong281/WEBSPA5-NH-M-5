using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models.ViewModels
{
    public class BookingViewModel
    {
        public int? ServiceId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn dịch vụ")]
        public int SelectedServiceId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chi nhánh")]
        public int BranchId { get; set; }

        public int? StaffId { get; set; }  // null = tự động phân công

        [Required(ErrorMessage = "Vui lòng chọn ngày")]
        [DataType(DataType.Date)]
        public DateTime BookingDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        public string? Notes { get; set; }

        // Thông tin hiển thị
        public string? ServiceName { get; set; }
        public int Duration { get; set; }
        public decimal Price { get; set; }
        public DateTime EndTime { get; set; }

        // Tự động phân công
        public bool AutoAssignStaff { get; set; } = true;
    }
}