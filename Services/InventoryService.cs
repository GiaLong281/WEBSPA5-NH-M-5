using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Services
{
    public interface IInventoryService
    {
        Task<bool> DeductStockAsync(int bookingDetailId, List<(int MaterialId, double ActualQuantity)> consumptions);
        Task<double> ConvertToGramsAsync(int materialId, string fromUnit, double amount);
        Task<int> PredictRemainingCapacityAsync(int materialId);
        Task<bool> ReceivePOAsync(int poId, DateTime? expiryDate = null);
    }

    public class InventoryService : IInventoryService
    {
        private readonly SpaDbContext _context;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(SpaDbContext context, ILogger<InventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> DeductStockAsync(int bookingDetailId, List<(int MaterialId, double ActualQuantity)> consumptions)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var detail = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .FirstOrDefaultAsync(bd => bd.DetailId == bookingDetailId);

                if (detail == null) return false;

                foreach (var item in consumptions)
                {
                    double remainingToDeduct = item.ActualQuantity;

                    // 1. Tìm các lô hàng còn tồn kho, ưu tiên hạn dùng gần nhất (FEFO)
                    var activeBatches = await _context.MaterialBatches
                        .Where(b => b.MaterialId == item.MaterialId && b.CurrentQuantity > 0 && b.IsActive)
                        .OrderBy(b => b.ExpiryDate)
                        .ToListAsync();

                    foreach (var batch in activeBatches)
                    {
                        if (remainingToDeduct <= 0) break;

                        int deductAmount = (int)Math.Min(batch.CurrentQuantity, remainingToDeduct);
                        batch.CurrentQuantity -= deductAmount;
                        remainingToDeduct -= deductAmount;

                        // Lưu vết tiêu thụ
                        var consumptionLog = new MaterialConsumption
                        {
                            BookingDetailId = bookingDetailId,
                            MaterialId = item.MaterialId,
                            StaffId = detail.StaffId ?? 0,
                            ActualQuantity = deductAmount,
                            BatchId = batch.BatchId,
                            CreatedAt = DateTime.Now
                        };
                        _context.MaterialConsumptions.Add(consumptionLog);
                    }

                    // Nếu trừ hết các lô mà vẫn còn dư -> Trừ vào kho tổng (nếu cho phép âm) hoặc báo lỗi
                    var material = await _context.Materials.FindAsync(item.MaterialId);
                    if (material != null)
                    {
                        material.CurrentStock -= (int)item.ActualQuantity;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Tranh chấp dữ liệu khi trừ kho cho BookingDetail {Id}", bookingDetailId);
                return false;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi trừ kho cho BookingDetail {Id}", bookingDetailId);
                return false;
            }
        }

        public async Task<double> ConvertToGramsAsync(int materialId, string fromUnit, double amount)
        {
            if (fromUnit.ToLower() == "gram") return amount;

            var conversion = await _context.MaterialConversions
                .FirstOrDefaultAsync(c => c.MaterialId == materialId && c.FromUnit.ToLower() == fromUnit.ToLower());

            if (conversion == null) return amount; // Mặc định 1:1 nếu không có cấu hình

            return amount * conversion.Ratio;
        }

        public async Task<int> PredictRemainingCapacityAsync(int materialId)
        {
            var material = await _context.Materials
                .Include(m => m.ServiceMaterials)
                .FirstOrDefaultAsync(m => m.MaterialId == materialId);

            if (material == null || material.CurrentStock <= 0) return 0;

            // Lấy định mức trung bình từ các dịch vụ sử dụng nguyên liệu này
            var avgConsumption = material.ServiceMaterials.Any() 
                ? material.ServiceMaterials.Average(sm => sm.Quantity) 
                : 0;

            if (avgConsumption <= 0) return 999; // Không rõ định mức -> coi như còn nhiều

            return (int)(material.CurrentStock / avgConsumption);
        }

        public async Task<bool> ReceivePOAsync(int poId, DateTime? expiryDate = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var po = await _context.PurchaseOrders
                    .Include(p => p.Details)
                    .FirstOrDefaultAsync(p => p.POId == poId);

                if (po == null || po.Status != POStatus.Approved) return false;

                foreach (var detail in po.Details)
                {
                    // Tăng kho tổng
                    var material = await _context.Materials.FindAsync(detail.MaterialId);
                    if (material != null) material.CurrentStock += detail.Quantity;

                    // Tạo lô hàng mới (Mặc định HSD 1 năm nếu không nhập)
                    var batch = new MaterialBatch
                    {
                        MaterialId = detail.MaterialId,
                        BatchCode = $"PO-{po.POCode}-{detail.MaterialId}",
                        ExpiryDate = expiryDate ?? DateTime.Now.AddYears(1),
                        OriginalQuantity = detail.Quantity,
                        CurrentQuantity = detail.Quantity,
                        UnitCost = detail.UnitPrice,
                        ReceivedDate = DateTime.Now
                    };
                    _context.MaterialBatches.Add(batch);
                }

                po.Status = POStatus.Received;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi nhập kho đơn hàng PO {Id}", poId);
                return false;
            }
        }
    }
}
