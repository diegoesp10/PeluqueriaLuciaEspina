using PeluqueriaLucia.Domain.Clientes;
namespace PeluqueriaLucia.Infrastructure.Persistencia;
internal static class ConsultaContactos
{
    public static IQueryable<Cliente> Candidatos(IQueryable<Cliente> clientes, string telefono, string correo)
    {
        var local = telefono[3..]; var email = correo.Trim().ToLowerInvariant();
        return clientes.Where(x => (x.Email != null && x.Email.Trim().ToLower() == email) ||
            (x.Telefono != null && (x.Telefono.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == telefono ||
            x.Telefono.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == local ||
            x.Telefono.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == "0034" + local)));
    }
}
