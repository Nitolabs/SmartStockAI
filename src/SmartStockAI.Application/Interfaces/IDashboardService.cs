using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(int businessId);
}
