# Guarda la contraseña de aplicación de Gmail de PRODUCCIÓN («Peluqueria Prod») en Key Vault (secreto correo-smtp),
# desactiva las versiones anteriores del secreto y reinicia la Web App para que la use.
# La contraseña se pide oculta y nunca se escribe en el repositorio: pasa por un fichero temporal que se borra al terminar.
# Requiere «az login» con permiso para escribir secretos en el Key Vault.
#   powershell -ExecutionPolicy Bypass -File scripts/actualizar-correo-azure.ps1
param(
    [string]$Almacen = 'kv-lucia-peluqueria',
    [string]$Secreto = 'correo-smtp',
    [string]$Grupo = 'rg-lucia-prod',
    [string]$WebApp = 'WebAppLuciaPeluqueria',
    [string]$Cuenta = 'luciaespina.p.e@gmail.com'
)
$ErrorActionPreference = 'Stop'

$segura = Read-Host 'Contraseña de aplicación «Peluqueria Prod» (se puede pegar con espacios)' -AsSecureString
$puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura)
try { $clave = ([Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero)) -replace '\s', '' }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero) }
if ($clave -notmatch '^[a-zA-Z]{16}$') { throw 'Una contraseña de aplicación de Google tiene 16 letras. Cópiala de nuevo; no se ha cambiado nada.' }

$json = [ordered]@{
    Servidor = 'smtp.gmail.com'; Puerto = 587; Seguridad = 'StartTls'; Usuario = $Cuenta; Contrasena = $clave
    Remitente = $Cuenta; NombreRemitente = 'Lucía Peluquería Ecológica'
} | ConvertTo-Json -Compress
$fichero = Join-Path ([IO.Path]::GetTempPath()) ('correo-smtp-' + [guid]::NewGuid() + '.json')
try {
    # UTF-8 sin BOM: así Key Vault guarda el JSON tal cual, con sus comillas
    [IO.File]::WriteAllText($fichero, $json, (New-Object Text.UTF8Encoding $false))
    $nueva = az keyvault secret set --vault-name $Almacen --name $Secreto --file $fichero --query id -o tsv
    if ($LASTEXITCODE -ne 0 -or -not $nueva) { throw 'No se ha podido guardar el secreto en Key Vault. Comprueba «az login».' }
}
finally { Remove-Item $fichero -ErrorAction SilentlyContinue; $clave = $null; $json = $null }
Write-Host 'Secreto guardado en Key Vault (versión nueva).'

# Las versiones anteriores llevan contraseñas ya revocadas: se desactivan (no se borran; se podrían reactivar)
$anteriores = az keyvault secret list-versions --vault-name $Almacen --name $Secreto --query '[?attributes.enabled].id' -o tsv
foreach ($id in $anteriores) {
    if ($id -and $id -ne $nueva) {
        az keyvault secret set-attributes --id $id --enabled false --output none
        if ($LASTEXITCODE -eq 0) { Write-Host 'Desactivada una versión anterior del secreto.' }
    }
}

# Las referencias a Key Vault se guardan en caché: el reinicio hace que la Web App lea ya la versión nueva
az webapp restart -g $Grupo -n $WebApp --output none
if ($LASTEXITCODE -ne 0) { throw 'El secreto está guardado, pero no se ha podido reiniciar la Web App. Reiníciala desde el portal.' }
Write-Host 'Web App reiniciada. En un par de minutos enviará con la contraseña nueva.'
