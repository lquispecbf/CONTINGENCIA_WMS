using ContingenciaWms.Configuration;
using ContingenciaWms.DTOs;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ContingenciaWms.Services
{
    public class EmailSenderContingencia : IEmailSenderContingencia
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailSenderContingencia> _logger;

        public EmailSenderContingencia(IOptions<SmtpSettings> settings, ILogger<EmailSenderContingencia> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> EnviarAlertaRescateAsync(List<ResultadoContingenciaDTO> ordenesRescatadas, List<SuscriptorCorreoDTO> destinatarios)
        {
            if (ordenesRescatadas == null || ordenesRescatadas.Count == 0)
                return false;

            if (destinatarios == null || destinatarios.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios de correo configurados para Contingencia WMS.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Password) || _settings.Password == "@")
            {
                _logger.LogWarning("Configuración SMTP incompleta en appsettings.json de Contingencia WMS (Password no configurada). Se omite el envío de correo.");
                return false;
            }

            try
            {
                string asunto = ordenesRescatadas.Count == 1
                    ? $"NOTIFICACIÓN CBF: Pedido #{ordenesRescatadas[0].DocNumSap} Regularizado y Reenviado a WMS"
                    : $"NOTIFICACIÓN CBF: {ordenesRescatadas.Count} Pedidos Regularizados y Reenviados a WMS";

                string cuerpoHtml = ConstruirCuerpoHtml(ordenesRescatadas);

                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.From, _settings.FromName),
                    Subject = asunto,
                    Body = cuerpoHtml,
                    IsBodyHtml = true,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8
                };

                foreach (var dest in destinatarios)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Correo))
                    {
                        message.To.Add(dest.Correo.Trim());
                    }
                }

                if (message.To.Count == 0)
                {
                    _logger.LogWarning("No se encontraron correos válidos en la lista de suscriptores.");
                    return false;
                }

                using var smtp = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                if (!string.IsNullOrWhiteSpace(_settings.User) && !string.IsNullOrWhiteSpace(_settings.Password))
                {
                    smtp.Credentials = new NetworkCredential(_settings.User.Trim(), _settings.Password.Trim());
                }

                await smtp.SendMailAsync(message);

                _logger.LogInformation(">>> Correo de contingencia enviado exitosamente a {Total} destinatario(s).", message.To.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de alerta de Contingencia WMS: {Mensaje}", ex.Message);
                return false;
            }
        }

        private string ConstruirCuerpoHtml(List<ResultadoContingenciaDTO> ordenes)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html lang='es'>");
            sb.Append("<head><meta charset='UTF-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background-color: #f4f6f9; color: #2c3e50; }");
            sb.Append(".card { max-width: 680px; margin: 0 auto; background: #ffffff; border-radius: 8px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); overflow: hidden; border: 1px solid #e1e8ed; }");
            sb.Append(".card-header { background: linear-gradient(135deg, #1ab394 0%, #17a085 100%); color: #ffffff; padding: 22px 26px; }");
            sb.Append(".card-header h2 { margin: 0 0 6px 0; font-size: 19px; font-weight: 700; letter-spacing: 0.3px; color: #ffffff; }");
            sb.Append(".card-header p { margin: 0; font-size: 13px; color: rgba(255,255,255,0.92); }");
            sb.Append(".card-body { padding: 24px; }");
            sb.Append(".summary-box { background: #f0fdf4; border-left: 4px solid #1ab394; padding: 15px 18px; margin-bottom: 20px; border-radius: 0 6px 6px 0; font-size: 13.5px; line-height: 1.5; color: #2d3748; }");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 13px; }");
            sb.Append("th { background-color: #f8fafc; color: #475569; text-align: left; padding: 11px 14px; border: 1px solid #e2e8f0; font-weight: 600; font-size: 12.5px; text-transform: uppercase; letter-spacing: 0.3px; }");
            sb.Append("td { padding: 12px 14px; border: 1px solid #e2e8f0; vertical-align: middle; }");
            sb.Append("tr:nth-child(even) { background-color: #fbfcfe; }");
            sb.Append(".badge-pill { display: inline-block; padding: 4px 10px; border-radius: 12px; font-size: 11.5px; font-weight: 600; text-align: center; }");
            sb.Append(".badge-warning { background-color: #fef3c7; color: #92400e; }");
            sb.Append(".badge-success { background-color: #d1fae5; color: #065f46; }");
            sb.Append(".footer { background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0; line-height: 1.5; }");
            sb.Append("</style></head><body>");

            sb.Append("<div class='card'>");
            sb.Append("<div class='card-header'>");
            sb.Append("<h2>NOTIFICACIÓN DE CONTINGENCIA WMS</h2>");
            sb.Append("<p>Compañía Farmacéutica CBF &bull; Regularización Automática de Pedidos</p>");
            sb.Append("</div>");

            sb.Append("<div class='card-body'>");
            sb.Append("<div class='summary-box'>");
            sb.Append("<strong>Estimado equipo:</strong><br>");
            sb.Append($"El sistema de contingencia identificó y regularizó automáticamente <strong>{(ordenes.Count == 1 ? "1 pedido" : $"{ordenes.Count} pedidos")}</strong> que presentaba(n) demora en su transmisión a almacén (WMS).<br><br>");
            sb.Append("<strong>Acción Automática Realizada:</strong> Las órdenes fueron reencoladas y reenviadas exitosamente a WMS para que el almacén proceda con su preparación y despacho habitual.<br>");
            sb.Append("<em style='color: #64748b; font-size: 12px;'>No se requiere ninguna acción manual de su parte. Este proceso es 100% automático.</em>");
            sb.Append("</div>");

            sb.Append("<table>");
            sb.Append("<thead><tr>");
            sb.Append("<th style='width: 150px;'>N° Pedido (SAP)</th>");
            sb.Append("<th>Situación Detectada</th>");
            sb.Append("<th>Estado Actual</th>");
            sb.Append("</tr></thead>");
            sb.Append("<tbody>");

            string fechaHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            foreach (var item in ordenes)
            {
                sb.Append("<tr>");
                sb.Append($"<td><strong style='color: #1ab394; font-size: 14px;'>#{item.DocNumSap}</strong><br><small style='color: #64748b;'>{fechaHora}</small></td>");
                sb.Append("<td><span class='badge-pill badge-warning'>Pendiente de envío a WMS</span></td>");
                sb.Append("<td><span class='badge-pill badge-success'>Reenviado a WMS (Listo para Despacho)</span></td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append("</div>");

            sb.Append("<div class='footer'>");
            sb.Append("Este es un aviso informativo automático generado por el Sistema de Monitoreo CBF.<br>Por favor no responder a este mensaje.");
            sb.Append("</div>");
            sb.Append("</div>");

            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}

