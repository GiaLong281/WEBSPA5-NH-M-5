using System.ComponentModel.DataAnnotations;

namespace SpaN5.Models
{
    public class BookingViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn dịch vụ")]
        public int ServiceId { get; set; }

        public string? ServiceName { get; set; }
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày")]
        public string Date { get; set; } 

        [Required(ErrorMessage = "Vui lòng chọn giờ")]
        public string Time { get; set; } 

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        public string? Email { get; set; }

        public string? Notes { get; set; }
    }
}
