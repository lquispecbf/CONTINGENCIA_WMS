namespace ContingenciaWms.DTOs
{
    public class ResultadoContingenciaDTO
    {
        public int DocEntry { get; set; }
        public int DocNumSap { get; set; }
        public int DocEntrySap { get; set; }
        public string? EstadoAnterior { get; set; }
        public string? EstadoNuevo { get; set; }
        public string? EstadoSap { get; set; }
        public string? Mensaje { get; set; }
        public DateTime FechaCambio { get; set; }
    }
}

