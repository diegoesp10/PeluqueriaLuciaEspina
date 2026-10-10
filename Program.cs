using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;
using PeluqueriaLuciaEspina.Services;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLuciaEspina.Servicios;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using PeluqueriaLuciaEspina.RepoServicio;
using System.Text;

// Habilita páginas de código (CP850, etc.) necesarias para acentos/ñ al construir el ticket ESC/POS.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Contexto de BD
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositorios y servicios
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IServicioClienteService, ServicioClienteService>();
builder.Services.AddScoped<IServicioGenericoService, ServicioGenericoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IServicioGenericoRepository, ServicioGenericoRepository>();
builder.Services.AddScoped<ITicketPrinterService, TicketPrinterService>();




// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ServicioCliente}/{action=Index}/{id?}");

app.Run();
