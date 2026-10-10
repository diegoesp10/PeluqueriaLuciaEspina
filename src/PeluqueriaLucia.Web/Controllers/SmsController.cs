using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Agenda;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken, RequestSizeLimit(65536), RequestFormLimits(ValueCountLimit=100, ValueLengthLimit=16384)]
public sealed class SmsController(ISmsCitas sms) : Controller
{
    [HttpPost("/sms/entrada")] public Task<IActionResult> Entrada(CancellationToken ct) => Procesar(true,ct);
    [HttpPost("/sms/estado")] public Task<IActionResult> Estado(CancellationToken ct) => Procesar(false,ct);
    private async Task<IActionResult> Procesar(bool entrada,CancellationToken ct)
    {
        if(!Request.HasFormContentType || Request.QueryString.HasValue) return BadRequest();
        var formulario = await Request.ReadFormAsync(ct);
        if (formulario.Any(x => x.Value.Count != 1)) return BadRequest();
        var campos=formulario.ToDictionary(x=>x.Key,x=>x.Value.ToString());
        if(!sms.ValidarFirma(Request.Path.ToString(),Request.Headers["X-Twilio-Signature"].ToString(),campos)) return Unauthorized();
        await sms.ProcesarWebhookAsync(campos,entrada,ct);
        return entrada ? Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>","text/xml") : Ok();
    }
}
