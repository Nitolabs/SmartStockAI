using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Application.Services;
using SmartStockAI.Domain.Interfaces;
using SmartStockAI.Infrastructure.AI;
using SmartStockAI.Infrastructure.Data;
using SmartStockAI.Infrastructure.Data.Seed;
using SmartStockAI.Infrastructure.Identity;
using SmartStockAI.Infrastructure.Repositories;
using SmartStockAI.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de datos ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = Environment.GetEnvironmentVariable("SMARTSTOCK_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "No se encontró una cadena de conexión. Configúrela con dotnet user-secrets, " +
        "la variable ConnectionStrings__DefaultConnection o SMARTSTOCK_CONNECTION_STRING.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------- Identity ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Revalida la sesión cada minuto: un usuario desactivado, o con rol cambiado, lo nota casi de inmediato
// (por defecto Identity lo revisa cada 30 minutos).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

// ---------- Repositorios y servicios de negocio ----------
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICompraService, CompraService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<INegocioService, NegocioService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IUserDirectory, UserDirectory>();
builder.Services.AddScoped<IRecomendacionService, RecomendacionService>();

// ---------- Correo (recuperación de contraseña) ----------
builder.Services.Configure<AppEmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();

// ---------- Inteligencia artificial (OpenAI / Azure OpenAI) ----------
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddHttpClient<IAIService, ChatCompletionsAIService>((sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<AIOptions>>().Value;
    http.Timeout = TimeSpan.FromSeconds(60);

    if (o.IsAzure && !string.IsNullOrWhiteSpace(o.Endpoint))
    {
        // El modelo es el nombre del deployment y va en la URL.
        http.BaseAddress = new Uri($"{o.Endpoint.TrimEnd('/')}/openai/deployments/{o.Model}/");
        if (!string.IsNullOrWhiteSpace(o.ApiKey))
            http.DefaultRequestHeaders.Add("api-key", o.ApiKey);
    }
    else
    {
        http.BaseAddress = new Uri("https://api.openai.com/v1/");
        if (!string.IsNullOrWhiteSpace(o.ApiKey))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", o.ApiKey);
    }
});

builder.Services.AddControllersWithViews();

// El carrito de Ventas/Compras y el widget de IA envían el token anti-falsificación por header (fetch/JSON).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

var app = builder.Build();

// ---------- Manejo de errores centralizado (sección 19) ----------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// En contenedor / Azure App Service el TLS lo termina el proxy; dentro solo hay HTTP.
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// ---------- Migraciones y seed inicial (roles, negocio demo, usuarios demo) ----------
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();
