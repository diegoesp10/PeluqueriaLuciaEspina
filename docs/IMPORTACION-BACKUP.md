# Importar un backup de la versión anterior

La importación se realiza con `scripts/ImportarBackupLucia.sql`, mediante una base auxiliar restaurada del backup. No se restaura encima de la base de trabajo. El esquema admitido es el que contiene `TicketSinClientePrecioEditableIva`: líneas con `PrecioAplicado`, visitas con `PorcentajeIva` y `ClienteId` opcional.

Antes de ejecutar el script, hacer una copia de seguridad verificada del destino y aplicar las migraciones, incluida `CompatibilidadBackupTickets`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
```

Restaurar el backup en una base auxiliar con otro nombre y archivos físicos distintos, sin `WITH REPLACE`. `RESTORE FILELISTONLY` permite obtener los nombres lógicos de los archivos para `WITH MOVE`. El script requiere que el destino no tenga clientes, visitas ni líneas; si ya los tiene, aborta sin sobrescribirlos ni duplicarlos. El nombre de origen y destino debe referirse a bases de la misma instancia SQL Server.

```powershell
sqlcmd -S localhost -E -C -b -f 65001 -i scripts/ImportarBackupLucia.sql -v Origen="Lucia_Importacion_20261013" Destino="PeluqueriaLuciaDb"
```

Los INSERT se ejecutan en una transacción con bloqueos para evitar altas simultáneas. Antes del commit se comprueban los recuentos y se comparan clientes, visitas, líneas y el catálogo previo. Un error revierte la importación. El script muestra al final el mapa de identificadores del catálogo; conviene guardar esa salida.

Se conservan los identificadores de clientes, visitas y líneas, los contactos, las fechas, los estados, las formas de pago, los cobros, el IVA y el precio que cada línea tenía guardado. Las visitas sin cliente permanecen sin cliente; la aplicación las muestra como «Sin cliente». El ticket utiliza el IVA histórico cuando existe y la configuración del negocio cuando no consta.

El catálogo se relaciona por nombre sin distinguir mayúsculas o acentos, ignorando espacios exteriores, y por sexo. Solo se reutilizan coincidencias inequívocas con el catálogo que existía antes de importar. Sus precios y configuración pública se conservan. Los servicios restantes se añaden activos como internos, con sus datos originales y duración inicial de 30 minutos. No se interpretan sinónimos ni se unifican filas distintas del backup. Las líneas guardan el nombre del servicio antiguo y su `PrecioAplicado`, aunque el catálogo actual use otro nombre o precio.

La importación no modifica usuarios, contraseña, MFA, sesiones, claves, agenda ni reseñas. Tampoco copia el historial de migraciones antiguo: el destino mantiene el correspondiente al proyecto actual. Las diferencias entre cobros y servicios que ya existan en el backup se conservan para su revisión.

Después de comparar el resultado con el origen y ejecutar `DBCC CHECKDB`, se pueden eliminar las bases auxiliares creadas para la importación. El `.bak` original y la copia del destino permiten reproducir o revertir la operación. No guardar backups ni salidas con datos privados en Git.
