namespace PeluqueriaLucia.Web.Models;

/// <summary>Página de error del sitio. Los textos están en español y la vista los traduce (son claves de Textos).</summary>
public sealed record ErrorModelo(int Codigo, string? IdPeticion)
{
    public string Titulo => Codigo switch
    {
        404 => "No encontramos esta página",
        400 => "La petición no es válida",
        401 or 403 => "Esta zona es privada",
        405 => "Esta acción no está disponible",
        410 => "Este enlace ya no está disponible",
        429 => "Demasiadas peticiones seguidas",
        503 => "Volvemos enseguida",
        _ => "Algo ha salido mal",
    };

    public string Descripcion => Codigo switch
    {
        404 => "Puede que el enlace sea antiguo o que lo que buscas se haya eliminado.",
        400 => "Vuelve atrás e inténtalo otra vez. Si la página llevaba mucho tiempo abierta, recárgala primero.",
        401 or 403 => "No tienes permiso para ver esta página.",
        405 => "Vuelve a la página anterior y usa sus botones para continuar.",
        410 => "Ha caducado o ya se ha utilizado. Contacta con el equipo si necesitas ayuda.",
        429 => "Has hecho muchas peticiones en poco tiempo. Espera un minuto y vuelve a intentarlo.",
        503 => "Estamos haciendo un pequeño mantenimiento. Vuelve a intentarlo en unos minutos.",
        _ => "No hemos podido completar esta operación. Vuelve a intentarlo en unos minutos.",
    };

    /// <summary>Número grande de la página: el código si es un error HTTP conocido.</summary>
    public string Numero => Codigo is >= 400 and <= 599 ? Codigo.ToString(System.Globalization.CultureInfo.InvariantCulture) : "!";
}
