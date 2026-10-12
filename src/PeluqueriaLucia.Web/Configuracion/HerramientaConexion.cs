using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Web.Configuracion;

internal static class HerramientaConexion
{
    public static async Task<int> ConfigurarAsync()
    {
        try
        {
            Console.WriteLine("Servicio conectado SQL. La credencial se guarda en Windows, fuera del repositorio.");
            var valor = HerramientaAdministrador.LeerOculto("Cadena SQL (Enter para conexión local con autenticación Windows): ");
            var cadena = ServicioConexionSql.Validar(string.IsNullOrWhiteSpace(valor) ? ServicioConexionSql.ConexionLocal() : valor);
            await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(cadena);
            await conexion.OpenAsync();
            ServicioConexionSql.GuardarCredencial(ServicioConexionSql.Destino, cadena);
            Console.WriteLine("Conexión verificada y guardada. No se han mostrado credenciales.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("No se ha podido verificar o guardar la conexión. Revisa servidor, base de datos y permisos.");
            return 1;
        }
    }
}
