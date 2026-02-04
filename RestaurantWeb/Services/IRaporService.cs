using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IRaporService
    {
        OperationResult<DashboardVm> GetDashboard(DateTime? baslangic, DateTime? bitis, string? mode); // ★
        OperationResult<ReportExcelDto> ExportDashboardExcel(DateTime? baslangic, DateTime? bitis, string? mode); // ★
    }
}
