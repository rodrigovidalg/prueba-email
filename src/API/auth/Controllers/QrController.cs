using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Auth.Infrastructure.Data;                      // AppDbContext
using Auth.Infrastructure.Services;                 // QrService, QrCardGenerator
using Auth.Infrastructure.Services.Notifications;   // INotificationService
using Auth.Application.DTOs;

namespace Auth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QrController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly QrService _qrService;                 // ← concreto (puedes dejarlo así)
    private readonly QrCardGenerator _card;                // ← concreto
    private readonly INotificationService _notify;
    private readonly ILogger<QrController> _logger;        // ← logger para ver el motivo si falla el correo

    public QrController(
        AppDbContext db,
        QrService qrService,
        QrCardGenerator card,
        INotificationService notify,
        ILogger<QrController> logger)                      // ← inyectamos logger
    {
        _db = db;
        _qrService = qrService;
        _card = card;
        _notify = notify;
        _logger = logger;
    }

    /// <summary>Genera/obtiene el QR del usuario y envía el carnet por email.</summary>
    [HttpPost("send-card")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]     // correo enviado
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]// QR ok, correo no enviado
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendCard([FromBody] QrSendRequest dto)
    {
        var user = await _db.Usuarios.FirstOrDefaultAsync(u =>
            u.UsuarioNombre == dto.UsuarioOrEmail || u.Email == dto.UsuarioOrEmail);

        if (user is null || !user.Activo)
            return NotFound("Usuario no encontrado o inactivo.");

        // 1) Obtener/crear código QR
        var qr = await _qrService.GetOrCreateUserQrAsync(user.Id);

        // 2) Generar PDF del carnet con el QR
        var pdf = _card.GenerateRegistrationPdf(
            fullName: user.NombreCompleto,
            userName: user.UsuarioNombre,
            email: user.Email,
            qrPayload: qr.Codigo
        );

        // 3) Enviar correo (cubre fallo de SMTP en Railway sin tirar 500)
        var subject = "Tu carnet de acceso con código QR";
        var body = $@"
            <p>Hola <b>{user.NombreCompleto}</b>,</p>
            <p>Adjuntamos tu carnet de acceso con código QR.</p>
            <p>Si no solicitaste este correo, por favor contacta a soporte.</p>
        ";

        try
        {
            await _notify.SendEmailAsync(
                toEmail: user.Email,
                subject: subject,
                htmlBody: body,
                attachment: (pdf.FileName, pdf.Content, pdf.ContentType)
            );

            return Ok(new { message = "Carnet enviado al correo del usuario." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo enviando correo a {Email}", user.Email);

            // 202 = aceptado/procesado parcialmente: QR ok, pero el email falló (por ejemplo SMTP bloqueado)
            return Accepted(new
            {
                message = "QR generado correctamente, pero no se pudo enviar el correo en este momento."
            });
        }
    }
}
