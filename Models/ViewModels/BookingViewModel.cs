using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SpaN5.Models.ViewModels
{
    public class BookingViewModel
    {
        public int? ServiceId { get; set; }

        public int SelectedServiceId { get; set; }

        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        public int BranchId { get; set; }

        public int? StaffId { get; set; }
        public bool AutoAssignStaff { get; set; } = true;
        public int? RoomNumber { get; set; }

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

        public List<ServiceInfo> ServicesInfo { get; set; } = new();
        public int TotalDuration => SelectedServiceIds.Count > 0 ?
            ServicesInfo?.Where(s => SelectedServiceIds.Contains(s.ServiceId)).Sum(s => s.Duration) ?? 0 : 0;
        public decimal TotalPrice => SelectedServiceIds.Count > 0 ?
            ServicesInfo?.Where(s => SelectedServiceIds.Contains(s.ServiceId)).Sum(s => s.Price) ?? 0 : 0;

        public DateTime EndTime { get; set; }

        // Thông tin khách hàng
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; } = string.Empty;
    }

    public class ServiceInfo
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Duration { get; set; }
        public decimal Price { get; set; }
    }
}