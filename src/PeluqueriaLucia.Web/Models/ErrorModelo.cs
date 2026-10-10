namespace PeluqueriaLucia.Web.Models;

public sealed record ErrorModelo(int Codigo, string? IdPeticion)
{
    public string Titulo => Codigo switch
    {
        404 => "No encontramos esta página",
        400 => "La petición no es válida",
        _ => "Algo ha salido mal",
    };

    public string Descripcion => Codigo switch
    {
        404 => "Puede que el enlace sea antiguo o que lo que buscas se haya eliminado.",
        400 => "Vuelve atrás e inténtalo otra vez. Si la página llevaba mucho tiempo abierta, recárgala primero.",
        _ => "No hemos podido completar esta operación. Vuelve a intentarlo en unos minutos.",
    };
}
