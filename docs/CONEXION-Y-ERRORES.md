# Servicio conectado SQL

La web y las herramientas de Entity Framework utilizan `ServicioConexionSql`. No leen `ConnectionStrings` desde appsettings, variables de configuración ni User Secrets. No hay contraseñas en el repositorio.

En Development, si no hay credencial, se puede usar SQL local con autenticación Windows, sin contraseña. Fuera de Development se exige configurar el servicio. En Windows ejecutar bajo el mismo usuario que ejecutará Visual Studio o la web:

```powershell
dotnet run --project src/PeluqueriaLucia.Web -- configurar-conexion
```

Enter configura la instancia local con autenticación integrada de Windows. Otra conexión se introduce de forma oculta, se valida con `SqlConnectionStringBuilder`, se verifica abriendo SQL y se guarda como credencial genérica `PeluqueriaLucia/SqlServer` en el Administrador de credenciales de Windows. IIS necesita configurar esta credencial bajo su identidad de ejecución; no comparte la del desarrollador. Este proveedor propio no es el asistente de Azure de Visual Studio: dicho asistente suele generar appsettings/User Secrets. Para una futura instalación Azure se puede sustituir el proveedor por Key Vault con identidad administrada, sin cambiar repositorios ni dominio.

No publicar archivos antiguos appsettings.Production.json con contraseñas. El servicio ignora sus cadenas. Si hubo credenciales SQL en Git, rotarlas en el servidor y gestionar su historial por separado.

# Reconstrucción

La migración `CatalogoInicialCompleto` incorpora la copia del catálogo del 10/10/2026 (42 servicios, 6 categorías), incluidos los internos. Solo inserta nombres ausentes; respeta precios e imágenes modificados. Las siguientes modificaciones administrativas requieren su propia copia de seguridad: esta semilla es una fotografía de esa fecha.

```powershell
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
```

Una base vacía se reconstruye completa. Borrar tablas conservando `__EFMigrationsHistory` no permite reconstruirlas: EF considera esas migraciones ya aplicadas. No se incluyen clientes, visitas, contraseñas ni MFA en la semilla. Después de recrear toda la base hay que crear el administrador con el comando `administrador-nuevo` (solo crea, nunca modifica cuentas existentes) y vincular su Authenticator. El comando `administrador` permite restablecer una cuenta existente desde el servidor.

# Errores

La respuesta 500 siempre es genérica, incluso en Development. El manejador no depende de SQL, cookies, TempData ni vistas, por lo que también funciona si falla la protección de cookies. Los detalles y la referencia de la petición quedan en los registros del servidor. Una operación que falla puede haberse guardado: verificar su estado antes de repetirla.

# Azure

En Azure App Service la cadena de conexión no sale del almacén de Windows: se lee de la cadena `Peluqueria` de la Web App (variable `SQLAZURECONNSTR_Peluqueria`), sin contraseña, con la identidad administrada. Desde un PC se puede apuntar a Azure con la variable `ConnectionStrings__Peluqueria` y `Authentication=Active Directory Default` (sesión de `az login`). Ver [AZURE.md](AZURE.md).
