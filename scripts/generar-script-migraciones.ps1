<#
.SYNOPSIS
    Genera el script SQL idempotente con todas las migraciones, para actualizar la base de datos de la peluquería.

.DESCRIPTION
    El script resultante se puede ejecutar tantas veces como se quiera: cada migración comprueba en __EFMigrationsHistory
    si ya está aplicada y solo aplica las que faltan, cada una dentro de su propia transacción.
    Ejecútalo en SQL Server Management Studio (o con sqlcmd) sobre la base de datos PeluqueriaLuciaDb, siempre después de
    hacer una copia de seguridad. Ver docs/DESPLIEGUE.md.

.EXAMPLE
    ./scripts/generar-script-migraciones.ps1
    ./scripts/generar-script-migraciones.ps1 -Salida C:\temp\migraciones.sql
#>
param(
    [string]$Salida = (Join-Path $PSScriptRoot '..\artifacts\migraciones.sql')
)

$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Force -Path (Split-Path $Salida -Parent) | Out-Null

Push-Location $raiz
try {
    dotnet tool restore | Out-Null
    dotnet ef migrations script --idempotent `
        --project src/PeluqueriaLucia.Infrastructure `
        --startup-project src/PeluqueriaLucia.Web `
        --output $Salida
    if ($LASTEXITCODE -ne 0) { throw 'No se ha podido generar el script de migraciones.' }
    Write-Host "Script generado en $((Resolve-Path $Salida).Path)" -ForegroundColor Green
}
finally {
    Pop-Location
}
