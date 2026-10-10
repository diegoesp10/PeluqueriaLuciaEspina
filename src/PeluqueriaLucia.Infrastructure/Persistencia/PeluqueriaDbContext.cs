using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Contexto de la base de datos. Los nombres de tablas y columnas son los de la primera versión de la aplicación
/// (ServiciosClientes, ServiciosGenericos…) y no se cambian: así la base de datos de producción se actualiza solo con
/// migraciones que añaden, nunca con renombrados ni borrados.
/// </summary>
public sealed class PeluqueriaDbContext(DbContextOptions<PeluqueriaDbContext> options) : DbContext(options), IUnidadDeTrabajo
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Servicio> Servicios => Set<Servicio>();

    public DbSet<Visita> Visitas => Set<Visita>();

    public DbSet<LineaVisita> LineasVisita => Set<LineaVisita>();

    public Task GuardarCambiosAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeluqueriaDbContext).Assembly);
}
