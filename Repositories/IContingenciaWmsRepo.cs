using ContingenciaWms.DTOs;

namespace ContingenciaWms.Repositories
{
    public interface IContingenciaWmsRepo
    {
        Task<int> ObtenerVersionConfigAsync();
        Task<ConfiguracionRutinaDTO> ObtenerConfiguracionRutinaAsync(int defIntervalo, int defDiasAtras);
        Task<int> ObtenerIntervaloSegundosAsync(int valorPorDefecto);
        Task<List<SuscriptorCorreoDTO>> ObtenerSuscriptoresCorreoAsync(string codigoRutina = "CONTINGENCIA_WMS");
        Task<List<ResultadoContingenciaDTO>> EjecutarContingenciaAsync(int diasAtras);
    }
}
