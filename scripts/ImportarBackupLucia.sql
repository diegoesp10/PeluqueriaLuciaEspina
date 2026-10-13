-- Importación del esquema del backup LuciaActual.bak (TicketSinClientePrecioEditableIva).
-- Ejecutar con sqlcmd -b -f 65001 -v Origen="Lucia_Importacion_20261013" Destino="PeluqueriaLuciaDb".
-- Requiere copia previa y migración CompatibilidadBackupTickets aplicada.
-- Solo permite importar en una base sin clientes, visitas ni líneas: repetir aborta sin duplicar.
USE [$(Destino)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

IF DB_NAME() = N'$(Origen)'
    THROW 51000, 'El origen y el destino deben ser distintos.', 1;
IF COL_LENGTH(N'dbo.ServiciosClientes', N'PorcentajeIva') IS NULL
    THROW 51000, 'Aplica CompatibilidadBackupTickets antes de importar.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ServiciosClientes') AND name=N'ClienteId' AND is_nullable=1)
    THROW 51000, 'El destino no admite los tickets antiguos sin cliente.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Bloqueos conservados hasta terminar para evitar altas simultáneas durante la importación.
    IF EXISTS (SELECT 1 FROM dbo.Clientes WITH (TABLOCKX,HOLDLOCK))
       OR EXISTS (SELECT 1 FROM dbo.ServiciosClientes WITH (TABLOCKX,HOLDLOCK))
       OR EXISTS (SELECT 1 FROM dbo.ServiciosGenericosRealizados WITH (TABLOCKX,HOLDLOCK))
        THROW 51000, 'El destino ya contiene datos de negocio. Se cancela para no sobrescribir ni duplicar.', 1;

    SELECT Id,Nombre,Precio,ImagenRuta,Sexo,FechaCreacion
    INTO #CatalogoAnterior FROM dbo.ServiciosGenericos WITH (TABLOCKX,HOLDLOCK);
    SELECT Id,Nombre,Apellido,Email,Telefono,FechaCreacion,Sexo
    INTO #Clientes FROM [$(Origen)].dbo.Clientes WITH (TABLOCK,HOLDLOCK);
    SELECT Id,Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva
    INTO #Visitas FROM [$(Origen)].dbo.ServiciosClientes WITH (TABLOCK,HOLDLOCK);
    SELECT Id,Nombre,Precio,ImagenRuta,Sexo,FechaCreacion
    INTO #Servicios FROM [$(Origen)].dbo.ServiciosGenericos WITH (TABLOCK,HOLDLOCK);
    SELECT Id,ServicioClienteId,ServicioGenericoId,PrecioAplicado
    INTO #Lineas FROM [$(Origen)].dbo.ServiciosGenericosRealizados WITH (TABLOCK,HOLDLOCK);

    IF EXISTS (SELECT 1 FROM #Visitas v LEFT JOIN #Clientes c ON c.Id=v.ClienteId WHERE v.ClienteId IS NOT NULL AND c.Id IS NULL)
       OR EXISTS (SELECT 1 FROM #Lineas r LEFT JOIN #Visitas v ON v.Id=r.ServicioClienteId LEFT JOIN #Servicios s ON s.Id=r.ServicioGenericoId WHERE v.Id IS NULL OR s.Id IS NULL)
        THROW 51000, 'El backup contiene referencias huérfanas.', 1;

    CREATE TABLE #MapaServicios (OrigenId int PRIMARY KEY, DestinoId int NOT NULL, Insertado bit NOT NULL);
    -- Solo se reutilizan coincidencias de nombre y sexo del catálogo previo.
    -- No se interpretan sinónimos ni se unifican entre sí las filas distintas del backup.
    IF EXISTS (
        SELECT s.Id FROM #Servicios s JOIN #CatalogoAnterior d
          ON LTRIM(RTRIM(s.Nombre)) COLLATE Latin1_General_100_CI_AI = LTRIM(RTRIM(d.Nombre)) COLLATE Latin1_General_100_CI_AI
          AND s.Sexo=d.Sexo GROUP BY s.Id HAVING COUNT(*)>1)
        THROW 51000, 'Hay coincidencias ambiguas en el catálogo. Revisa el mapa antes de importar.', 1;

    INSERT INTO #MapaServicios (OrigenId,DestinoId,Insertado)
    SELECT s.Id,d.Id,0 FROM #Servicios s JOIN #CatalogoAnterior d
      ON LTRIM(RTRIM(s.Nombre)) COLLATE Latin1_General_100_CI_AI = LTRIM(RTRIM(d.Nombre)) COLLATE Latin1_General_100_CI_AI
      AND s.Sexo=d.Sexo;

    DECLARE @id int,@nombre nvarchar(100),@precio decimal(18,2),@imagen nvarchar(max),@sexo int,@creacion datetime2;
    DECLARE nuevos CURSOR LOCAL FAST_FORWARD FOR
        SELECT Id,Nombre,Precio,ImagenRuta,Sexo,FechaCreacion FROM #Servicios s
        WHERE NOT EXISTS (SELECT 1 FROM #MapaServicios m WHERE m.OrigenId=s.Id) ORDER BY Id;
    OPEN nuevos;
    FETCH NEXT FROM nuevos INTO @id,@nombre,@precio,@imagen,@sexo,@creacion;
    WHILE @@FETCH_STATUS=0
    BEGIN
        INSERT INTO dbo.ServiciosGenericos
            (Nombre,Precio,ImagenRuta,Sexo,FechaCreacion,Activo,CategoriaPublicaId,DescripcionPublica,Destacado,DuracionMinutos,OrdenPublico,Publicado,TipoPrecioPublico)
        VALUES (@nombre,@precio,@imagen,@sexo,@creacion,1,NULL,NULL,0,30,0,0,0);
        INSERT INTO #MapaServicios VALUES (@id,CONVERT(int,SCOPE_IDENTITY()),1);
        FETCH NEXT FROM nuevos INTO @id,@nombre,@precio,@imagen,@sexo,@creacion;
    END;
    CLOSE nuevos;
    DEALLOCATE nuevos;

    SET IDENTITY_INSERT dbo.Clientes ON;
    INSERT INTO dbo.Clientes (Id,Nombre,Apellido,Email,Telefono,FechaCreacion,Sexo,Apodo,PendienteCompletar)
    SELECT Id,Nombre,Apellido,Email,Telefono,FechaCreacion,Sexo,NULL,0 FROM #Clientes;
    SET IDENTITY_INSERT dbo.Clientes OFF;

    SET IDENTITY_INSERT dbo.ServiciosClientes ON;
    INSERT INTO dbo.ServiciosClientes (Id,Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva)
    SELECT Id,Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva FROM #Visitas;
    SET IDENTITY_INSERT dbo.ServiciosClientes OFF;

    SET IDENTITY_INSERT dbo.ServiciosGenericosRealizados ON;
    INSERT INTO dbo.ServiciosGenericosRealizados (Id,ServicioClienteId,ServicioGenericoId,NombreAplicado,PrecioAplicado)
    SELECT r.Id,r.ServicioClienteId,m.DestinoId,s.Nombre,r.PrecioAplicado
    FROM #Lineas r JOIN #MapaServicios m ON m.OrigenId=r.ServicioGenericoId JOIN #Servicios s ON s.Id=r.ServicioGenericoId;
    SET IDENTITY_INSERT dbo.ServiciosGenericosRealizados OFF;

    IF (SELECT COUNT(*) FROM dbo.Clientes)<>(SELECT COUNT(*) FROM #Clientes)
       OR (SELECT COUNT(*) FROM dbo.ServiciosClientes)<>(SELECT COUNT(*) FROM #Visitas)
       OR (SELECT COUNT(*) FROM dbo.ServiciosGenericosRealizados)<>(SELECT COUNT(*) FROM #Lineas)
       OR (SELECT COUNT(*) FROM #MapaServicios)<>(SELECT COUNT(*) FROM #Servicios)
        THROW 51000, 'Los recuentos no coinciden. Se revierte la importación.', 1;
    IF EXISTS (
        SELECT Id,Nombre COLLATE DATABASE_DEFAULT,Apellido COLLATE DATABASE_DEFAULT,Email COLLATE DATABASE_DEFAULT,Telefono COLLATE DATABASE_DEFAULT,FechaCreacion,Sexo FROM #Clientes
        EXCEPT SELECT Id,Nombre,Apellido,Email,Telefono,FechaCreacion,Sexo FROM dbo.Clientes)
       OR EXISTS (
        SELECT Id,Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva FROM #Visitas
        EXCEPT SELECT Id,Fecha,ClienteId,OpcionPago,Estado,TotalEfectivo,TotalTarjeta,PorcentajeIva FROM dbo.ServiciosClientes)
       OR EXISTS (
        SELECT r.Id,r.ServicioClienteId,m.DestinoId,s.Nombre COLLATE DATABASE_DEFAULT,r.PrecioAplicado
        FROM #Lineas r JOIN #MapaServicios m ON m.OrigenId=r.ServicioGenericoId JOIN #Servicios s ON s.Id=r.ServicioGenericoId
        EXCEPT SELECT Id,ServicioClienteId,ServicioGenericoId,NombreAplicado,PrecioAplicado FROM dbo.ServiciosGenericosRealizados)
       OR EXISTS (
        SELECT Id,Nombre COLLATE DATABASE_DEFAULT,Precio,ImagenRuta COLLATE DATABASE_DEFAULT,Sexo,FechaCreacion FROM #CatalogoAnterior
        EXCEPT SELECT Id,Nombre,Precio,ImagenRuta,Sexo,FechaCreacion FROM dbo.ServiciosGenericos)
        THROW 51000, 'La comparación de datos ha fallado. Se revierte la importación.', 1;

    COMMIT TRANSACTION;
    SELECT (SELECT COUNT(*) FROM #Clientes) ClientesImportados,(SELECT COUNT(*) FROM #Visitas) VisitasImportadas,
        (SELECT COUNT(*) FROM #Lineas) LineasImportadas,
        (SELECT COUNT(*) FROM #MapaServicios WHERE Insertado=1) ServiciosNuevos,
        (SELECT COUNT(*) FROM #MapaServicios WHERE Insertado=0) ServiciosReutilizados;
    SELECT m.OrigenId,m.DestinoId,m.Insertado,s.Nombre NombreAnterior,d.Nombre NombreActual,s.Precio PrecioCatalogoAnterior,d.Precio PrecioCatalogoActual
        FROM #MapaServicios m JOIN #Servicios s ON s.Id=m.OrigenId JOIN dbo.ServiciosGenericos d ON d.Id=m.DestinoId ORDER BY m.OrigenId;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    -- IDENTITY_INSERT es de sesión, no transaccional.
    SET IDENTITY_INSERT dbo.Clientes OFF;
    SET IDENTITY_INSERT dbo.ServiciosClientes OFF;
    SET IDENTITY_INSERT dbo.ServiciosGenericosRealizados OFF;
    THROW;
END CATCH;
