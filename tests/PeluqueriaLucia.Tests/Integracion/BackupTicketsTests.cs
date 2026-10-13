using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class BackupTicketsTests
{
    [Fact]
    public async Task Tickets_sin_cliente_conservan_precio_cobros_e_iva_y_se_pueden_listar()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await bd.EjecutarAsync("""
            INSERT INTO ServiciosClientes (Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva)
            VALUES ('2026-10-01',NULL,2,1,72.60,0,0), ('2026-10-02',NULL,1,1,0,72.60,21);
            INSERT INTO ServiciosGenericosRealizados (ServicioClienteId,ServicioGenericoId,NombreAplicado,PrecioAplicado)
            SELECT v.Id,s.Id,N'Mechas antiguas',72.60 FROM ServiciosClientes v
            CROSS JOIN ServiciosGenericos s WHERE s.Nombre=N'Mechas';
            """);

        await using var db = bd.Contexto();
        var visitas = await db.Visitas.Include(v => v.Cliente).Include(v => v.Lineas).OrderBy(v => v.Id).ToListAsync();
        Assert.Equal(2, visitas.Count);
        Assert.All(visitas, v =>
        {
            Assert.Null(v.ClienteId);
            Assert.Null(v.Cliente);
            Assert.Equal(72.60m, v.Total);
            Assert.Equal("Mechas antiguas", Assert.Single(v.Lineas).Nombre);
        });
        Assert.Equal(72.60m, visitas[0].TotalEfectivo);
        Assert.Equal(72.60m, visitas[1].TotalTarjeta);

        var negocio = new DatosNegocio { TipoImpuesto = 21 };
        var exento = new TicketModelo(visitas[0], negocio);
        Assert.Equal(0m, exento.TipoImpuesto);
        Assert.Equal(72.60m, exento.Base);
        Assert.Equal(0m, exento.Cuota);
        var gravado = new TicketModelo(visitas[1], negocio);
        Assert.Equal(21m, gravado.TipoImpuesto);
        Assert.Equal(60m, gravado.Base);
        Assert.Equal(12.60m, gravado.Cuota);
    }

    [Fact]
    public async Task Actualizar_esquema_conserva_visitas_y_tipo_configurado_cuando_no_hay_iva_historico()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261012090119_OrigenDeLasCitas");
        await bd.EjecutarAsync("""
            INSERT INTO Clientes (Nombre,Apellido,FechaCreacion,Sexo) VALUES (N'Ana',N'Prueba','2026-10-01',1);
            INSERT INTO ServiciosClientes (Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta)
            VALUES ('2026-10-01',1,2,1,121,0);
            INSERT INTO ServiciosGenericosRealizados (ServicioClienteId,ServicioGenericoId,NombreAplicado,PrecioAplicado)
            SELECT 1,Id,N'Mechas antiguas',121 FROM ServiciosGenericos WHERE Nombre=N'Mechas';
            """);
        await using var db = bd.Contexto();
        await db.Database.MigrateAsync();
        var visita = await db.Visitas.Include(v => v.Lineas).SingleAsync();
        Assert.Equal(1, visita.ClienteId);
        Assert.Null(visita.PorcentajeIva);
        Assert.Equal(121m, visita.TotalEfectivo);
        var ticket = new TicketModelo(visita, new DatosNegocio { TipoImpuesto = 21 });
        Assert.Equal(100m, ticket.Base);
        Assert.Equal(21m, ticket.Cuota);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
