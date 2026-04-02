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

        public int? StaffId { get; set; }
        public bool AutoAssignStaff { get; set; } = true;

        [Required(ErrorMessage = "Vui lòng chọn ngày")]
        [DataType(DataType.Date)]
        public DateTime BookingDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ")]
        public TimeSpan StartTime { get; set; }

        public string? Notes { get; set; }

        // Thông tin hiển thị
        public string? ServiceName { get; set; }
        public int Duration { get; set; }
        public decimal Price { get; set; }
        public DateTime EndTime { get; set; }

        // Thông tin khách hàng
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; } = string.Empty;
    }
}