using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartStockAI.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        // Nunca se expone el stack trace, cadenas de conexión ni claves al usuario final (sección 19).
        return View("Error", requestId);
    }
}
