using Microsoft.AspNetCore.Http;
using SpaN5.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SpaN5.Services
{
    public class AuditService
    {
        private readonly SpaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(SpaDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityName, string entityId, string? oldValues, string? newValues)
        {
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            var log = new AuditLog
            {
                UserName = userName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ip,
                CreatedAt = DateTime.Now
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}