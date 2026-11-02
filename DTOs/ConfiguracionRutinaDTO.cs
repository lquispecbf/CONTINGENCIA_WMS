namespace ContingenciaWms.DTOs
{
    public class ConfiguracionRutinaDTO
    {
        public int IdRutina { get; set; }
        public string CodigoRutina { get; set; } = "CONTINGENCIA_WMS";
        public int IntervaloSegundos { get; set; } = 60;
        public bool Habilitado { get; set; } = true;
        public int DiasAtras { get; set; } = 3;
        public bool CanalCorreo { get; set; } = false;
        public bool CanalIntranet { get; set; } = true;
    }
}

