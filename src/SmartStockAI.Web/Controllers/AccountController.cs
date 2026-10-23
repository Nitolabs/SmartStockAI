using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Interfaces;
using SmartStockAI.Infrastructure.Identity;
using SmartStockAI.Web.ViewModels;

namespace SmartStockAI.Web.Controllers;

/// <summary>
/// Módulo de autenticación (sección 6.1): login, logout, registro, recuperación y cambio de contraseña.
/// Cada acción declara explícitamente si es anónima: un [AllowAnonymous] a nivel de clase anularía
/// los [Authorize] de las acciones (por eso ChangePassword quedaba abierta).
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<Business> _businesses;
    private readonly IAppEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IRepository<Business> businesses,
        IAppEmailSender email,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _businesses = businesses;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Usuario) ?? await _userManager.FindByNameAsync(model.Usuario);
        if (user is null || !user.Activo)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Contrasena, model.RecordarSesion, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Cuenta bloqueada temporalmente por múltiples intentos fallidos.");
            return View(model);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        await EmitirClaimsAsync(user);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var existente = await _userManager.FindByEmailAsync(model.Email);
        if (existente is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con ese correo.");
            return View(model);
        }

        // Cada registro crea su propio Business (tenant) — aislamiento desde el primer momento.
        var business = new Business { Nombre = model.NombreNegocio.Trim(), Rubro = model.Rubro, Email = model.Email };
        await _businesses.AddAsync(business);
        await _businesses.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            NombreCompleto = model.NombreCompleto.Trim(),
            BusinessId = business.Id
        };

        var result = await _userManager.CreateAsync(user, model.Contrasena);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Administrador);
        await _signInManager.SignInAsync(user, isPersistent: false);
        await EmitirClaimsAsync(user);

        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && user.Activo)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var tokenCodificado = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token = tokenCodificado }, Request.Scheme)!;

            var cuerpo =
                $"<p>Hola {WebUtility.HtmlEncode(user.NombreCompleto)},</p>" +
                "<p>Recibimos una solicitud para restablecer tu contraseña de SmartStock AI.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(resetUrl)}\">Restablecer mi contraseña</a></p>" +
                "<p>Si no fuiste tú, ignora este mensaje.</p>";

            var enviado = await _email.SendAsync(user.Email!, "Recuperación de contraseña - SmartStock AI", cuerpo);
            if (!enviado)
                _logger.LogWarning("El correo de recuperación para {Email} no se envió (SMTP no configurado o con error).", user.Email);

            // Solo para demostraciones sin SMTP: muestra el enlace en pantalla. Desactivado por defecto,
            // porque permitiría a cualquiera restablecer la contraseña de una cuenta conociendo su correo.
            if (_configuration.GetValue<bool>("Auth:MostrarEnlaceRecuperacion"))
                TempData["EnlaceRecuperacion"] = resetUrl;
        }

        // Misma respuesta exista o no el correo, para no revelar qué cuentas existen.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (email is null || token is null) return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null) return RedirectToAction(nameof(ResetPasswordConfirmation));

        string tokenDecodificado;
        try
        {
            tokenDecodificado = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "El enlace de recuperación no es válido o ya expiró.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, tokenDecodificado, model.Contrasena);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await _userManager.ChangePasswordAsync(user, model.ContrasenaActual, model.NuevaContrasena);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        ViewBag.Mensaje = "Tu contraseña se actualizó correctamente.";
        return View(new ChangePasswordViewModel());
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Guarda BusinessId y NombreCompleto como claims de la sesión para que BaseController los lea
    /// sin consultar la base de datos en cada request.
    /// </summary>
    private async Task EmitirClaimsAsync(ApplicationUser user)
    {
        var claimsActuales = await _userManager.GetClaimsAsync(user);
        var aRemover = claimsActuales.Where(c => c.Type is "BusinessId" or "NombreCompleto").ToList();
        if (aRemover.Count > 0)
            await _userManager.RemoveClaimsAsync(user, aRemover);

        await _userManager.AddClaimsAsync(user, new[]
        {
            new Claim("BusinessId", user.BusinessId.ToString()),
            new Claim("NombreCompleto", user.NombreCompleto)
        });

        await _signInManager.RefreshSignInAsync(user);
    }
}
