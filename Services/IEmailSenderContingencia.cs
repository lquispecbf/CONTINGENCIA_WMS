using ContingenciaWms.DTOs;

namespace ContingenciaWms.Services
{
    public interface IEmailSenderContingencia
    {
        Task<bool> EnviarAlertaRescateAsync(List<ResultadoContingenciaDTO> ordenesRescatadas, List<SuscriptorCorreoDTO> destinatarios);
    }
}

