using Microsoft.EntityFrameworkCore;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class MigracionComunicacionesTests
{
    [Fact]
    public async Task Actualizar_conserva_reservas_catalogo_y_archiva_la_cola_anterior()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261010192215_AgendaCitasYComunicaciones");
        await bd.EjecutarAsync("""
            DECLARE @id uniqueidentifier = NEWID();
            INSERT ReservasCitas (Id,SolicitudId,Inicio,Fin,Nombre,Telefono,ServicioNombre,Estado,CreadaUtc,ActualizadaUtc,CaducaUtc,IpHash,TelefonoValidado,NotaInterna)
            VALUES (@id,NEWID(),'2030-10-12T10:00:00','2030-10-12T10:30:00',N'Prueba','+34612345678',N'Consulta',1,SYSUTCDATETIME(),SYSUTCDATETIME(),DATEADD(day,1,SYSUTCDATETIME()),'',1,'');
            INSERT MensajesCitas (Id,ReservaId,Telefono,Texto,Estado,CreadoUtc) VALUES (NEWID(),@id,'+34612345678',N'Aviso anterior',0,SYSUTCDATETIME());
            INSERT SmsEntrantes (Id,ProveedorId,Telefono,Texto,RecibidoUtc) VALUES (NEWID(),'referencia-ficticia','+34612345678',N'Respuesta anterior',SYSUTCDATETIME());
            """);
        await using var db = bd.Contexto(); await db.Database.MigrateAsync();
        Assert.Equal(1, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas WHERE Estado=1 AND Correo='' AND ConfirmacionHash IS NULL"));
        Assert.Equal(1, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM NotificacionesCitas WHERE Estado=6 AND Destinatario='+34612345678'"));
        Assert.Equal(1, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ComunicacionesHistoricas WHERE Texto=N'Respuesta anterior'"));
        Assert.Equal(42, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosGenericos"));
        Assert.Equal(0, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='SmsEntrantes'"));
    }
}
