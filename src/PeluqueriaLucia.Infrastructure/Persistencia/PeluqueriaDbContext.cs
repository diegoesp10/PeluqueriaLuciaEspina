using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Contexto de la base de datos. Los nombres de tablas y columnas son los de la primera versión de la aplicación
/// (ServiciosClientes, ServiciosGenericos…) y no se cambian: así la base de datos de producción se actualiza solo con
/// migraciones que conservan el historial. Las tablas públicas antiguas se retiran después de unificar sus datos.
/// También guarda las claves con las que se cifran las cookies de sesión, para que sobrevivan a reinicios de IIS.
/// </summary>
public sealed class PeluqueriaDbContext(DbContextOptions<PeluqueriaDbContext> options) : DbContext(options), IUnidadDeTrabajo, IDataProtectionKeyContext
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<PeluqueriaLucia.Domain.Agenda.ReservaCita> ReservasCitas => Set<PeluqueriaLucia.Domain.Agenda.ReservaCita>();
    public DbSet<PeluqueriaLucia.Domain.Agenda.TramoAgenda> TramosAgenda => Set<PeluqueriaLucia.Domain.Agenda.TramoAgenda>();
    public DbSet<PeluqueriaLucia.Domain.Agenda.CierreAgenda> CierresAgenda => Set<PeluqueriaLucia.Domain.Agenda.CierreAgenda>();
    public DbSet<PeluqueriaLucia.Domain.Agenda.MensajeCita> MensajesCitas => Set<PeluqueriaLucia.Domain.Agenda.MensajeCita>();

    public DbSet<Servicio> Servicios => Set<Servicio>();

    public DbSet<Visita> Visitas => Set<Visita>();

    public DbSet<LineaVisita> LineasVisita => Set<LineaVisita>();

    public DbSet<LineaProductoVisita> ProductosVisitas => Set<LineaProductoVisita>();

    public DbSet<Producto> Productos => Set<Producto>();

    public DbSet<FotoProducto> FotosProductos => Set<FotoProducto>();

    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();

    public DbSet<Administrador> Administradores => Set<Administrador>();

    public DbSet<SesionAdministrador> SesionesAdministrador => Set<SesionAdministrador>();

    public DbSet<IntentoAcceso> IntentosAcceso => Set<IntentoAcceso>();
    public DbSet<DesafioMfa> DesafiosMfa => Set<DesafioMfa>();
    public DbSet<RecuperacionMfa> RecuperacionesMfa => Set<RecuperacionMfa>();
    public DbSet<CategoriaTratamiento> CategoriasPublicas => Set<CategoriaTratamiento>();
    public DbSet<PermisoResena> PermisosResenas => Set<PermisoResena>();
    public DbSet<Resena> Resenas => Set<Resena>();
    public DbSet<TelefonoContacto> TelefonosContacto => Set<TelefonoContacto>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public Task GuardarCambiosAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeluqueriaDbContext).Assembly);
}
