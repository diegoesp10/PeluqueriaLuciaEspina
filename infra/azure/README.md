# Infraestructura en Azure

Plantillas ARM exportadas del portal al crear los recursos, para poder recrearlos o automatizarlos más adelante.

| Fichero | Qué crea |
|---|---|
| `webapp.template.json` | Plan de App Service Linux B1 y la Web App .NET 10 (`WebAppLuciaPeluqueria`), solo HTTPS, Always On, FTP desactivado |
| `webapp.parameters.json` | Valores usados en el portal (Spain Central, grupo `rg-lucia-prod`). El ID de la suscripción no se guarda: se rellena al desplegar |

## Desplegar la plantilla

```powershell
az deployment group create `
  --resource-group rg-lucia-prod `
  --template-file infra/azure/webapp.template.json `
  --parameters infra/azure/webapp.parameters.json `
  --parameters subscriptionId=$(az account show --query id -o tsv)
```

## Lo que la plantilla todavía no incluye

La plantilla es la que genera el portal y solo cubre el plan y la Web App. Para automatizar todo el entorno faltaría añadir:

- **Configuración de la Web App** que se hace después a mano: identidad administrada asignada por el sistema, TLS mínimo 1.2 y las variables `ASPNETCORE_ENVIRONMENT=Production` y `TZ=Europe/Madrid` (en `siteConfig.appSettings`).
- **Azure SQL**: servidor `sql-lucia-peluqueria` con autenticación solo de Microsoft Entra, base de producción `PeluqueriaLuciaProd` (Basic 5 DTU; la oferta gratuita no está disponible en Spain Central), intercalación `Modern_Spanish_CI_AS`, reglas de firewall.
- **Key Vault** `kv-lucia-peluqueria` con permisos por roles, la clave `proteccion-datos` y las asignaciones de rol para la identidad de la Web App.
- **Dominio y certificado** de Cloudflare (`luciapeluqueriaecologica.com`).

La configuración que la app necesita en la Web App y el despliegue con GitHub Actions están en [docs/AZURE.md](../../docs/AZURE.md).

El acceso de la Web App a cada base de datos no se puede expresar en ARM: se concede con T-SQL (`CREATE USER [WebAppLuciaPeluqueria] FROM EXTERNAL PROVIDER`).
