using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Tests.Dominio;

/// <summary>
/// Moderación de una reseña: al publicarla queda cuándo; solo se oculta una publicada (y se puede volver a publicar); cada
/// cambio de estado anota cuándo se revisó, y guardar solo la respuesta no lo cambia.
/// </summary>
public sealed class ResenaTests
{
    private static readonly DateTime Escrita = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

    private static Resena Nueva() => Resena.Crear(Guid.NewGuid(), "Marta G.", "Me encantó el color vegetal del tinte.", 5, null, Escrita);

    [Fact]
    public void Al_publicarla_queda_cuando_y_al_guardar_otra_respuesta_no_cambia()
    {
        var resena = Nueva();
        var publicada = Escrita.AddDays(1);

        resena.Moderar(EstadoResena.Publicada, "¡Gracias!", "Thank you!", publicada);
        resena.Moderar(EstadoResena.Publicada, "¡Muchas gracias!", "Thank you so much!", publicada.AddHours(3));

        Assert.Equal((EstadoResena.Publicada, (DateTime?)publicada, (DateTime?)publicada), (resena.Estado, resena.PublicadaEn, resena.RevisadaEn));
        Assert.Equal(("¡Muchas gracias!", "Thank you so much!"), (resena.Respuesta, resena.RespuestaEn));
    }

    [Fact]
    public void Una_publicada_se_oculta_y_se_vuelve_a_publicar_con_la_fecha_nueva()
    {
        var resena = Nueva();
        var publicada = Escrita.AddDays(1);
        var oculta = Escrita.AddDays(4);
        var deNuevo = Escrita.AddDays(9);

        resena.Moderar(EstadoResena.Publicada, null, null, publicada);
        resena.Moderar(EstadoResena.Oculta, null, null, oculta);
        Assert.Equal((EstadoResena.Oculta, (DateTime?)publicada, (DateTime?)oculta), (resena.Estado, resena.PublicadaEn, resena.RevisadaEn));

        resena.Moderar(EstadoResena.Publicada, null, null, deNuevo);
        Assert.Equal((EstadoResena.Publicada, (DateTime?)deNuevo, (DateTime?)deNuevo), (resena.Estado, resena.PublicadaEn, resena.RevisadaEn));
    }

    [Fact]
    public void Solo_se_ocultan_las_publicadas_y_rechazar_anota_cuando()
    {
        var pendiente = Nueva();
        var error = Assert.Throws<ReglaDeNegocioException>(() => pendiente.Moderar(EstadoResena.Oculta, null, null, Escrita.AddDays(1)));
        Assert.Equal("Solo se ocultan las reseñas publicadas.", error.Message);
        Assert.Equal((EstadoResena.Pendiente, (DateTime?)null), (pendiente.Estado, pendiente.RevisadaEn));

        var rechazada = Escrita.AddDays(2);
        pendiente.Moderar(EstadoResena.Rechazada, null, null, rechazada);
        Assert.Equal((EstadoResena.Rechazada, (DateTime?)rechazada, (DateTime?)null), (pendiente.Estado, pendiente.RevisadaEn, pendiente.PublicadaEn));
        Assert.Throws<ReglaDeNegocioException>(() => pendiente.Moderar(EstadoResena.Oculta, null, null, rechazada.AddDays(1)));
    }
}
