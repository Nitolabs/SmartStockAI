using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

public class DashboardController : BaseController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(UserManager<ApplicationUser> userManager, IDashboardService dashboardService)
        : base(userManager)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetDashboardAsync(CurrentBusinessId);
        return View(dashboard);
    }
}
