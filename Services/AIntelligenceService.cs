using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaN5.Services
{
    public interface IAIntelligenceService
    {
        Task<List<StaffKPI>> GetStaffRadarDataAsync();
        Task<int[][]> GetBookingHeatmapAsync();
        Task<List<StrategicRecommendation>> GetStrategicRecommendationsAsync();
    }

    public class StaffKPI
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public double Skill { get; set; } // Chuyên môn
        public double Speed { get; set; } // Tốc độ
        public double Retention { get; set; } // Tỷ lệ khách quay lại
        public double Review { get; set; } // Điểm đánh giá
        public double Upsell { get; set; } // Bán thêm dịch vụ
        public double CrossSell { get; set; } // Bán thêm sản phẩm
        public double Compliance { get; set; } // Tuân thủ
    }

    public class StrategicRecommendation
    {
        public string Type { get; set; } = string.Empty; // Churn, Peak, FlashSale
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty; // VIP, New, Lapsed
    }

    public class AIntelligenceService : IAIntelligenceService
    {
        private readonly SpaDbContext _context;

        public AIntelligenceService(SpaDbContext context)
        {
            _context = context;
        }

        public async Task<List<StaffKPI>> GetStaffRadarDataAsync()
        {
            var staffs = await _context.Staffs.Where(s => s.Status == "active").ToListAsync();
            var now = DateTime.Now;
            var thirtyDaysAgo = now.AddDays(-30);

            var result = new List<StaffKPI>();

            foreach (var staff in staffs)
            {
                var bookings = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Include(bd => bd.Service)
                    .Where(bd => bd.StaffId == staff.StaffId && bd.Booking.BookingDate >= thirtyDaysAgo)
                    .ToListAsync();

                var reviews = await _context.Reviews
                    .Where(r => r.ServiceId != null && bookings.Select(b => b.ServiceId).Contains(r.ServiceId))
                    .ToListAsync();

                var attendance = await _context.Attendances
                    .Where(a => a.StaffId == staff.StaffId && a.Date >= thirtyDaysAgo)
                    .ToListAsync();

                // 1. Skill (Chuyên môn): Số lượng dịch vụ đúng sở trường / tổng dịch vụ
                double skillScore = staff.SpecializationId.HasValue 
                    ? (bookings.Count(b => b.ServiceId == staff.SpecializationId) * 10.0 / (bookings.Count > 0 ? bookings.Count : 1))
                    : 7.0;

                // 2. Review (Đánh giá): Trung bình rating * 2
                double reviewScore = reviews.Any() ? reviews.Average(r => r.Rating) * 2.0 : 8.5;

                // 3. Compliance (Tuân thủ): Check-in đúng giờ. Giả sử 8h sáng.
                double complianceScore = 10.0;
                if (attendance.Any())
                {
                    var lateCount = attendance.Count(a => a.CheckInTime.HasValue && a.CheckInTime.Value.Hour >= 8 && a.CheckInTime.Value.Minute > 15);
                    complianceScore = 10.0 - (lateCount * 1.5);
                }

                // 4. Retention (Tỷ lệ quay lại): Số khách phục vụ > 1 lần trong 30 ngày
                var customerGroups = bookings.GroupBy(b => b.Booking.CustomerId);
                double retentionScore = (customerGroups.Count(g => g.Count() > 1) * 10.0 / (customerGroups.Count() > 0 ? customerGroups.Count() : 1)) + 5.0;

                // 5. Upsell & Cross-sell (Giả lập dựa trên doanh thu vượt mức)
                double upsellScore = bookings.Count(b => b.PriceAtTime > b.Service.Price) * 2.0 + 6.0;
                double crossSellScore = bookings.Sum(b => (double)b.PriceAtTime) > 5000000 ? 9.0 : 6.5;

                result.Add(new StaffKPI
                {
                    StaffId = staff.StaffId,
                    StaffName = staff.FullName,
                    Skill = Math.Clamp(skillScore, 3, 10),
                    Review = Math.Clamp(reviewScore, 3, 10),
                    Compliance = Math.Clamp(complianceScore, 3, 10),
                    Retention = Math.Clamp(retentionScore, 3, 10),
                    Speed = 8.5, // Giả lập tốc độ ổn định
                    Upsell = Math.Clamp(upsellScore, 3, 10),
                    CrossSell = Math.Clamp(crossSellScore, 3, 10)
                });
            }

            return result;
        }

        public async Task<int[][]> GetBookingHeatmapAsync()
        {
            // Trả về ma trận 7 ngày x 24 giờ
            var matrix = new int[7][];
            for (int i = 0; i < 7; i++) matrix[i] = new int[24];

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var bookings = await _context.Bookings
                .Where(b => b.BookingDate >= thirtyDaysAgo)
                .Select(b => new { b.BookingDate.DayOfWeek, b.StartTime.Hour })
                .ToListAsync();

            foreach (var b in bookings)
            {
                matrix[(int)b.DayOfWeek][b.Hour]++;
            }

            return matrix;
        }

        public async Task<List<StrategicRecommendation>> GetStrategicRecommendationsAsync()
        {
            var recommendations = new List<StrategicRecommendation>();
            var now = DateTime.Now;

            // Logic 1: Flash Sale dựa trên Heatmap
            var matrix = await GetBookingHeatmapAsync();
            // Tìm giờ vắng nhất trong các ngày tới (giả sử chiều nay)
            int currentDay = (int)now.DayOfWeek;
            int coldHour = -1;
            for (int h = 13; h < 17; h++) // Khung giờ chiều
            {
                if (matrix[currentDay][h] < 2) { coldHour = h; break; }
            }

            if (coldHour != -1)
            {
                recommendations.Add(new StrategicRecommendation
                {
                    Type = "FlashSale",
                    Title = "Kích cầu giờ thấp điểm",
                    Icon = "fa-bolt",
                    Color = "#f59e0b",
                    Message = $"Phát hiện khung giờ <b>{coldHour}h - {coldHour+1}h</b> chiều nay đang trống 80% công suất. Đề xuất chạy Flash Sale 'Happy Hour' giảm 25% cho khách mới.",
                    Action = "Triển khai chiến dịch",
                    TargetAudience = "New"
                });
            }

            // Logic 2: Churn Risk VIP
            var lapsedDate = now.AddDays(-30);
            var vipRisk = await _context.Customers
                .Where(c => c.TotalSpent > 2000000 && !c.Bookings.Any(b => b.BookingDate >= lapsedDate))
                .Take(1)
                .ToListAsync();

            if (vipRisk.Any())
            {
                recommendations.Add(new StrategicRecommendation
                {
                    Type = "Churn",
                    Title = "Rủi ro khách VIP rơi bỏ",
                    Icon = "fa-user-minus",
                    Color = "#ef4444",
                    Message = $"Khách VIP <b>{vipRisk[0].FullName}</b> đã 35 ngày chưa quay lại. AI đề xuất tặng gói <i>'Upgrade phòng VIP miễn phí'</i> để tái kích hoạt.",
                    Action = "Gửi ưu đãi riêng",
                    TargetAudience = "VIP"
                });
            }

            return recommendations;
        }
    }
}
